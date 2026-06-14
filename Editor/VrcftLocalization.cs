using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// エディタUIと生成Expression Menuで使用する表示言語。
    /// </summary>
    public enum VrcftLanguage
    {
        Auto = 0,
        Japanese = 1,
        English = 2,
    }

    /// <summary>
    /// Editor専用の軽量ローカライズ辞書。
    /// 配布パッケージの依存を増やさず、エディタ拡張内の固定文字列だけを切り替える。
    /// </summary>
    internal static class VrcftLocalization
    {
        private const string LanguagePrefsKey = "Kurotori.VrcftAutoSetup.Language";

        private static readonly Dictionary<string, string> Japanese = new Dictionary<string, string>
        {
            ["language"] = "表示言語",
            ["language.auto"] = "自動",
            ["language.japanese"] = "日本語",
            ["language.english"] = "English",
            ["title"] = "VRCFT 自動セットアップ",
            ["description"] = "対象アバターのブレンドシェイプを検知し、VRCFT用のアニメーションを生成してModular Avatarコンポーネント付きでアバターに配置します。",
            ["avatar"] = "対象アバター",
            ["detect"] = "検知",
            ["preset"] = "プリセット",
            ["parameterProfile"] = "パラメーターモード",
            ["settings"] = "生成設定",
            ["useBinary"] = "Binary同期 (ビット削減)",
            ["bitsOverride"] = "ビット数一律上書き (0=個別)",
            ["writeDefaults"] = "Write Defaults",
            ["smoothing"] = "スムージング",
            ["smoothingDisabled"] = "Write Defaults Off では AAP を使うスムージングが不安定なため無効化されます。",
            ["local"] = "ローカル",
            ["remote"] = "リモート",
            ["eyeLook"] = "EyeLook (視線)",
            ["driveMode"] = "駆動方式",
            ["voiceLipSyncBlend"] = "声でリップシンク優先",
            ["voiceThreshold"] = "Voiceしきい値",
            ["addMenu"] = "メニュー生成",
            ["menuLanguage"] = "生成メニュー言語",
            ["outputFolder"] = "出力フォルダ",
            ["tooltip.language"] = "このエディタウィンドウに表示する言語です。自動ではUnity Editorのシステム言語から判定します。",
            ["tooltip.avatar"] = "VRCFT用アセットを生成して配置する対象アバターです。VRCAvatarDescriptorが付いたオブジェクトを指定します。",
            ["tooltip.preset"] = "生成対象に含めるVRCFTパラメーターの範囲です。Minimal、Standard、Fullの順に対象が増えます。",
            ["tooltip.parameterProfile"] = "通常パラメーターとSimplified Trackingパラメーターの優先方針です。Hybridは対応シェイプの揃い方に応じて自動選択します。",
            ["tooltip.useBinary"] = "VRCFTのFloatパラメーターを複数のBoolビットとして同期し、Animator内でFloatへ復元します。同期ビット数を抑えるための設定です。",
            ["tooltip.bitsOverride"] = "Binary同期時に全パラメーターへ同じビット数を適用します。0の場合は各パラメーターの既定値を使います。",
            ["tooltip.writeDefaults"] = "生成するAnimator StateのWrite Defaults方針です。既存アバターのAnimator構成に合わせて選択します。",
            ["tooltip.smoothing"] = "Animator内でVRCFTパラメーターの変化をなめらかにします。Write Defaults Offでは無効化されます。",
            ["tooltip.local"] = "自分の環境で見える表情のスムージング量です。生成メニューのSmoothingから調整できます。",
            ["tooltip.remote"] = "他ユーザーから見える表情向けのスムージング量です。Binary同期やリモート更新の段階感を抑えるために使います。",
            ["tooltip.eyeLook"] = "VRCFTの視線パラメーターからHumanoidの目muscleを駆動するAdditive Controllerを生成します。",
            ["tooltip.driveMode"] = "EyeLookの駆動方式です。現在はHumanoid Muscle方式が生成対象です。",
            ["tooltip.voiceLipSyncBlend"] = "発声中だけVRChat標準Viseme LipSyncを優先し、話していない間はVRCFTの口トラッキングを使います。",
            ["tooltip.voiceThreshold"] = "Viseme LipSyncへ切り替えるVoice音量のしきい値です。低いほど小さい声でも切り替わります。",
            ["tooltip.addMenu"] = "Face Tracking用のExpression Menuを生成し、Modular Avatar Menu Installerで追加します。",
            ["tooltip.menuLanguage"] = "生成されるExpression Menuの表示名に使う言語です。AutoではエディタUIの表示言語に合わせます。",
            ["tooltip.outputFolder"] = "生成したAnimator Controller、Animation Clip、Expression Menu、Prefabの保存先フォルダです。",
            ["tooltip.manual"] = "自動検知できないシェイプや誤検知を、スロット単位で手動上書きする欄を開きます。",
            ["tooltip.enabled"] = "このVRCFTパラメーターを生成対象に含めるかを切り替えます。",
            ["tooltip.parameter"] = "生成されるVRCFT v2パラメーター名です。互換性のため翻訳されません。",
            ["tooltip.bits"] = "Binary同期時にこのパラメーターへ割り当てるビット数です。大きいほど精度が上がり、同期コストも増えます。",
            ["tooltip.detectedShape"] = "自動検知または手動指定で割り当てられたブレンドシェイプ名です。",
            ["detectionResult"] = "検知結果: {0} / {1} パラメーター",
            ["syncBits"] = "同期ビット数 (推定): {0} bits",
            ["parameterList"] = "パラメーター一覧",
            ["manual"] = "手動",
            ["enabled"] = "有効",
            ["parameter"] = "パラメーター",
            ["bits"] = "ビット",
            ["detectedShape"] = "検知シェイプ",
            ["generate"] = "生成してアバターに配置",
            ["selectInstalled"] = "配置オブジェクトを選択",
            ["notDetected"] = "(未検知)",
            ["none"] = "なし",
            ["active"] = "有効",
            ["inactive"] = "無効",
            ["avatarSummary"] = "アバター: {0}",
            ["faceMeshSummary"] = "フェイスメッシュ: {0}",
            ["scannedMeshesSummary"] = "走査メッシュ数: {0}",
            ["presetSummary"] = "プリセット: {0}",
            ["parameterProfileSummary"] = "パラメーターモード: {0}",
            ["detectedSummary"] = "検知: {0} / {1} パラメーター",
            ["missingSummary"] = "未検知 ({0}): {1}",
            ["missingNoneSummary"] = "未検知: なし",
            ["installComplete"] = "生成・配置完了",
            ["installedObject"] = "配置オブジェクト: {0}",
            ["prefab"] = "プレハブ: {0}",
            ["installFailed"] = "FX生成は完了しましたが、MA配置に失敗しました (Modular Avatar が必要です)",
            ["output"] = "出力先: {0}",
            ["layerCount"] = "レイヤー数: {0}",
            ["syncedParameterCount"] = "同期パラメーター数: {0}",
            ["clipCount"] = "クリップ数: {0}",
            ["syncedBitsTotal"] = "同期ビット合計 (localOnly除く): {0} / 256 bits",
            ["generateFailed"] = "生成失敗: {0}",
            ["error.modularAvatarRequired"] = "[VRCFT Auto Setup] Modular Avatarが必要です。パッケージをインストールしてください。",
            ["error.avatarMissing"] = "[VRCFT Auto Setup] アバターが指定されていません。",
            ["error.invalidFxResult"] = "[VRCFT Auto Setup] FX生成結果が無効です。",
            ["warning.prefabSaveFailed"] = "[VRCFT Auto Setup] プレハブ保存に失敗しました: {0}",
            ["profile.Hybrid"] = "Hybrid",
            ["profile.Simplified"] = "Simplified",
            ["profile.Detailed"] = "Detailed",
            ["eyeLookMode.HumanoidMuscleFixed"] = "Humanoid Muscle",
            ["eyeLookMode.BlendShapes"] = "BlendShapes",
            ["menu.faceTracking"] = "Face Tracking",
            ["menu.eyeTracking"] = "Eye Tracking",
            ["menu.lipTracking"] = "Lip Tracking",
            ["menu.voiceLipSyncBlend"] = "Voice LipSync Blend",
            ["menu.smoothing"] = "Smoothing",
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["language"] = "Display Language",
            ["language.auto"] = "Auto",
            ["language.japanese"] = "Japanese",
            ["language.english"] = "English",
            ["title"] = "VRCFT Auto Setup",
            ["description"] = "Detects blend shapes on the target avatar, generates VRCFT animation assets, and installs them under the avatar with Modular Avatar components.",
            ["avatar"] = "Target Avatar",
            ["detect"] = "Detect",
            ["preset"] = "Preset",
            ["parameterProfile"] = "Parameter Mode",
            ["settings"] = "Generation Settings",
            ["useBinary"] = "Binary Sync (bit reduction)",
            ["bitsOverride"] = "Override Bits (0 = per parameter)",
            ["writeDefaults"] = "Write Defaults",
            ["smoothing"] = "Smoothing",
            ["smoothingDisabled"] = "Smoothing uses AAP and is disabled because it is unstable with Write Defaults Off.",
            ["local"] = "Local",
            ["remote"] = "Remote",
            ["eyeLook"] = "EyeLook",
            ["driveMode"] = "Drive Mode",
            ["voiceLipSyncBlend"] = "Prefer Viseme LipSync While Speaking",
            ["voiceThreshold"] = "Voice Threshold",
            ["addMenu"] = "Generate Menu",
            ["menuLanguage"] = "Generated Menu Language",
            ["outputFolder"] = "Output Folder",
            ["tooltip.language"] = "Language used by this editor window. Auto resolves from the Unity Editor system language.",
            ["tooltip.avatar"] = "Target avatar where VRCFT assets will be generated and installed. Assign a GameObject with VRCAvatarDescriptor.",
            ["tooltip.preset"] = "Range of VRCFT parameters to generate. Minimal, Standard, and Full include progressively more parameters.",
            ["tooltip.parameterProfile"] = "Priority mode for detailed parameters and Simplified Tracking parameters. Hybrid selects based on the blend shapes available on the avatar.",
            ["tooltip.useBinary"] = "Syncs VRCFT Float parameters as multiple Bool bits and decodes them back to Float values in the Animator. This reduces synced parameter cost.",
            ["tooltip.bitsOverride"] = "Applies the same bit count to every parameter when Binary Sync is enabled. 0 uses each parameter's default bit count.",
            ["tooltip.writeDefaults"] = "Write Defaults policy for generated Animator States. Choose the mode that matches the avatar's existing Animator setup.",
            ["tooltip.smoothing"] = "Smooths VRCFT parameter changes inside the Animator. Disabled when Write Defaults is Off.",
            ["tooltip.local"] = "Smoothing amount visible in your local environment. This can be adjusted from the generated Smoothing menu control.",
            ["tooltip.remote"] = "Smoothing amount for how other users see your expressions. Use it to reduce stepped motion from Binary Sync or remote updates.",
            ["tooltip.eyeLook"] = "Generates an Additive Controller that drives Humanoid eye muscles from VRCFT gaze parameters.",
            ["tooltip.driveMode"] = "Drive mode for EyeLook generation. Humanoid Muscle is the currently generated mode.",
            ["tooltip.voiceLipSyncBlend"] = "Prefers VRChat standard Viseme LipSync while speaking, and uses VRCFT mouth tracking while silent.",
            ["tooltip.voiceThreshold"] = "Voice volume threshold for switching to Viseme LipSync. Lower values switch with quieter speech.",
            ["tooltip.addMenu"] = "Generates a Face Tracking Expression Menu and appends it with Modular Avatar Menu Installer.",
            ["tooltip.menuLanguage"] = "Language used for generated Expression Menu control names. Auto follows the editor UI language.",
            ["tooltip.outputFolder"] = "Folder where generated Animator Controllers, Animation Clips, Expression Menus, and Prefabs are saved.",
            ["tooltip.manual"] = "Opens per-slot manual overrides for shape keys that were not detected or were detected incorrectly.",
            ["tooltip.enabled"] = "Toggles whether this VRCFT parameter is included in generation.",
            ["tooltip.parameter"] = "VRCFT v2 parameter name to generate. It is not translated for compatibility.",
            ["tooltip.bits"] = "Bit count assigned to this parameter for Binary Sync. Higher values improve precision and increase sync cost.",
            ["tooltip.detectedShape"] = "Blend shapes assigned by automatic detection or manual override.",
            ["detectionResult"] = "Detection Result: {0} / {1} parameters",
            ["syncBits"] = "Estimated Sync Bits: {0} bits",
            ["parameterList"] = "Parameter List",
            ["manual"] = "Manual",
            ["enabled"] = "Enabled",
            ["parameter"] = "Parameter",
            ["bits"] = "Bits",
            ["detectedShape"] = "Detected Shapes",
            ["generate"] = "Generate and Install",
            ["selectInstalled"] = "Select Installed Object",
            ["notDetected"] = "(not detected)",
            ["none"] = "none",
            ["active"] = "enabled",
            ["inactive"] = "disabled",
            ["avatarSummary"] = "Avatar: {0}",
            ["faceMeshSummary"] = "Face Mesh: {0}",
            ["scannedMeshesSummary"] = "Scanned Meshes: {0}",
            ["presetSummary"] = "Preset: {0}",
            ["parameterProfileSummary"] = "Parameter Mode: {0}",
            ["detectedSummary"] = "Detected: {0} / {1} parameters",
            ["missingSummary"] = "Missing ({0}): {1}",
            ["missingNoneSummary"] = "Missing: none",
            ["installComplete"] = "Generation and installation complete",
            ["installedObject"] = "Installed Object: {0}",
            ["prefab"] = "Prefab: {0}",
            ["installFailed"] = "FX generation completed, but MA installation failed (Modular Avatar is required).",
            ["output"] = "Output: {0}",
            ["layerCount"] = "Layer Count: {0}",
            ["syncedParameterCount"] = "Synced Parameters: {0}",
            ["clipCount"] = "Clips: {0}",
            ["syncedBitsTotal"] = "Total Sync Bits (excluding localOnly): {0} / 256 bits",
            ["generateFailed"] = "Generation failed: {0}",
            ["error.modularAvatarRequired"] = "[VRCFT Auto Setup] Modular Avatar is required. Please install the package.",
            ["error.avatarMissing"] = "[VRCFT Auto Setup] No avatar is assigned.",
            ["error.invalidFxResult"] = "[VRCFT Auto Setup] The FX generation result is invalid.",
            ["warning.prefabSaveFailed"] = "[VRCFT Auto Setup] Failed to save prefab: {0}",
            ["profile.Hybrid"] = "Hybrid",
            ["profile.Simplified"] = "Simplified",
            ["profile.Detailed"] = "Detailed",
            ["eyeLookMode.HumanoidMuscleFixed"] = "Humanoid Muscle",
            ["eyeLookMode.BlendShapes"] = "BlendShapes",
            ["menu.faceTracking"] = "Face Tracking",
            ["menu.eyeTracking"] = "Eye Tracking",
            ["menu.lipTracking"] = "Lip Tracking",
            ["menu.voiceLipSyncBlend"] = "Voice LipSync Blend",
            ["menu.smoothing"] = "Smoothing",
        };

        public static VrcftLanguage EditorLanguage
        {
            get => (VrcftLanguage)EditorPrefs.GetInt(LanguagePrefsKey, (int)VrcftLanguage.Auto);
            set => EditorPrefs.SetInt(LanguagePrefsKey, (int)value);
        }

        /// <summary>
        /// Auto指定を現在の実行環境に対する実言語へ解決する。
        /// </summary>
        public static VrcftLanguage ResolveLanguage(VrcftLanguage language)
        {
            if (language != VrcftLanguage.Auto) return language;
            return Application.systemLanguage == SystemLanguage.Japanese
                ? VrcftLanguage.Japanese
                : VrcftLanguage.English;
        }

        public static string T(string key)
        {
            return T(EditorLanguage, key);
        }

        public static string T(VrcftLanguage language, string key)
        {
            var table = ResolveLanguage(language) == VrcftLanguage.Japanese ? Japanese : English;
            return table.TryGetValue(key, out var value) ? value : key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static string Format(VrcftLanguage language, string key, params object[] args)
        {
            return string.Format(T(language, key), args);
        }

        public static GUIContent Content(string labelKey, string tooltipKey)
        {
            return new GUIContent(T(labelKey), T(tooltipKey));
        }

        public static string LanguageLabel(VrcftLanguage language)
        {
            switch (language)
            {
                case VrcftLanguage.Auto:
                    return T("language.auto");
                case VrcftLanguage.Japanese:
                    return T("language.japanese");
                case VrcftLanguage.English:
                    return T("language.english");
                default:
                    return language.ToString();
            }
        }

        public static string EnumLabel(Enum value)
        {
            if (value is VrcftParameterProfile)
            {
                return T("profile." + value);
            }
            if (value is EyeLookMode)
            {
                return T("eyeLookMode." + value);
            }
            return value.ToString();
        }
    }
}
