using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// VRCExpressionsMenu アセットを生成する。
    /// FT_Root (MenuInstaller で追加するルート) → "Face Tracking" SubMenu → FT_Menu (本体)。
    /// </summary>
    public static class VrcftMenuBuilder
    {
        private const string SmoothingParam = "OSCm/Local/FloatSmoothing";

        /// <summary>
        /// 実効設定に合わせたメニューを生成し、MenuInstaller に渡すルートメニュー (FT_Root) を返す。
        /// </summary>
        public static VRCExpressionsMenu BuildMenu(VrcftGenerationResult result, string outputDir, bool includeSmoothing, bool includeVoiceLipSyncBlend, VrcftLanguage language)
        {
            string menuDir = outputDir.Replace('\\', '/').TrimEnd('/') + "/Menu";
            VrcftAssetUtility.EnsureFolder(menuDir);

            // ---------- FT_Menu (サブメニュー本体) ----------
            var ftMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            ftMenu.name = "FT_Menu";
            ftMenu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = VrcftLocalization.T(language, "menu.eyeTracking"),
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "EyeTrackingActive" },
                value = 1f,
            });
            ftMenu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = VrcftLocalization.T(language, "menu.lipTracking"),
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "LipTrackingActive" },
                value = 1f,
            });
            if (includeVoiceLipSyncBlend)
            {
                ftMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = VrcftLocalization.T(language, "menu.voiceLipSyncBlend"),
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "VoiceLipSyncBlend" },
                    value = 1f,
                });
            }
            if (includeSmoothing)
            {
                // スムージング無効時は対応する MA Parameter も生成されないため、メニュー参照も作らない。
                ftMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = VrcftLocalization.T(language, "menu.smoothing"),
                    type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                    subParameters = new[]
                    {
                        new VRCExpressionsMenu.Control.Parameter { name = SmoothingParam },
                    },
                });
            }
            AssetDatabase.CreateAsset(ftMenu, menuDir + "/FT_Menu.asset");

            // ---------- FT_Root (MenuInstaller で追加するルート) ----------
            var ftRoot = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            ftRoot.name = "FT_Root";
            ftRoot.controls.Add(new VRCExpressionsMenu.Control
            {
                name = VrcftLocalization.T(language, "menu.faceTracking"),
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                subMenu = ftMenu,
            });
            AssetDatabase.CreateAsset(ftRoot, menuDir + "/FT_Root.asset");

            EditorUtility.SetDirty(ftMenu);
            EditorUtility.SetDirty(ftRoot);
            AssetDatabase.SaveAssets();

            return ftRoot;
        }
    }
}
