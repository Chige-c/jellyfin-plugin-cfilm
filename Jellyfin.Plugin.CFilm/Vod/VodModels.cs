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
/// GET /Plugins/CFilm/VodProviders が返す「一括対応表」。
/// providers が本体で、キー=作品ID(32桁ハイフン無し)、値=配信サービスの配列。
/// region / generatedAt / count は付随情報(自己説明のため)。
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

    /// <summary>作品ID → 配信サービス配列 の対応表。</summary>
    [JsonPropertyName("providers")]
    public Dictionary<string, List<VodProvider>> Providers { get; set; } = new();
}
