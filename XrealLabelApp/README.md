# XREAL 3DoF ラベル配置アプリ

仕様書「XREAL 3DoFラベル配置アプリ 仕様書」（2026-10-19）の v1 実装。
XREAL One＋Beam Pro で、頭の向いた方向にテキストラベルを置いていく Unity アプリ（3DoF・その場から動かない前提）。

## 環境

| 項目 | 内容 |
|---|---|
| Unity | 6000.0.58f2 |
| XREAL SDK | `com.xreal.xr` 3.1.0（リポジトリには含めていない。下の「初回セットアップ」を参照） |
| 主なパッケージ | Input System 1.14 / uGUI 2.0 / XR Plugin Management 4.5 / XR Core Utils 2.5 |
| ビルド先 | Android（Beam Pro）。API 29+ / IL2CPP / ARM64 / OpenGLES3 / 縦画面 |

> Android へのビルドには、Unity Hub で 6000.0.58f2 に **Android Build Support**（OpenJDK・SDK & NDK を含む）を追加する必要があります。

## 初回セットアップ

1. XREAL SDK 3.1.0 の `com.xreal.xr.tar.gz` を入手し、リポジトリの一番上（`XrealLabelApp` と同じ階層）に置く。入手先は XREAL 公式の [リリースノート](https://docs.xreal.com/Release%20Note/XREAL%20SDK%203.1.0)。SDK は約 250MB あるのでリポジトリには入れていない
   ```
   <リポジトリ>/
     com.xreal.xr.tar.gz   ← ここに置く（Packages/manifest.json が file:../../com.xreal.xr.tar.gz で参照）
     XrealLabelApp/
   ```
2. Unity Hub で `XrealLabelApp` フォルダを開く（初回は `Library/` の生成に時間がかかる）
3. 必要なら、メニュー **XREAL Label → Setup Project** を実行する。`Assets/Scenes/Main.unity` と設定はリポジトリに入っているので、普通は不要
   - `Assets/Scenes/Main.unity` を作成（XR Origin＋LabelApp）
   - XR Plug-in Management：Android で XREAL を有効化、トラッキングを 3DoF に設定
   - Android の Player 設定を XREAL SDK の要件に合わせる
4. File → Build Profiles で Android に切り替え、メニュー **XREAL Label → Build Android APK** を実行する。`Builds/XrealLabel.apk` ができる
5. Beam Pro に入れる：
   ```bash
   adb install -r Builds/XrealLabel.apk
   ```

## 使い方（操作）

| 操作 | 眼鏡 / Beam Pro | エディタ |
|---|---|---|
| 確定（開始・原点設定・配置） | 眼鏡の Xボタン（クリック）、Beam Pro 画面の青いボタン、Beam Pro の仮想コントローラーの Trigger | Space / 青いボタン |
| 頭を回す | 頭を回す | 右ドラッグ / 矢印キー |
| 文字入力 | 画面のソフトキーボード、または候補ボタン | キーボード入力＋Enter |
| 編集・削除 | ラベルを見ている（ホバー）ときに画面のボタン | 同じ |
| 全消去・リセンター | 画面のボタン（確認ダイアログあり） | 同じ |
| システムのリセンターを模擬 | — | R キー（yaw が 30° 跳ぶ） |
| ドリフトを模擬 | — | D キー（yaw が 0.5° ずれる） |

流れ：起動時の安全表示 → 確定 → 基準方向を向いて確定（原点θ0）→ 物体の方を向いて確定 → Beam Pro で文字を入力して OK。

### ボタンの呼び方

眼鏡（XREAL One）のボタンは、XREAL 公式チュートリアル（[Buttons](https://tutorials.xreal.com/docs/glasses/one-series/first-use/buttons)）の呼び方に合わせる。日本語の公式名は見つからなかったので英語名を使う。アプリの表示・ログ・コードはすべてこの呼び方にそろえている（`ButtonNames.cs`）。

| 公式の呼び方 | 位置（右のつる） | SDK のキー種別 | 眼鏡側の標準の動き（公式） | このアプリでの扱い | ログの ID |
|---|---|---|---|---|---|
| Xボタン（X Button） | 下側の赤いボタン | `MULTI_KEY` | クリック：Follow / Anchor の切り替え、ダブルクリック：OSD メニュー、長押し：画面のリセンター | クリックで確定。長押しは眼鏡のリセンターとして検知 | `Glasses_X_Click` など |
| +/-ボタン（+/- Button） | 下側 | `INCREASE_KEY` / `DECREASE_KEY` | Quick Panel、明るさ・音量・調光の調整 | 記録だけ（確定には使わない） | `Glasses_Plus_Click` / `Glasses_Minus_Click` |
| Quickボタン（Quick Button） | 上側の短いボタン | `MENU_KEY`（推定・要確認） | 割り当て可能（標準は UltraWide） | 記録だけ | `Glasses_Quick_Click` |

Beam Pro 側は、画面の大きな青いボタン（ログ ID `BeamPro_ConfirmButton`）と、SDK の仮想コントローラーの Trigger（`BeamPro_Trigger`、公式の [Controller](https://docs.xreal.com/Input%20and%20Interactions/Controller) の呼び方）。クリックの種類は クリック / ダブルクリック / 長押し（ログでは `Click` / `DoubleClick` / `LongPress`）。

Xボタンのクリックは、眼鏡側でも Follow / Anchor の切り替えとして扱われる（実機で `ACTION_TYPE_TRIGGER_SWITCH_SPACE_MODE` が届いた）。このアプリの表示に影響するかは要確認。

エディタでは Game ビューの左側が眼鏡の表示、右側が Beam Pro の画面になる。

## 仕様との対応

| ID | 実装 |
|---|---|
| F-01 原点設定 | `LabelApp.SetOrigin`：確定時の頭の yaw を θ0 に。未設定のあいだは配置できない |
| F-02 ラベル配置 | `LabelApp.BeginPlace` → 仮ラベル（薄いパネル）を表示 → Beam Pro で入力 → `OnTextSubmitted` で生成 |
| F-03 ホバー | `LabelApp.UpdateHover`：視線レイとパネルの交差判定。枠を明るくする |
| F-04 編集 / F-05 削除 | ホバー中に Beam Pro の「編集」「削除」 |
| F-06 全消去 | 「全消去」→ 確認ダイアログ |
| F-07 リセンター | 「リセンター」→「ラベルがすべてリセットされますが、よろしいですか？」→ 全削除・原点未設定へ |
| F-08 画角外の誘導 | `GlassesHud.UpdateArrows`：画角外かつ視線との角度差 40° 以内で、画面端に白い矢印 |
| F-09 上限 | 20 個。超えたら Beam Pro に通知（振動つき） |

- ラベルは `yawRel`（θ0 からの相対 yaw）と `pitch` で保持し、位置は「原点の向き × 相対角 × 2.0 m」で毎フレーム求める（`LabelData` / `LabelApp.LateUpdate`）。
- 表示：2.0 m 固定・ビルボード・白 50% の角丸パネル・文字高 5 cm（`LabelView`）。レティクルは中央の白点（`GlassesHud`）。
- 文字色は Beam Pro 画面のボタンで「黒の抜き文字 / 白文字＋縁取り」を切り替えられる（要実機確認項目の比較用）。

### システム側リセンターの検知（要実機確認）

`RecenterDetector` で次の 3 つを併用し、どれかで検知したら全ラベルを削除して「リセンターされたためラベルをリセットしました」と表示する。

1. 眼鏡イベント `ACTION_TYPE_TRIGGER_RECENTER`。SDK 3.1.0 では internal の `XREALCallbackHandler.OnGlassesEventExt` に流れるため、リフレクションで購読している（IL2CPP 用に `Assets/link.xml` で保持）
2. `XRInputSubsystem.trackingOriginUpdated`
3. 1 フレームで yaw が 45° 以上跳んだとき（閾値は Inspector で変更可）

アプリからのリセンター（F-07）は `XRInputSubsystem.TryRecenter()` を試し、対応していなければ XR Origin のカメラオフセットを回して「今の向き = yaw 0」にする。SDK 3.1.0 には頭のリセンター API がない（`XREALPlugin.RecenterController` はコントローラー用）。

## ログ

`Application.persistentDataPath/Logs/` に保存（Beam Pro では `/sdcard/Android/data/com.mp.xreallabel/files/Logs/`）。

- `oplog_YYYYMMDD_HHMMSS.csv`：`timestamp,event,labelId,headYaw,headPitch,headRoll,detail`
  - event：`origin_set / recenter / place / edit / delete / clear_all / key_press / recenter_detected`
  - `detail` は仕様書の列に追加した補足列（キーの種類、ラベル文字列、ドリフト量など）
  - `recenter` 行の detail に、リセンター直前の headYaw と θ0 の差（`diff=`）を記録する。基準方向を向いてからリセンターすると、そのままドリフト量になる
  - 届いたボタン入力はすべて `key_press`（例 `Glasses_X_Click`）として残る。ID は上の「ボタンの呼び方」を参照
- `session_YYYYMMDD_HHMMSS.json`：`originYaw`・`originSetAt`・`recenterCount`

```bash
adb pull /sdcard/Android/data/com.mp.xreallabel/files/Logs ./Logs
```

```bash
adb logcat -s Unity | grep LabelApp
```

## 要実機確認（実装側で調整できるところ）

| 確認項目 | 調整箇所 |
|---|---|
| 確定に使う眼鏡のボタン | `InputRouter.m_ConfirmKeys`（Xボタンのクリックだけ）。届いたボタンは Beam Pro 画面の「最後の入力」に公式の呼び方で出る。Quickボタンが `MENU_KEY` かどうかは、Quickボタンを押して「最後の入力」を見れば確かめられる |
| ソフトキーボードが使えるか | 使えなければ候補ボタンで入力。候補は `LabelApp.m_Presets` |
| システムリセンターの検知 | logcat の `GlassesEvent` と `recenter_detected` 行の detail を見る |
| 文字色 | Beam Pro 画面のボタンで切替 |
| 矢印の閾値（40°） | `LabelApp.m_ArrowThreshold` |

## 実機での接続

Beam Pro の USB-C は、眼鏡（映像出力）と adb が同じ口しか使えない。眼鏡をつないだままログを見るには Wi-Fi 経由の adb を使う。

1. Mac のケーブルを眼鏡用の口に挿して `adb tcpip 5555`
2. Beam Pro の Wi-Fi アドレスを確認（IPv6 だけのネットワークなら IPv6 アドレスを使う）
3. ケーブルを抜いて眼鏡を挿し、`adb connect [アドレス]:5555`

Unity で Android ビルドをすると adb サーバーが再起動されるので、ビルド後は `adb connect` し直す。初回起動時は「他のアプリの上に重ねて表示」の許可が必要。

## エディタでの通し確認（PlayMode テスト）

`Assets/Tests/LabelAppFlowTests.cs` が Main シーンを動かし、安全表示 → 原点設定 → 配置 → ホバー → 編集 → 候補入力 → 画角外の矢印 → 削除 → 上限20個 → 全消去 → リセンター → システムリセンター検知 → CSV の内容までを確認する。
眼鏡ビューと Beam Pro 画面のスクリーンショットは `Logs/TestCaptures/` に保存される。

Window → General → Test Runner → PlayMode → Run All。

コマンドラインから：

```bash
open -W -n -a "/Applications/Unity/Hub/Editor/6000.0.58f2/Unity.app" --args -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults "$PWD/Logs/test-results.xml" -logFile "$PWD/Logs/test.log"
```

エディタ（Mac）では SDK の `XREALCallbackHandler.OnLoad` が `DllNotFoundException: XREALXRPlugin` を出すが、Mac 用のネイティブプラグインがないためで、動作には影響しない。

## スクリプト構成

```
Assets/Scripts/
  LabelApp.cs           状態遷移と機能（F-01〜F-09）
  LabelData.cs          ラベル・セッションのデータ
  LabelView.cs          眼鏡側のラベル表示（パネル・文字・ホバー枠・当たり判定）
  GlassesHud.cs         レティクル・案内・通知・画角外の矢印
  PhoneUI.cs            Beam Pro 画面のUI（ボタン・文字入力・確認ダイアログ）
  InputRouter.cs        確定入力（Xボタン / Trigger / 青いボタン / Space）
  ButtonNames.cs        ボタンの呼び方（公式名と SDK のキー種別の対応）
  RecenterDetector.cs   システムリセンターの検知とアプリからのリセンター
  OperationLogger.cs    操作ログCSV・セッションJSON
  EditorHeadSimulator.cs エディタ用の頭の回転シミュレーター
  HeadMath.cs / UiFactory.cs  角度計算・UI生成ヘルパー
Assets/Editor/ProjectSetup.cs  シーン・XR・Player 設定の一括セットアップ
```
