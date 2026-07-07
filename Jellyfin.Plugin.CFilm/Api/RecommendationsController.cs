using System.Net.Mime;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CFilm.Api;

/// <summary>
/// 「ちだのおすすめ」を配る API。
///
/// 属性(attribute, [ ]で書く目印)の意味:
///   [ApiController] … これは Web API のコントローラですという宣言
///   [Authorize]     … 有効な Jellyfin トークンが必須(=ログイン済みなら誰でも)
///   [Route(...)]    … このクラスの共通 URL 接頭辞
///   [Produces(...)] … 返すのは JSON
/// </summary>
[ApiController]
[Authorize]
[Route("Plugins/CFilm")]
[Produces(MediaTypeNames.Application.Json)]
public class RecommendationsController : ControllerBase
{
    /// <summary>
    /// GET /Plugins/CFilm/Recommendations
    /// 設定に保存された行名と作品ID(順序付き)を、そのまま返します。
    /// </summary>
    [HttpGet("Recommendations")]
    public ActionResult<RecommendationsResponse> GetRecommendations()
    {
        // Plugin.Instance は起動時に自分を入れておいた静的プロパティ。
        // "!" は「null ではないと約束する」印(Nullable を有効にしているため必要)。
        var config = Plugin.Instance!.Configuration;

        return new RecommendationsResponse
        {
            Name = config.RecommendationRowName,
            ItemIds = config.RecommendationItemIds
        };
    }
}

/// <summary>
/// 返す JSON の形。
/// [JsonPropertyName] でキー名を厳密に固定 → Jellyfin 全体の命名規則に
/// 左右されず、必ず "name" / "itemIds" という小文字始まりで出力されます。
/// </summary>
public class RecommendationsResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("itemIds")]
    public string[] ItemIds { get; set; } = System.Array.Empty<string>();
}
