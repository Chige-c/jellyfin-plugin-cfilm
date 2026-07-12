# 新バージョンのリリース手順（メンテナー向け）

CFilmは **GitHubリポジトリ経由の更新**（`manifest.json` + GitHub Releases）を正式な配布経路として使っている。
`gh` CLI が未導入のため、Release作成の1ステップだけWeb UIで手動作業になる。それ以外は全部コマンドで完結する。

これに従わずサーバーへ直接scp/unzipすると、`manifest.json`が指すバージョンと実際にサーバーに入っている
バージョンがズレて次回以降の判断を誤りやすい。**通常運用では手動設置(README.mdの3-2)は使わない。**

## 手順

1. **コードを変更してコミット**する（このコミットは後述のGitHub Releaseとは別。プラグイン本体の変更として先に確定させる）。

2. **バージョンを決めてビルド＆パッケージ化**（例: `1.2.4.0`。パッチ的な変更は末尾を+1、既存の番号体系に合わせる）
   ```powershell
   .\package.ps1 -Version 1.2.4.0
   ```
   `dist\CFilm_1.2.4.0\`（dll + meta.json）と `dist\CFilm_1.2.4.0.zip` ができる。

3. **meta.jsonのchangelogを実際の変更内容に書き換えて、zipを作り直す**
   （`package.ps1`はデフォルトで`"$Version - Initial release."`という仮の文言を書き込むだけなので、必ず手動修正が要る）
   ```powershell
   # dist\CFilm_1.2.4.0\meta.json の "changelog" を編集した後↓
   Compress-Archive -Path "dist\CFilm_1.2.4.0\*" -DestinationPath "dist\CFilm_1.2.4.0.zip" -Force
   ```

4. **リポジトリ直下の`manifest.json`に新バージョンを追記**（MD5チェックサムも自動計算される。手で書き写さない）
   ```powershell
   .\generate-manifest.ps1 -Version 1.2.4.0 -RepoOwner Chige-c -RepoName jellyfin-plugin-cfilm -Changelog "変更内容をここに書く"
   ```
   これで`manifest.json`の`versions`配列の先頭に新バージョンが追加される（既存バージョンは残る）。

5. **`manifest.json`をコミットしてGitHubへpush**
   ```powershell
   git add manifest.json
   git commit -m "Add v1.2.4.0 to repository manifest"
   git push origin main
   ```
   （手順1のコード変更コミットと合わせて2つのコミットがpushされる形でよい）

6. **GitHub Releaseを作成**（`gh` CLI未導入のため、ここだけWebブラウザで手動）
   - まず https://github.com/Chige-c/jellyfin-plugin-cfilm/releases を開いて、
     同じバージョン名の**ドラフトや残骸が無いか先に確認する**（過去に「v1.2.3.0 is used by another
     release」というエラーに遭遇した原因は、以前作りかけた未公開ドラフトが残っていたこと。
     見つかったら中身を確認して削除するか、それを完成させて使う）。
   - https://github.com/Chige-c/jellyfin-plugin-cfilm/releases/new を開く。
   - **タグ**: `v1.2.4.0`（新規タグとして入力。対象ブランチは`main`のままでよい。タグは
     Release公開と同時に自動作成されるので、事前に`git tag`する必要は無い）。
   - **タイトル**: `v1.2.4.0`
   - **説明**: 手順3で書いたchangelogをそのまま貼る。
   - **Assets**: `dist\CFilm_1.2.4.0.zip` をドラッグ&ドロップでアップロード。
   - 「Publish release」。

7. **配布物が正しいか確認する**
   ```bash
   # ダウンロードURLが200を返すか
   curl -s -L -o /dev/null -w "%{http_code}\n" https://github.com/Chige-c/jellyfin-plugin-cfilm/releases/download/v1.2.4.0/CFilm_1.2.4.0.zip

   # manifest.jsonが公開されているか（pushしてから数分キャッシュが残ることがある）
   curl -s https://raw.githubusercontent.com/Chige-c/jellyfin-plugin-cfilm/main/manifest.json | grep -A2 '"version":  "1.2.4.0"'
   ```
   不安な場合は、ローカルzipのMD5/SHA256とGitHub Release画面に表示されるハッシュを突き合わせて一致を確認する
   （PowerShell: `(Get-FileHash "dist\CFilm_1.2.4.0.zip" -Algorithm SHA256).Hash.ToLower()`）。

8. **Jellyfin側で更新を反映**
   - ダッシュボード → プラグイン → カタログ を開き、CFilmの「更新」ボタンをクリック。
   - **カタログの内容はJellyfin起動時にしか再取得されないことがある。** 反映されない場合は
     `sudo systemctl restart jellyfin` してからカタログを開き直す。
   - 更新後、そのバージョンで追加/変更したエンドポイントを直接叩いて確認する
     （例: `curl http://localhost:<port>/Plugins/CFilm/ConnectInfo`）。

## つまずきやすいポイント

- **`405 Method Not Allowed` + `Allow: DELETE`が返る** → 自作エンドポイントの応答ではなく、
  Jellyfin本体の汎用ルート（`DELETE /Plugins/{pluginId}/{version}`、プラグインのアンインストール用）が
  パスの形だけ偶然一致して代わりに答えているサイン。**新バージョンがまだロードされていない**ことの
  ほぼ確定的な証拠なので、手順8のカタログ更新・再起動からやり直す。
- **プラグインの実フォルダの場所を勘で決めない** → `/var/lib/jellyfin/plugins`はデフォルト値でしかなく、
  `systemctl status jellyfin`の`Drop-In`欄にデータディレクトリを上書きする設定
  （例: `override-datadir.conf`）が無いか必ず確認する。上書きされていれば、実際の場所は
  そのconfファイルの中に書いてある。詳細は[README.md](README.md)の3-2。
- **`generate-manifest.ps1`をスキップして`manifest.json`を手で編集しない** → PowerShell 5.1の
  `ConvertTo-Json`は要素1個の配列を渡すと外側の`[]`を自動で外してしまう罠があり、スクリプト側で
  対策済み。手編集だとこの罠を踏みやすいうえチェックサム計算もずれやすい。
