# CFilm — Jellyfin プラグイン

C-film アプリ向けの独自機能を **Jellyfin サーバー本体** に追加するプラグインです。
アプリは Jellyfin の URL さえ知っていれば、これらの機能を Jellyfin の API 経由で取得できます
（中継サーバー chidahub が不要になります）。

- **機能1: ちだのおすすめ** — 設定画面で作品を検索し、順序付きで選んだレコメンド行を API で公開。
- **機能2: VOD識別** — 各作品の配信サービス(Netflix 等)を TMDB(日本 / 定額見放題 flatrate)で判定し、
  ライブラリ全体の一括対応表を API で公開。

対象サーバー: **Jellyfin 10.11.x**（`.NET 9` / `targetAbi 10.11.11.0` でビルド）

---

## 1. ビルド

```powershell
dotnet build .\Jellyfin.Plugin.CFilm\Jellyfin.Plugin.CFilm.csproj -c Release
```

出力: `Jellyfin.Plugin.CFilm\bin\Release\net9.0\Jellyfin.Plugin.CFilm.dll`

## 2. パッケージ化（設置できる形にする）

```powershell
.\package.ps1
```

`dist\CFilm_1.0.0.0\`（`Jellyfin.Plugin.CFilm.dll` と `meta.json`）と、
転送用の `dist\CFilm_1.0.0.0.zip` ができます。

- `meta.json` = Jellyfin がプラグインを認識するための「名札」。これが無いと一覧に出ません。

## 3. サーバーへ設置（Ubuntu 直インストールの Jellyfin）

Jellyfin のプラグインフォルダは通常 `/var/lib/jellyfin/plugins` です。

```bash
# 例: Windows から zip を転送 → サーバー上で展開
scp dist/CFilm_1.0.0.0.zip user@192.168.0.11:/tmp/

# サーバー側
sudo mkdir -p /var/lib/jellyfin/plugins/CFilm_1.0.0.0
sudo unzip /tmp/CFilm_1.0.0.0.zip -d /var/lib/jellyfin/plugins/CFilm_1.0.0.0
sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/CFilm_1.0.0.0
sudo systemctl restart jellyfin
```

> パスが違う場合: ダッシュボード → 管理 → 全般 や、`/etc/jellyfin` の設定で
> データフォルダを確認してください。プラグインは「データフォルダ/plugins」の下です。

設置後、**ダッシュボード → プラグイン → CFilm** が表示されれば成功です。

## 4. 使い方（設定画面）

**ダッシュボード → プラグイン → CFilm** を開く。

- **ちだのおすすめ**
  1. 「行の名前」を入力（例: ちだのおすすめ）。
  2. 検索欄に作品名を入れ、候補の「＋ 追加」。
  3. 「選択中」で ▲▼ で並べ替え、✕ で削除。**上から順**が表示順。
  4. 一番下の「保存」。
- **VOD識別**
  1. **TMDB APIキー**（themoviedb.org → アカウント設定 → API の v3 キー）を入力。
  2. 「🔄 今すぐスキャン」→ 保存してライブラリ全体を走査（作品数により数分）。
  3. 以降は **毎日 午前4時** に自動更新（ダッシュボード → 予約タスク → CFilm でも実行可）。

---

## 5. API 仕様（アプリ連携）

すべて Jellyfin 標準の認証を使います。アプリは Jellyfin にログイン済みのトークンを、
通常のリクエストと同じヘッダで付けてください。

```
Authorization: MediaBrowser Token="<アクセストークン>"
```

### 5-1. GET /Plugins/CFilm/Recommendations

- 認証: ログイン済みユーザーなら誰でも（`[Authorize]`）。
- レスポンス例:

```json
{
  "name": "ちだのおすすめ",
  "itemIds": ["a1b2c3d4e5f6...", "0f1e2d3c4b5a..."]
}
```

- `itemIds` は **順序付き**（設定画面の並び順）。ID は Jellyfin 標準の 32桁ハイフン無し形式。
- 作品の詳細は標準 API で取得:
  `GET /Items?Ids=<itemIdsをカンマ区切り>&Fields=...`（順序は itemIds 側で保持）。

### 5-2. GET /Plugins/CFilm/VodProviders

- 認証: ログイン済みユーザーなら誰でも（`[Authorize]`）。
- 動作: キャッシュがあれば即返す。無ければその場で1回だけ構築（初回のみ時間がかかる）。
- レスポンス例:

```json
{
  "region": "JP",
  "generatedAt": "2026-07-07T04:00:00.0000000Z",
  "count": 123,
  "providers": {
    "a1b2c3d4e5f6...": [
      { "id": 8,   "name": "Netflix",             "logoPath": "/pbpMk2JmcoNnQwx5JGpXngfoWtp.jpg" },
      { "id": 119, "name": "Amazon Prime Video",  "logoPath": "/68MNrwlkpF7WnmNPXLah69CR5cb.jpg" }
    ]
  }
}
```

- `providers` のキー = 作品ID（Recommendations の itemIds と同じ形式）。
- 各配信サービスは `id`(数値) と `name`(文字列) を**両方**持つ
  （数値で判定しても、名前で表示しても困らないように）。
- `logoPath` は TMDB 基準の相対パス。実画像は
  `https://image.tmdb.org/t/p/original{logoPath}` で取得できます。

### 5-3. POST /Plugins/CFilm/VodProviders/Rebuild （管理者のみ）

- 認証: 管理者（`[Authorize(RequiresElevation)]`）。
- 動作: 一括対応表を**強制的に作り直して**返す（設定画面の「今すぐスキャン」が使用）。
- レスポンス形式は 5-2 と同じ。

---

## 6. トラブルシュート

- **プラグイン一覧に出ない** → `meta.json` があるか、フォルダ所有者が `jellyfin` か、
  サーバーを再起動したかを確認。サーバーログ(`/var/log/jellyfin`)に読み込みエラーが出ます。
- **VodProviders が空** → TMDB APIキー未設定 / 作品に TMDB ID が無い / 日本(JP)に配信が無い。
  ダッシュボードのログに `CFilm:` で始まる行が出ます。
- **バージョン不一致で読み込めない** → サーバーが 10.11 系か確認。別バージョンなら
  `.csproj` の `Jellyfin.Controller` バージョンと `meta.json` の `targetAbi` を合わせて再ビルド。

## 7. ファイル構成

```
Jellyfin.Plugin.CFilm/
  Plugin.cs                         … プラグイン本体(Id/名前/設定ページ/キャッシュ場所)
  PluginServiceRegistrator.cs       … DIへサービス登録(VodProviderManager)
  Configuration/
    PluginConfiguration.cs          … 設定の入れ物(行名/ID配列/TMDBキー/地域)
    configPage.html                 … 設定画面(検索・並べ替え・スキャン)
  Api/
    RecommendationsController.cs     … GET /Plugins/CFilm/Recommendations
  Vod/
    VodModels.cs                     … 返却JSONの形(VodProvider / VodProvidersResponse)
    VodProviderManager.cs            … 走査+TMDB取得+キャッシュ(頭脳)
    VodProvidersController.cs        … GET/POST の VOD API
    VodScanScheduledTask.cs          … 毎日4時の自動スキャン
```
