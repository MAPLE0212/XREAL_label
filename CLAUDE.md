# XREAL 3DoF ラベル配置アプリ

XREAL One＋Beam Pro で、頭の向いた方向にテキストラベルを置く Unity アプリ（3DoF）。このリポジトリは公開（GitHub）。

## フォルダ構成

```
XREAL_label/                 ← このリポジトリの一番上
  CLAUDE.md                  ← このファイル（公開）
  CLAUDE.local.md            ← 個人用の補足（git 管理外。あれば読む）
  README.md                  ← GitHub の入口。XrealLabelApp/README.md へ案内するだけ
  .gitignore
  com.xreal.xr.tar.gz        ← XREAL SDK 3.1.0（約250MB、git 管理外）
  research/                  ← 研究メモ（git 管理外。このリポジトリには入れない）
  XrealLabelApp/             ← Unity プロジェクト（Unity 6000.0.58f2）
    README.md                ← アプリの説明の本体（セットアップ・操作・仕様との対応・ログ・テスト）
    Assets/Scripts/          ← アプリのコード（XrealLabel.asmdef）
    Assets/Editor/           ← ProjectSetup.cs（シーン・XR・Player 設定）、AndroidBuild.cs（APK ビルド）
    Assets/Tests/            ← PlayMode テスト（LabelAppFlowTests.cs）
    Assets/Scenes/Main.unity ← 唯一のシーン
    Packages/manifest.json   ← com.xreal.xr は file:../../com.xreal.xr.tar.gz で参照
    ProjectSettings/
    Library/ Logs/ Builds/ UserSettings/ .utmp/   ← Unity が作るもの（git 管理外、編集しない）
```

スクリプトごとの役割は `XrealLabelApp/README.md` の「スクリプト構成」にある。

## 決まりごと

- ドキュメントとコメントは日本語。アプリの説明は `XrealLabelApp/README.md` にまとめ、一番上の `README.md` は案内だけにする
- 眼鏡のボタンの呼び方は XREAL 公式の英語名（X ボタン / +/- ボタン / Quick ボタン）にそろえる。対応は `ButtonNames.cs` と README の「ボタンの呼び方」
- SDK（`com.xreal.xr.tar.gz`）、実機ログ（`XrealLabelApp/Logs/`）、APK（`XrealLabelApp/Builds/`）、`research/` はコミットしない。公開リポジトリなので、push の前に `git status` で確認する
- `.cs` などを追加・削除したら、対応する `.meta` もそろえる

## ビルドとテスト

```bash
# APK（Builds/XrealLabel.apk）。ビルドターゲットが Android になっていること
"/Applications/Unity/Hub/Editor/6000.0.58f2/Unity.app/Contents/MacOS/Unity" -batchmode -quit -buildTarget Android -projectPath XrealLabelApp -executeMethod XrealLabel.EditorTools.AndroidBuild.Run
```

PlayMode テストのコマンドは `XrealLabelApp/README.md` の「エディタでの通し確認」を参照。結果は `XrealLabelApp/Logs/test-results.xml`、スクリーンショットは `XrealLabelApp/Logs/TestCaptures/`。
