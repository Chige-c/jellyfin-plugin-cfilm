using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.CFilm.Vod;

/// <summary>
/// 1つの配信サービス(プロバイダー)を表す。
/// id(数値) と name(文字列) を「両方」返すのが肝。
/// こうしておくと、アプリ側が「数値IDで判定」でも「名前で表示」でも困りません
/// (＝『数値ID vs 文字列ラベル』の取り違えを最初から防ぐ)。
/// </summary>
public class VodProvider
{
    /// <summary>TMDB のプロバイダーID。例: Netflix=8。</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>プロバイダー名。例: "Netflix"。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>ロゴ画像のパス(TMDB基準の相対パス)。無い場合は null。</summary>
    [JsonPropertyName("logoPath")]
    public string? LogoPath { get; set; }
}

/// <summary>
/// 1作品分の結果。どのライブラリの作品か(libraryId)を一緒に持つのが肝。
/// アプリ側はこれを見て「このライブラリの作品はVOD識別済みだから通常表示では隠す」
/// といった判断ができます。
/// </summary>
public class VodItemEntry
{
    /// <summary>この作品が属するJellyfinライブラリのID(仮想フォルダのItemId)。</summary>
    [JsonPropertyName("libraryId")]
    public string LibraryId { get; set; } = string.Empty;

    /// <summary>配信サービスの配列。</summary>
    [JsonPropertyName("providers")]
    public List<VodProvider> Providers { get; set; } = new();
}

/// <summary>
/// GET /Plugins/CFilm/VodProviders が返す「一括対応表」。
/// items が本体で、キー=作品ID(32桁ハイフン無し)、値=libraryId+配信サービス配列。
/// region / generatedAt / count / libraryIds は付随情報(自己説明のため)。
/// </summary>
public class VodProvidersResponse
{
    /// <summary>調べた地域。既定 "JP"。</summary>
    [JsonPropertyName("region")]
    public string Region { get; set; } = "JP";

    /// <summary>この対応表を作った日時(UTC, ISO 8601)。</summary>
    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = string.Empty;

    /// <summary>配信元が見つかった作品数。</summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>スキャン対象として選ばれたライブラリID一覧(設定画面での選択そのまま)。</summary>
    [JsonPropertyName("libraryIds")]
    public List<string> LibraryIds { get; set; } = new();

    /// <summary>作品ID → (libraryId, 配信サービス配列) の対応表。</summary>
    [JsonPropertyName("items")]
    public Dictionary<string, VodItemEntry> Items { get; set; } = new();
}
