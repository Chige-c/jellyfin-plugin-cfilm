# CFilm — Jellyfin プラグイン

C-film アプリ向けの独自機能を **Jellyfin サーバー本体** に追加するプラグインです。
アプリは Jellyfin の URL さえ知っていれば、これらの機能を Jellyfin の API 経由で取得できます
（中継サーバー chidahub が不要になります）。

- **機能1: ちだのおすすめ** — 設定画面で作品を検索し、順序付きで選んだレコメンド行を API で公開。
- **機能2: VOD識別** — 選んだライブラリの中の各作品について、配信サービス(Netflix 等)を
  TMDB(日本 / 定額見放題 flatrate)で判定し、一括対応表(どのライブラリの作品かも含む)を API で公開。

対象サーバー: **Jellyfin 10.11.x**（`.NET 9` / `targetAbi 10.11.11.0` でビルド）

> 新バージョンをリリースする手順（ビルド→パッケージ化→GitHub Release作成→配信）は
> [RELEASING.md](RELEASING.md) を参照。

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

## 3. サーバーへ設置

### 3-1. 通常の更新・設置（推奨: GitHubリポジトリ経由）

このリポジトリは Jellyfin の「リポジトリ」機能に対応した `manifest.json` を配布している。
**初回に一度だけ**登録すれば、以降の更新はダッシュボードのワンクリックで完了する
（scp・ssh・systemctlはすべて不要）。

ダッシュボード → プラグイン → リポジトリ → 「+」で以下のURLを追加:

```
https://raw.githubusercontent.com/Chige-c/jellyfin-plugin-cfilm/main/manifest.json
```

登録後、ダッシュボード → プラグイン → カタログ に CFilm が表示され、新バージョンがあれば
「更新」ボタンが出る。クリックするだけで正しい場所に自動配置される。

> **更新ボタンが出ない/押しても反映されない場合**: プラグインのカタログ一覧は
> **Jellyfin起動時にしか再取得されないことがある**。`sudo systemctl restart jellyfin` してから
> カタログを開き直す。それでも反映されない場合の切り分けは 3-2 末尾の「つまずきやすいポイント」を参照。

メンテナー自身が新バージョンをリリースする手順は [RELEASING.md](RELEASING.md) にまとめてある。

### 3-2. 手動設置（リポジトリを使わない場合・トラブルシュート用）

Jellyfin のプラグインフォルダは**デフォルトでは** `/var/lib/jellyfin/plugins` だが、これは
あくまでデフォルト値。**サーバーによってはデータディレクトリがカスタム設定されており、
実際のプラグインフォルダが別の場所にあることがある**（実例: あるサーバーでは
`/mnt/ssd128GB/jellyfin/lib/plugins` だった）。まず以下で上書き設定の有無を確認する。

```bash
systemctl status jellyfin --no-pager | grep -i drop-in -A2
# Drop-In欄にファイルが出てきたら中身を確認（例: override-datadir.conf 等の名前が典型）
cat /etc/systemd/system/jellyfin.service.d/*.conf
```

実際のパスが分かったら設置する（`<PLUGINS_DIR>` は上で確認した実パスに置き換える）:

```bash
# 例: Windows から zip を転送 → サーバー上で展開
scp dist/CFilm_1.0.0.0.zip user@192.168.0.11:/tmp/

# サーバー側
sudo mkdir -p <PLUGINS_DIR>/CFilm_1.0.0.0
sudo unzip /tmp/CFilm_1.0.0.0.zip -d <PLUGINS_DIR>/CFilm_1.0.0.0
sudo chown -R jellyfin:jellyfin <PLUGINS_DIR>/CFilm_1.0.0.0
sudo rm -rf <PLUGINS_DIR>/CFilm_<古いバージョン>   # 古いバージョンと共存させると読み込みが混乱しうる
sudo systemctl restart jellyfin
```

設置後、**ダッシュボード → プラグイン → CFilm** が表示され、バージョン番号が正しければ成功。
確実に確認したい場合は、そのバージョンで追加/変更したエンドポイントを直接叩くのが一番早い
（例: `curl http://localhost:<port>/Plugins/CFilm/ConnectInfo`）。

> **つまずきやすいポイント**: 更新したはずなのに古い挙動のままの時、
> ```bash
> curl -s -D - -o /dev/null http://localhost:<port>/Plugins/CFilm/<新しく追加したパス>
> ```
> で**`405 Method Not Allowed` + `Allow: DELETE`** が返ってきたら要注意。これは自作エンドポイントの
> 応答ではなく、**Jellyfin本体の汎用ルート（`DELETE /Plugins/{pluginId}/{version}`、プラグインの
> アンインストール用）が、パスの形（セグメント数）だけ偶然一致して代わりに答えてしまっている**サイン。
> 新バージョンがまだロードされていないことのほぼ確定的な証拠なので、上のパス確認からやり直す。

## 4. 使い方（設定画面）

**ダッシュボード → プラグイン → CFilm** を開く。

- **ちだのおすすめ**
  1. 「行の名前」を入力（例: ちだのおすすめ）。
  2. 検索欄に作品名を入れ、候補の「＋ 追加」。
  3. 「選択中」で ▲▼ で並べ替え、✕ で削除。**上から順**が表示順。
  4. 一番下の「保存」。
- **VOD識別**
  1. **① 対象ライブラリを選ぶ**（チェックボックス、複数選択可）。**何も選ばないとスキャンされません**。
  2. **② TMDB APIキー**（themoviedb.org → アカウント設定 → API の v3 キー）を入力。
  3. **③ 「🔄 今すぐスキャン」** → 設定を保存して、選んだライブラリだけを走査（作品数により数分）。
  4. 以降は **毎日 午前4時** に、保存済みのライブラリ選択で自動更新
     （ダッシュボード → 予約タスク → CFilm でも手動実行可）。

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
  設定画面でライブラリが1つも選ばれていない場合、`items` は空のまま返る。
- レスポンス例:

```json
{
  "region": "JP",
  "generatedAt": "2026-07-07T04:00:00.0000000Z",
  "count": 123,
  "libraryIds": ["3f2e1d...", "9a8b7c..."],
  "items": {
    "a1b2c3d4e5f6...": {
      "libraryId": "3f2e1d...",
      "providers": [
        { "id": 8,   "name": "Netflix",             "logoPath": "/pbpMk2JmcoNnQwx5JGpXngfoWtp.jpg" },
        { "id": 119, "name": "Amazon Prime Video",  "logoPath": "/68MNrwlkpF7WnmNPXLah69CR5cb.jpg" }
      ]
    }
  }
}
```

- `items` のキー = 作品ID（Recommendations の itemIds と同じ形式）。
- `items[itemId].libraryId` = その作品が属する Jellyfin ライブラリのID（`/Library/VirtualFolders` の `ItemId` と同じ値）。
  **アプリはこれを使って、VOD識別済みライブラリを通常表示から除外するなどの絞り込みができる。**
- `libraryIds` = 設定画面でスキャン対象として選ばれているライブラリID一覧（`items` に実際に出てくる `libraryId` の元ネタ）。
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

## 7. ワンタップ接続（任意）

C-film アプリの「サーバーURLをタップ1つで自動入力」機能です。CFilmプラグインが
入っているサーバーなら**追加のセットアップ無しで**使えます（プラグイン本体が
`GET /Plugins/CFilm/Connect` エンドポイントとして提供するため）。

### 使い方

1. あなたの Jellyfin サーバーに、**Web UI を遮断した「バックエンド専用URL」**を
   用意する（リバースプロキシで `/` と `/web/` を 403 にする。設定例は
   お使いのリバースプロキシのドキュメントを参照）。
2. そのバックエンド専用URL（例: `https://backend.example.com`）を、
   そのまま LINE / Discord などで共有する。
3. タップすると `https://backend.example.com` が一瞬開き、CFilmプラグインの
   `Connect` エンドポイントが返す中継用ページが `cfilm://connect?server=...`
   へ即座にリダイレクトし、C-film アプリが起動する。
   アプリのサーバーURL入力欄には、タップされた URL 自身が自動入力される。

### 仕組み

Android の App Links / iOS の Universal Links（`https://` のまま自動起動する
仕組み）は、対象ドメインを**アプリのビルド時に1つだけ事前登録**する必要があり、
サーバー管理者ごとに異なるドメインには対応できません。CFilmプラグインの
`Connect` エンドポイントは、事前登録が不要な**カスタムURLスキーム
(`cfilm://`)へブラウザ側でリダイレクトする軽量なページ**を返すことで、
この制約を回避しています。アクセスされた自分自身のホスト名
（`X-Forwarded-Host` / `Host` ヘッダー）をそのまま `server=` パラメータに使うため、
**プラグインが入っているサーバーであれば、ドメインを問わず同じ仕組みで動きます**
（外部の中継サーバーやアプリの再ビルドは不要）。

## 8. ファイル構成

```
Jellyfin.Plugin.CFilm/
  Plugin.cs                         … プラグイン本体(Id/名前/設定ページ/キャッシュ場所)
  PluginServiceRegistrator.cs       … DIへサービス登録(VodProviderManager)
  Configuration/
    PluginConfiguration.cs          … 設定の入れ物(行名/ID配列/TMDBキー/地域/対象ライブラリID)
    configPage.html                 … 設定画面(検索・並べ替え・ライブラリ選択・スキャン)
  Api/
    RecommendationsController.cs     … GET /Plugins/CFilm/Recommendations
    ConnectController.cs             … GET /Plugins/CFilm/Connect (ワンタップ接続)
                                        GET /Plugins/CFilm/ConnectInfo (JSON版、手動/保存サーバー接続用)
  Vod/
    VodModels.cs                     … 返却JSONの形(VodProvider / VodProvidersResponse)
    VodProviderManager.cs            … 走査+TMDB取得+キャッシュ(頭脳)
    VodProvidersController.cs        … GET/POST の VOD API
    VodScanScheduledTask.cs          … 毎日4時の自動スキャン
```
