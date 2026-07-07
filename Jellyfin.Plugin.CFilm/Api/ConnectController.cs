using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.CFilm.Api;

/// <summary>
/// C-film アプリの「ワンタップ接続」用エンドポイント。
///
/// [AllowAnonymous] … このプラグインの他の API は [Authorize](ログイン済み必須)だが、
///   ここはログイン前のユーザーがタップして使うページなので認証不要にする。
///
/// このエンドポイントにアクセスされた「自分自身のURL」を使って
/// cfilm://connect?server=https://&lt;このサーバーのURL&gt; へブラウザ側でリダイレクトさせる
/// HTML を返す。Android の App Links / iOS の Universal Links(https のまま自動起動する仕組み)は
/// アプリのビルド時に対象ドメインを1つずつ事前登録する必要があり、複数のサーバー管理者が
/// それぞれ別ドメインで使うことができない。カスタムURLスキーム(cfilm://)は事前登録が不要なため、
/// CFilmプラグインが入っている Jellyfin サーバーであれば、ドメインを問わずこのエンドポイントだけで
/// 同じ「タップでアプリ起動」が実現できる。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("Plugins/CFilm")]
public class ConnectController : ControllerBase
{
    /// <summary>
    /// GET /Plugins/CFilm/Connect
    /// リバースプロキシ配下でも正しいホスト名を取るため、
    /// X-Forwarded-Proto (標準名) と X-Forwarded-Protocol (Jellyfin界隈でよく使われる表記)の
    /// 両方を見る(無ければ Request から取得)。X-Forwarded-Host も同様。
    /// </summary>
    [HttpGet("Connect")]
    public ContentResult GetConnect()
    {
        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault()
            ?? Request.Headers["X-Forwarded-Protocol"].FirstOrDefault();
        var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();

        var scheme = string.IsNullOrEmpty(forwardedProto) ? Request.Scheme : forwardedProto;
        var host = string.IsNullOrEmpty(forwardedHost) ? Request.Host.ToString() : forwardedHost;

        var serverUrl = $"{scheme}://{host}";
        var jellyseerrUrl = Plugin.Instance!.Configuration.JellyseerrUrl ?? string.Empty;

        return new ContentResult
        {
            Content = BuildHtml(serverUrl, jellyseerrUrl),
            ContentType = "text/html; charset=utf-8",
            StatusCode = 200
        };
    }

    private static string BuildHtml(string serverUrl, string jellyseerrUrl)
    {
        // JSON文字列化してJSリテラルとして埋め込む(XSS対策。ホスト名を直接HTMLへ差し込まない)。
        var serverUrlJson = JsonSerializer.Serialize(serverUrl);
        // 未設定なら "" になり、JS側の if (jellyseerrUrl) で自然に無視される。
        var jellyseerrUrlJson = JsonSerializer.Serialize(jellyseerrUrl);

        return $$"""
        <!doctype html>
        <html lang="ja">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
        <title>C-film に接続</title>
        <style>
          html, body { height: 100%; margin: 0; }
          body {
            background: #000; color: #fff;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
            display: flex; align-items: center; justify-content: center;
            padding: 24px; box-sizing: border-box;
          }
          .card { max-width: 420px; text-align: center; }
          .spinner {
            width: 36px; height: 36px; margin: 0 auto 20px;
            border: 3px solid rgba(255,255,255,0.2);
            border-top-color: #fff; border-radius: 50%;
            animation: spin 0.8s linear infinite;
          }
          @keyframes spin { to { transform: rotate(360deg); } }
          h1 { font-size: 18px; font-weight: 600; margin: 0 0 8px; }
          p { font-size: 14px; color: #aaa; line-height: 1.6; margin: 0 0 24px; }
          .btn {
            display: inline-block; width: 100%; box-sizing: border-box;
            padding: 14px; border-radius: 12px; background: #fff; color: #000;
            font-weight: 700; font-size: 15px; text-decoration: none; margin-bottom: 12px;
          }
          .btn.secondary { background: transparent; color: #fff; border: 1.5px solid #444; }
          #fallback { display: none; }
        </style>
        </head>
        <body>
          <div class="card">
            <div id="loading">
              <div class="spinner"></div>
              <h1>C-film アプリを起動しています…</h1>
              <p>アプリが開かない場合は自動的にインストール案内を表示します。</p>
            </div>
            <div id="fallback">
              <h1>C-film アプリが必要です</h1>
              <p>このサーバーに接続するには C-film アプリのインストールが必要です。インストール後にこのページをもう一度開くと、サーバーが自動設定されます。</p>
              <a class="btn" id="store-link" href="#">ストアでインストール</a>
              <a class="btn secondary" id="retry-link" href="#">もう一度アプリを開く</a>
            </div>
          </div>
          <script>
            (function () {
              var serverUrl = {{serverUrlJson}};
              var jellyseerrUrl = {{jellyseerrUrlJson}};
              var customUrl = 'cfilm://connect?server=' + encodeURIComponent(serverUrl);
              if (jellyseerrUrl) {
                customUrl += '&jellyseerr=' + encodeURIComponent(jellyseerrUrl);
              }

              var isIOS = /iPhone|iPad|iPod/i.test(navigator.userAgent);
              var storeUrl = isIOS
                ? 'https://cfilm.app/get-ios'
                : 'https://cfilm.app/get-android';
              document.getElementById('store-link').href = storeUrl;
              document.getElementById('retry-link').href = customUrl;

              var appOpened = false;
              document.addEventListener('visibilitychange', function () {
                if (document.hidden) appOpened = true;
              });
              window.addEventListener('pagehide', function () { appOpened = true; });

              window.location.href = customUrl;

              setTimeout(function () {
                if (!appOpened) {
                  document.getElementById('loading').style.display = 'none';
                  document.getElementById('fallback').style.display = 'block';
                }
              }, 1500);
            })();
          </script>
        </body>
        </html>
        """;
    }
}
