# VRCFT Auto Setup

VRChat Face Tracking (VRCFT) 用のアニメーションセットアップを、対象アバターのブレンドシェイプから生成するUnity Editor拡張です。

アバター内の表情ブレンドシェイプを検知し、VRCFT v2パラメーターで動作するAnimator Controller、Expression Parameters、Expression Menu、Modular Avatar用の配置オブジェクトをまとめて作成します。

## 特長

- 面倒なVRCFT向けAnimator、同期パラメーター、Expression Menuをワンクリックで生成します。
- Modular Avatarに対応し、元のアバター構成を直接書き換えない非破壊導入ができます。
- Binary同期に対応し、必要な同期パラメーターを最小化します。標準プリセットでは制御パラメーター込みで78bitを目安に生成します。
- ローカルとリモートで別々に調整できるスムージング機能により、Binary同期やリモート更新レートの低さで生じるコマ送り感を緩和します。

## インストール

VRChat Creator Companionに以下のVPMリポジトリを追加してから、プロジェクトに `VRCFT Auto Setup` を追加してください。

```text
https://kurotori4423.github.io/vpm.kurotori4423/vpm.json
```

## 依存パッケージ

- VRChat SDK - Avatars
- Modular Avatar

## 使い方

1. Unity Editorのメニューから `Tools/Kurotori/VRCFT Auto Setup` を開きます。
2. `対象アバター` に `VRCAvatarDescriptor` が付いたアバターを指定します。
3. `検知` を押して、フェイスメッシュとブレンドシェイプの対応を確認します。
4. 必要に応じてプリセット、生成設定、各パラメーターの有効状態やビット数を調整します。
5. `生成してアバターに配置` を押すと、生成物が保存され、Modular Avatarコンポーネント付きのオブジェクトがアバター直下に配置されます。

生成物は既定で `Assets/VrcftAutoSetup/Generated/<アバター名>/` に出力されます。

## 主な機能

### ブレンドシェイプ自動検知

対象アバターの `SkinnedMeshRenderer` を走査し、VRCFT v2パラメーターに対応するブレンドシェイプを検知します。

対応名はARKit系の `jawOpen`、Unified Expressions系の `JawOpen`、SRanipal系の `Jaw_Open` などを想定しています。大文字小文字や一部の記号差は正規化して比較します。

自動検知できなかった項目は、パラメーター一覧の `手動` 欄からブレンドシェイプ名を直接指定できます。

### Animator Controller生成

検知結果からFX用Animator Controllerを生成します。

- VRCFT v2パラメーターを受け取るレイヤー
- 必要に応じたBinaryデコードレイヤー
- 必要に応じたスムージングレイヤー
- 表情ブレンドシェイプを駆動するレイヤー
- `EyeTrackingActive` / `LipTrackingActive` による有効・無効切り替えレイヤー

EyeLookを有効にした場合は、Humanoid eye muscleを駆動するAdditive用Animator Controllerも生成します。

### Modular Avatar配置

生成したAnimator Controller、同期パラメーター、Expression MenuをModular Avatarのコンポーネントとしてまとめたオブジェクトを作成し、アバター直下に配置します。

同名の配置オブジェクトが既にある場合は、生成時に置き換えます。

### Expression Menu生成

`メニュー生成` が有効な場合、アバターのメニューに `Face Tracking` サブメニューを追加します。

生成される項目は設定に応じて変わります。

- `Eye Tracking`: 目・視線系のVRCFT駆動を切り替えます。
- `Lip Tracking`: 口・舌系のVRCFT駆動を切り替えます。
- `Voice LipSync Blend`: 発声中にVRChat標準Visemeを優先するか切り替えます。
- `Smoothing`: ローカル環境でのスムージング量をRadial Puppetで調整します。

## 生成設定

### プリセット

生成対象にするVRCFTパラメーターの範囲を選びます。

- `Minimal`: 主要な目、まぶた、口開き、笑顔・口角、舌などを中心にした軽量構成です。同期ビット数を抑えたい場合に向きます。
- `Standard`: 左右独立の目や主要な口・頬・眉を含む標準構成です。通常はこの設定から始めるのがおすすめです。
- `Full`: 細かな眉、頬、鼻、舌、口周りの補助表情まで含める構成です。対応ブレンドシェイプが多いアバター向けです。

プリセットを変更すると、対象外のパラメーターは一覧から除外されます。

### Binary同期 (ビット削減)

VRCFTのFloatパラメーターを複数のBoolビットとして同期し、Animator内でFloat値へ復元します。

有効にすると同期ビット数を抑えやすくなります。無効にすると各有効パラメーターをFloatとして扱うため、1パラメーターあたり8bit相当で計算されます。

### ビット数一律上書き

Binary同期が有効な場合に使います。

`0` の場合は各パラメーターの既定ビット数を使います。`1` 以上を指定すると、すべてのパラメーターに同じビット数を適用します。

値を大きくすると精度は上がりますが、同期ビット数も増えます。値を小さくすると軽くなりますが、表情の段階が粗くなります。

### Write Defaults

生成するAnimator StateのWrite Defaults方針です。

- `On`: すべての生成ステートをWrite Defaults Onにします。既定値です。
- `Mix`: 通常はOff寄りにしつつ、AAPやDirect BlendTreeなど必要な箇所だけOnにします。
- `Off`: すべてOffにします。この設定ではスムージングは無効化されます。

既存アバターのAnimator構成と合わせたい場合に調整してください。

### スムージング

Animator内でVRCFTパラメーターの変化をなめらかにします。

- `ローカル`: 自分の環境で見える表情のスムージング量です。Expression Menuの `Smoothing` から調整できます。
- `リモート`: 他ユーザーから見える表情向けのスムージング量です。

値が小さいほど入力に素早く追従し、値が大きいほど変化がなめらかになります。Binary同期の段階的な変化や、リモート表示で更新間隔が目立つ場合のコマ送り感を抑えたいときに調整します。Write Defaultsが `Off` の場合は無効になります。

### EyeLook (視線)

VRCFTの視線パラメーターから、Humanoidの目ボーン用muscleを駆動するAdditive Animator Controllerを生成します。

現在の実装では `HumanoidMuscleFixed` が生成対象です。`BlendShapes` はUI上の選択肢として存在しますが、この方式ではAdditive EyeLook Controllerは生成されません。

### 声でリップシンク優先

有効にすると、`Voice` パラメーターがしきい値を超えている間だけVRChat標準のVisemeリップシンクを優先します。

話していない間はVRCFTの口トラッキングを使い、発声中は標準リップシンクへ寄せたい場合に使います。

### Voiceしきい値

`声でリップシンク優先` が切り替わるVoice音量のしきい値です。

値を下げると小さい声でも標準リップシンクへ切り替わり、値を上げると大きめの発声時だけ切り替わります。

### メニュー生成

有効にすると、`Face Tracking` サブメニューを生成してModular Avatar Menu Installerで追加します。

既に独自メニューへ手動で組み込みたい場合は無効にできます。

### 出力フォルダ

生成アセットの保存先です。

既定値は `Assets/VrcftAutoSetup/Generated` です。実際の出力先はこのフォルダの下にアバター名のフォルダを作って保存されます。

## パラメーター一覧の見方

- `手動`: ブレンドシェイプ名を手動指定する欄を開きます。
- `有効`: そのVRCFTパラメーターを生成対象に含めるかを切り替えます。
- `パラメーター`: 生成されるVRCFT v2パラメーター名です。
- `ビット`: Binary同期時に使うビット数です。
- `検知シェイプ`: 自動検知または手動指定で割り当てられたブレンドシェイプ名です。

未検知のパラメーターは灰色で表示されます。必要なブレンドシェイプが存在する場合は、手動欄から名前を入力してください。

## 生成される主なファイル

既定設定では、出力フォルダ内に以下のようなアセットが作成されます。

- `FX_FaceTracking.controller`
- `Animations/`
- `Animations/Binary/`
- `Animations/Smooth/`
- `Additive/Additive_EyeTracking.controller`
- `Additive/Animations/`
- `Additive/Masks/VRCFT_HeadOnly.mask`
- `Menu/FT_Root.asset`
- `Menu/FT_Menu.asset`
- `<アバター名>_VRCFT.prefab`

設定や検知結果によって、一部のフォルダやアセットは生成されない場合があります。
