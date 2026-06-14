# VRCFT Auto Setup

VRChat Face Tracking (VRCFT) 用のアニメーションセットアップを、対象アバターのブレンドシェイプから生成するUnity Editor拡張です。

## インストール

VRChat Creator Companionに以下のVPMリポジトリを追加してから、プロジェクトに `VRCFT Auto Setup` を追加してください。

```text
https://kurotori4423.github.io/vpm.kurotori4423/vpm.json
```

## 依存パッケージ

- VRChat SDK - Avatars
- Modular Avatar

## 使い方

Unity Editorのメニューから `Tools/Kurotori/VRCFT Auto Setup` を開き、対象アバターを指定して生成を実行します。

生成物は既定で `Assets/VrcftAutoSetup/Generated/<アバター名>/` に出力されます。
