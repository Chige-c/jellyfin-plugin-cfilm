using Jellyfin.Plugin.CFilm.Vod;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CFilm;

/// <summary>
/// DI(依存性注入)への「部品の登録」係。
/// Jellyfin 起動時に呼ばれ、ここで登録したものが後から自動で配られます。
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // VodProviderManager をサーバー全体で1個だけ(シングルトン)にする。
        // これで API と 定期タスクが同じ1個を共有し、キャッシュも一貫します。
        serviceCollection.AddSingleton<VodProviderManager>();
    }
}
