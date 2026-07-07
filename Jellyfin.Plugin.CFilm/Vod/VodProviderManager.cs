using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CFilm.Vod;

/// <summary>
/// 機能2の「頭脳」。ライブラリを走査し、TMDB から配信元(flatrate)を取得して
/// 一括対応表を作り、vod-cache.json に保存/読込します。
///
/// このクラスは DI(依存性注入)で1個だけ作られ(シングルトン)、
/// API と 定期タスクの両方から共有されます。
/// </summary>
public class VodProviderManager
{
    private readonly ILibraryManager _libraryManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VodProviderManager> _logger;

    // 同時に再構築が2回走らないようにする「通行証」(1本だけ)。
    private static readonly SemaphoreSlim _rebuildLock = new(1, 1);

    /// <summary>コンストラクタ。3つの部品は Jellyfin が自動で渡します(DI)。</summary>
    public VodProviderManager(
        ILibraryManager libraryManager,
        IHttpClientFactory httpClientFactory,
        ILogger<VodProviderManager> logger)
    {
        _libraryManager = libraryManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static string CacheFilePath => Path.Combine(Plugin.Instance!.CacheDirectory, "vod-cache.json");

    /// <summary>キャッシュがあれば即返す。無ければ1回だけ構築して返す。</summary>
    public async Task<VodProvidersResponse> GetOrBuildAsync(CancellationToken cancellationToken)
    {
        var cached = TryLoadCache();
        if (cached is not null)
        {
            return cached;
        }

        return await RebuildAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>強制的に作り直して保存する(?force / 定期タスク用)。</summary>
    public async Task<VodProvidersResponse> RebuildAsync(CancellationToken cancellationToken)
    {
        await _rebuildLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var response = await BuildAsync(cancellationToken).ConfigureAwait(false);

            // APIキー未設定 / ライブラリ未選択のときの「空の結果」は保存しない。
            // 保存すると、後で設定しても GET が空キャッシュを返し続けてしまうため。
            var config = Plugin.Instance!.Configuration;
            if (!string.IsNullOrWhiteSpace(config.TmdbApiKey) && config.VodLibraryIds.Length > 0)
            {
                SaveCache(response);
            }

            return response;
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <summary>実際の走査＋TMDB問い合わせ。</summary>
    private async Task<VodProvidersResponse> BuildAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        var apiKey = config.TmdbApiKey;
        var region = string.IsNullOrWhiteSpace(config.TmdbRegion)
            ? "JP"
            : config.TmdbRegion.Trim().ToUpperInvariant();

        var response = new VodProvidersResponse
        {
            Region = region,
            GeneratedAt = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            LibraryIds = config.VodLibraryIds.ToList()
        };

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("CFilm: TMDB API key is not set. VOD scan skipped.");
            return response;
        }

        if (config.VodLibraryIds.Length == 0)
        {
            _logger.LogWarning("CFilm: No libraries selected. VOD scan skipped.");
            return response;
        }

        // 選んだライブラリごとに絞り込んで走査する。
        // ParentId + Recursive=true で「そのライブラリ配下だけ」に限定できる
        // (標準の /Items?ParentId=...&Recursive=true と同じ仕組み)。
        // こうすることで、取得した瞬間に「どのライブラリの作品か」が分かる
        // (後から逆引きする必要がない)。
        var targets = new List<(string ItemId, string MediaType, string TmdbId, string LibraryId)>();
        var seenItemIds = new HashSet<Guid>();

        foreach (var libraryId in config.VodLibraryIds)
        {
            if (!Guid.TryParse(libraryId, out var libraryGuid))
            {
                _logger.LogWarning("CFilm: skipping invalid library id in configuration: {LibraryId}", libraryId);
                continue;
            }

            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                Recursive = true,
                ParentId = libraryGuid
            });

            foreach (var item in items)
            {
                // 同じ作品が複数の選択ライブラリに重複して出てくる場合は、最初に見つけた方を採用する。
                if (!seenItemIds.Add(item.Id))
                {
                    continue;
                }

                var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);
                if (string.IsNullOrEmpty(tmdbId))
                {
                    continue;
                }

                // 映画は /movie、シリーズは /tv とURLが違う。
                var mediaType = item is Series ? "tv" : "movie";
                // 作品IDは Jellyfin の見た目に合わせて 32桁ハイフン無し("N")。
                targets.Add((item.Id.ToString("N"), mediaType, tmdbId, libraryId));
            }
        }

        var results = new ConcurrentDictionary<string, VodItemEntry>();
        var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);

        // 同時に最大5本まで(TMDBに優しく)。
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 5,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(targets, parallelOptions, async (target, token) =>
        {
            try
            {
                var providers = await FetchProvidersAsync(httpClient, target.MediaType, target.TmdbId, apiKey, region, token).ConfigureAwait(false);
                if (providers.Count > 0)
                {
                    results[target.ItemId] = new VodItemEntry { LibraryId = target.LibraryId, Providers = providers };
                }
            }
            catch (Exception ex)
            {
                // 1件失敗しても全体は止めない。
                _logger.LogWarning(ex, "CFilm: failed to fetch providers for {MediaType}/{TmdbId}", target.MediaType, target.TmdbId);
            }
        }).ConfigureAwait(false);

        response.Items = new Dictionary<string, VodItemEntry>(results);
        response.Count = response.Items.Count;
        _logger.LogInformation("CFilm: VOD scan finished. {Count} items matched (region {Region}, {LibraryCount} libraries).", response.Count, region, config.VodLibraryIds.Length);
        return response;
    }

    /// <summary>TMDB の watch/providers を1件叩いて flatrate(定額見放題)だけ取り出す。</summary>
    private static async Task<List<VodProvider>> FetchProvidersAsync(
        HttpClient httpClient,
        string mediaType,
        string tmdbId,
        string apiKey,
        string region,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.themoviedb.org/3/{mediaType}/{tmdbId}/watch/providers?api_key={apiKey}";

        using var resp = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            return new List<VodProvider>();
        }

        var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var list = new List<VodProvider>();

        // JSONの形: { "results": { "JP": { "flatrate": [ {provider_id, provider_name, logo_path}, ... ] } } }
        if (doc.RootElement.TryGetProperty("results", out var results)
            && results.TryGetProperty(region, out var regionEl)
            && regionEl.TryGetProperty("flatrate", out var flatrate)
            && flatrate.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in flatrate.EnumerateArray())
            {
                list.Add(new VodProvider
                {
                    Id = p.TryGetProperty("provider_id", out var idEl) ? idEl.GetInt32() : 0,
                    Name = p.TryGetProperty("provider_name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty,
                    LogoPath = p.TryGetProperty("logo_path", out var logoEl) ? logoEl.GetString() : null
                });
            }
        }

        return list;
    }

    private VodProvidersResponse? TryLoadCache()
    {
        try
        {
            var path = CacheFilePath;
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<VodProvidersResponse>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CFilm: failed to read VOD cache; will rebuild.");
            return null;
        }
    }

    private void SaveCache(VodProvidersResponse response)
    {
        try
        {
            var json = JsonSerializer.Serialize(response);
            File.WriteAllText(CacheFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CFilm: failed to write VOD cache.");
        }
    }
}
