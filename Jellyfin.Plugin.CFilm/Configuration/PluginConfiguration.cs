using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CFilm.Configuration;

/// <summary>
/// プラグインの設定。
/// BasePluginConfiguration を継承すると、Jellyfin がこの中身を
/// 自動で XML ファイルに保存・読込してくれます(自分でファイル操作しなくてよい)。
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// コンストラクタで「既定値(初期値)」を入れておきます。
    /// 初回起動時はこの値が使われます。
    /// </summary>
    public PluginConfiguration()
    {
        RecommendationRowName = "ちだのおすすめ";
        RecommendationItemIds = Array.Empty<string>();
        TmdbApiKey = string.Empty;
        TmdbRegion = "JP";
        VodLibraryIds = Array.Empty<string>();
    }

    /// <summary>
    /// 【機能1】おすすめ行の表示名。例: "ちだのおすすめ"。
    /// </summary>
    public string RecommendationRowName { get; set; }

    /// <summary>
    /// 【機能1】おすすめに出す作品IDの「順序付き」リスト。
    /// 配列(順番あり)なので、並べ替えがそのまま表示順になります。
    /// </summary>
    public string[] RecommendationItemIds { get; set; }

    /// <summary>
    /// 【機能2】TMDB の APIキー。配信元(Netflix等)を調べるのに使います。
    /// </summary>
    public string TmdbApiKey { get; set; }

    /// <summary>
    /// 【機能2】配信元を調べる地域コード。日本は "JP"(既定)。
    /// </summary>
    public string TmdbRegion { get; set; }

    /// <summary>
    /// 【機能2】スキャン対象として選んだライブラリのID一覧(Jellyfinの"仮想フォルダ"ItemId)。
    /// 空の場合はスキャンを実行しない(=誤ってライブラリ全体を舐めるのを防ぐ安全策)。
    /// </summary>
    public string[] VodLibraryIds { get; set; }
}
