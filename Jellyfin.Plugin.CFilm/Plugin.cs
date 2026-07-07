using Jellyfin.Plugin.CFilm.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CFilm;

/// <summary>
/// CFilm プラグイン本体。Jellyfin の起動時に「1つだけ」作られます。
///
/// BasePlugin&lt;PluginConfiguration&gt; を継承する = 「設定を持つ標準的なプラグイン」。
/// IHasWebPages を実装する = 「ダッシュボードに設定画面を出せる」。
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// コンストラクタ。かっこ内の2つは Jellyfin が自動で渡してくれる部品(DI)。
    ///   applicationPaths: サーバーのフォルダ位置を知る部品
    ///   xmlSerializer   : 設定を XML で保存/読込する部品
    /// </summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        // 機能2で作るキャッシュ(vod-cache.json)の置き場: <サーバーのデータ領域>/cfilm
        CacheDirectory = Path.Combine(applicationPaths.DataPath, "cfilm");
        Directory.CreateDirectory(CacheDirectory);
    }

    /// <summary>プラグインの表示名。</summary>
    public override string Name => "CFilm";

    /// <summary>
    /// プラグインを一意に識別する ID(GUID)。世界で1つの値。
    /// build.yaml / meta.json の guid とも一致させる必要があります。
    /// </summary>
    public override Guid Id => Guid.Parse("596aa080-416e-46f0-805b-6d499f1cabd8");

    /// <summary>
    /// できたてのインスタンスを静的に保持。
    /// これで API 側から Plugin.Instance!.Configuration のように設定へ触れます。
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// 機能2のキャッシュ(vod-cache.json)を置くフォルダの絶対パス。
    /// </summary>
    public string CacheDirectory { get; }

    /// <summary>
    /// 設定画面のページ一覧を返す。今は configPage.html の1枚だけ。
    /// EmbeddedResourcePath は「DLL に埋め込んだ HTML の名前」。
    /// 名前は 名前空間 + フォルダ + ファイル名 をドットでつないだ形になります。
    /// </summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        };
    }
}
