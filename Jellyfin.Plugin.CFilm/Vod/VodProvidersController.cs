using System.Net.Mime;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CFilm.Vod;

/// <summary>
/// 機能2「VOD識別」を配る API。
/// </summary>
[ApiController]
[Authorize]
[Route("Plugins/CFilm")]
[Produces(MediaTypeNames.Application.Json)]
public class VodProvidersController : ControllerBase
{
    private readonly VodProviderManager _manager;

    /// <summary>コンストラクタ。VodProviderManager は DI で渡されます。</summary>
    public VodProvidersController(VodProviderManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// GET /Plugins/CFilm/VodProviders
    /// ライブラリ全体の一括対応表を返す(キャッシュがあれば即・無ければ構築)。
    /// ログイン済みなら誰でも呼べます([Authorize])。
    /// </summary>
    [HttpGet("VodProviders")]
    public async Task<ActionResult<VodProvidersResponse>> GetVodProviders(CancellationToken cancellationToken)
    {
        return await _manager.GetOrBuildAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POST /Plugins/CFilm/VodProviders/Rebuild
    /// 強制的に作り直す(あなたの言う ?force に相当)。
    /// 重い処理なので「管理者のみ」に限定([Authorize(RequiresElevation)])。
    /// </summary>
    [Authorize(Policy = Policies.RequiresElevation)]
    [HttpPost("VodProviders/Rebuild")]
    public async Task<ActionResult<VodProvidersResponse>> RebuildVodProviders(CancellationToken cancellationToken)
    {
        return await _manager.RebuildAsync(cancellationToken).ConfigureAwait(false);
    }
}
