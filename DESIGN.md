# VRCFT Auto Setup — 設計ドキュメント

VRChat Face Tracking (VRCFT) 用のアニメーションセットアップを、対象アバターから自動生成する Unity エディタ拡張。

## 目的

1. アバターのフェイスメッシュから FT 用ブレンドシェイプ (ARKit / Unified Expressions / SRanipal 命名) を自動検知
2. 検知結果から最適な AnimatorController (FXレイヤー) を自動構築
3. Binary Parameter による同期ビット数削減オプション
4. VRCAvatarDescriptor の EyeLook RotationStates からボーン駆動の視線アニメーションを自動生成
5. OSCmooth 方式の Animator ベーススムージングを統合
6. Modular Avatar (MA MergeAnimator / Parameters / MenuInstaller) でインストール容易なプレハブとして出力し、対象アバター直下に配置
7. Expression メニュー (FT On/Off、Eye/Lip 個別トグル、スムージング調整) を生成

## パッケージ構成

```
Packages/com.kurotori.vrcft-auto-setup/
├── package.json                 (name: com.kurotori.vrcft-auto-setup, editor-only)
├── DESIGN.md
└── Editor/
    ├── Kurotori.VrcftAutoSetup.Editor.asmdef
    ├── VrcftShapeCatalog.cs        … FTパラメーター定義 + シェイプ名エイリアス辞書
    ├── VrcftDetectionReport.cs     … 検知結果データモデル
    ├── VrcftAvatarDetector.cs      … アバター走査・マッチング
    ├── VrcftAutoSetupSettings.cs   … 生成オプション (プリセット/Binary/スムージング)
    ├── VrcftAssetUtility.cs        … クリップ/アセット生成・保存ユーティリティ
    ├── VrcftAnimatorGenerator.cs   … FXコントローラー構築 (Direct BT / Binaryデコード / スムージング)
    ├── VrcftEyeLookGenerator.cs    … EyeLook回転アニメ生成 + 視線レイヤー
    ├── VrcftMenuBuilder.cs         … VRCExpressionsMenuアセット生成
    ├── VrcftModularAvatarInstaller.cs … MAコンポーネント付きオブジェクト生成・配置
    └── VrcftAutoSetupWindow.cs     … EditorWindow (UI)
```

asmdef 参照: `VRC.SDK3A`, `VRC.SDKBase`, `nadena.dev.modular-avatar.core`。
versionDefines で `nadena.dev.modular-avatar` → `USE_MODULAR_AVATAR` を定義し MA 依存コードをガード。

生成アセット出力先: `Assets/VrcftAutoSetup/Generated/<アバター名>/` 配下
(`Animations/`, `Animations/Binary/`, `Animations/Smooth/`, `FX_FaceTracking.controller`, `Menu/`, `<アバター名>_FT.prefab`)

## データフロー (生成される Animator の構造)

```
VRCFT OSC入力 (同期パラメーター: Float直値 or Binary Bool群)
  ↓ [Layer 1] Binaryデコードレイヤー (Binary有効時のみ)
      Direct BT。各ビット Bool (FT/v2/X1,2,4,8 + XNegative) を
      AAPクリップ (Animatorパラメーター FT/v2/X を 重み 2^n/(2^N-1) で駆動) で合成
  ↓ FT/v2/X (Animator内Float、Binary時は非同期)
  ↓ [Layer 2] スムージングレイヤー (OSCmooth方式)
      IsLocal で Local/Remote 2ステート切替。各ステートは Direct BT。
      子: Simple1D(blend=OSCm/Local|Remote/FloatSmoothing)
            ├ t=0: Simple1D(blend=FT/v2/X)          → -1/+1 AAPクリップ
            └ t=1: Simple1D(blend=OSCm/Smooth/FT/v2/X) → -1/+1 AAPクリップ
      出力先: OSCm/Smooth/FT/v2/X (指数移動平均)
  ↓ OSCm/Smooth/FT/v2/X
  ↓ [Layer 3] 駆動レイヤー (Direct BT 1本)
      各パラメーターの子ツリー:
        単方向 (0..1): Simple1D → min/max ブレンドシェイプクリップ (値0-100)
        双方向 (-1..1): Simple1D threshold -1/0/+1 → 負側/ニュートラル/正側クリップ
      directBlendParameter は定数1の OSCm/BlendSet
  ↓ [Layer 4] EyeLookレイヤー
      OSCm/Smooth/FT/v2/EyeLeftX, EyeRightX, EyeY で
      Eye_L/Eye_R ボーン回転クリップ (Descriptor RotationStates由来) をブレンド
  ↓ [Layer 5] 制御レイヤー
      EyeTrackingActive / LipTrackingActive トグルで
      VRCAnimatorTrackingControl (Eyes/Mouth: Animation⇔Tracking) を切替
```

- WriteDefaults: 全生成ステート ON (Direct BT 構成の標準)。
- 全レイヤーは MA MergeAnimator (layerType=FX, pathMode=Relative, 相対パスルート=アバタールート) でマージ。
  ※ EyeLookボーン回転もFXに含める (Additiveレイヤー分割はオプション検討)。
- フェイスメッシュのパスはアバタールートからの相対パス (AoiSyu では `Body`)。

## パラメーター設計

- 命名は VRCFT v2 標準 (`FT/v2/JawOpen` 等)。VRCFT は avatar config (OSC) を自動生成するため、標準名に従えば追加設定不要。
- Float直値モード: 各パラメーターを Float 8bit で同期。
- Binaryモード: 各パラメーターを Bool×Nビット (`FT/v2/X1`, `X2`, `X4`[, `X8`]) + 双方向は `XNegative` 1bit。
  デコード重み: `(negative ? -1 : 1) × 2^bit / (2^N − 1)`。
- 共通制御: `EyeTrackingActive` (Bool, synced, saved), `LipTrackingActive` (Bool, synced, saved),
  `OSCm/Local/FloatSmoothing` (Float, local-only), `OSCm/Remote/FloatSmoothing` (Float, 非同期・Animator内定数)。
- パラメーター名は**大文字小文字を区別**。VRCFT は `v2/...` / `FT/v2/...` どちらの登録名も自動検出する (本ツールは `FT/v2/` を採用)。
- Binary デコード重み: `(negative ? -1 : 1) × 2^bit / (2^N − 1)` (adjerry テンプレート/OSCmooth 方式、最大値が 1.0 に到達)。
- Simplified 複合パラメーターを優先的に使い数を節約。アバターに対応シェイプが無いパラメーターは自動で除外。

### プリセット定義 (Web調査により確定)

**Minimal (~32bit, Binary時)** — 複合パラメーター中心:
EyeX(4), EyeY(4), EyeLid(4), JawOpen(4), SmileSad(4+1), LipPucker(3), TongueOut(1), 制御Bool×2
※双極は +1bit (Negative)

**Standard (~70bit)** — 左右独立の目+主要表情:
EyeLeftX/EyeRightX(各4+1), EyeY(4+1), EyeLidLeft/Right(各4), BrowExpression(3+1),
JawOpen(4), JawX(3+1), MouthClosed(3), SmileFrownLeft/Right(各4+1), MouthX(3+1),
MouthUpperUp(3), MouthLowerDown(3), LipPucker(3), LipFunnel(3), CheekPuffSuck(3+1),
TongueOut(3), 制御Bool×2

**Full (~120bit)** — Standard + 細部:
EyeSquintLeft/Right(各3), PupilDilation(3), BrowExpressionLeft/Right個別(各3+1),
JawForward(2), MouthStretch/Press/Dimple/RaiserUpper/RaiserLower(各2),
CheekSquintLeft/Right(各2), NoseSneer(2), LipSuckUpper/Lower(各3), TongueX/Y(各2+1)

精度指針 (VRCFT公式): まぶた・口開きなど目立つ部位は4bit以上、鼻・頬等は3bitで十分。

### AoiSyu での検知見込み

Body メッシュに ARKit 48種 (eyeLook系除く) が camelCase で存在。eyeLook はボーン (Eye_L/R) + Descriptor RotationStates (±3°)。
まばたきは eyeBlinkLeft/Right を EyeLid系にマップ。

## ブレンドシェイプ検知

`VrcftShapeCatalog` に FT パラメーター毎のエントリを定義:

```csharp
class CatalogEntry {
    string parameterName;        // 例 "FT/v2/JawOpen"
    bool twoSided;               // -1..1 か 0..1 か
    ShapeBinding[] positives;    // 正側に割り当てるシェイプ候補 (エイリアス配列)
    ShapeBinding[] negatives;    // 負側 (双方向のみ)
    int defaultBits;             // Binary時の推奨ビット数
    Preset minPreset;            // どのプリセットから含まれるか
}
```

エイリアス辞書は ARKit (camelCase: jawOpen)、Unified Expressions (PascalCase: JawOpen)、
SRanipal (Jaw_Open)、大文字小文字無視・記号除去で正規化マッチ。
複合パラメーター (例 SmileFrown) は複数シェイプ (mouthSmileLeft+Right / mouthFrownLeft+Right) を1クリップにまとめて駆動。

検知結果は `VrcftDetectionReport` としてUIに一覧表示し、ユーザーがマッピングの上書き・無効化を可能にする。

## EyeLook 生成

- Descriptor `customEyeLookSettings` の RotationStates (Up/Down/Left/Right の Quaternion) を読み取り、
  Eye_L/Eye_R の localEulerAngles をキーにした 1フレームクリップを生成 (Up/Down/Left/Right/Straight)。
- `FT/v2/EyeLeftX` / `FT/v2/EyeRightX` / `FT/v2/EyeY` (-1..1) の 2D相当を 1D×2段 (X→Y) の BT で合成。
- EyeTrackingActive 時は TrackingControl で Eyes=Animation に切替、OFF時は Tracking に戻す。
- まぶた: `FT/v2/EyeLidLeft/Right` (0..1, default 0.75) → eyeBlink シェイプを逆向き (0.75=開眼) に駆動。
  EyeWide があれば 0.75..1 を Wide に割当 (テンプレート方式)。

## Expression メニュー

```
Face Tracking/ (SubMenu)
├ Enable Eye Tracking  (Toggle: EyeTrackingActive)
├ Enable Lip Tracking  (Toggle: LipTrackingActive)
└ Smoothing            (RadialPuppet: OSCm/Local/FloatSmoothing)
```

MA MenuInstaller でアバターのルートメニューに追加。

## 実装フェーズ

- **Phase A**: 骨格 + カタログ + 検知 + UI (タスク#7)
- **Phase B**: クリップ/FX生成 + Binary + スムージング + EyeLook (タスク#8,9,10,11)
- **Phase C**: MA + メニュー + プレハブ配置 (タスク#12) → AoiSyu 適用検証 (タスク#13)

各フェーズ完了時に `unicli exec Compile` で検証。
