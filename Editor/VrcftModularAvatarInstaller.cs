using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#if USE_MODULAR_AVATAR
using nadena.dev.modular_avatar.core;
#endif

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// Modular Avatar コンポーネント (MergeAnimator / Parameters / MenuInstaller) を持つ
    /// オブジェクトを生成し、対象アバター直下に配置・プレハブ保存する。
    /// </summary>
    public static class VrcftModularAvatarInstaller
    {
        public static GameObject Install(VRCAvatarDescriptor avatar, VrcftGenerationResult result, VrcftAutoSetupSettings settings)
        {
#if !USE_MODULAR_AVATAR
            Debug.LogError(VrcftLocalization.T("error.modularAvatarRequired"));
            return null;
#else
            if (avatar == null)
            {
                Debug.LogError(VrcftLocalization.T("error.avatarMissing"));
                return null;
            }
            if (result == null || result.fxController == null)
            {
                Debug.LogError(VrcftLocalization.T("error.invalidFxResult"));
                return null;
            }

            string objName = avatar.name + "_VRCFT";

            // 1. 既存の同名オブジェクトを削除
            var existing = avatar.transform.Find(objName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            // 2. 新規オブジェクト生成・配置
            var obj = new GameObject(objName);
            obj.transform.SetParent(avatar.transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            // 3. MergeAnimator
            AddMergeAnimator(obj, result.fxController, VRCAvatarDescriptor.AnimLayerType.FX);
            if (result.additiveController != null)
            {
                AddMergeAnimator(obj, result.additiveController, VRCAvatarDescriptor.AnimLayerType.Additive);
            }

            // 4. Parameters
            var maParams = obj.AddComponent<ModularAvatarParameters>();
            maParams.parameters.Clear();
            foreach (var p in result.syncedParameters)
            {
                maParams.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = p.name,
                    syncType = ResolveParameterSyncType(p),
                    defaultValue = p.defaultValue,
                    saved = p.saved,
                    localOnly = p.localOnly,
                    hasExplicitDefaultValue = true,
                });
            }
            EditorUtility.SetDirty(maParams);

            // 5. メニュー
            if (settings.addMenu)
            {
                // Auto指定時はエディタ上で選んだ表示言語に合わせ、生成物だけ別言語になる混乱を避ける。
                var menuLanguage = settings.generatedMenuLanguage == VrcftLanguage.Auto
                    ? VrcftLocalization.EditorLanguage
                    : settings.generatedMenuLanguage;
                VRCExpressionsMenu rootMenu = VrcftMenuBuilder.BuildMenu(
                    result,
                    result.outputDir,
                    VrcftAnimatorGenerator.UseSmoothing(settings),
                    settings.enableVoiceLipSyncBlend,
                    menuLanguage);
                var installer = obj.AddComponent<ModularAvatarMenuInstaller>();
                installer.menuToAppend = rootMenu;
                EditorUtility.SetDirty(installer);
            }

            // 7. プレハブ保存
            string prefabPath = result.outputDir.Replace('\\', '/').TrimEnd('/') + "/" + objName + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);
            if (prefab == null)
            {
                Debug.LogWarning(VrcftLocalization.Format("warning.prefabSaveFailed", prefabPath));
            }

            EditorUtility.SetDirty(obj);
            return obj;
#endif
        }

#if USE_MODULAR_AVATAR
        /// <summary>
        /// Playable Layer ごとの AnimatorController を Modular Avatar の MergeAnimator として追加する。
        /// </summary>
        private static void AddMergeAnimator(GameObject obj, UnityEditor.Animations.AnimatorController controller, VRCAvatarDescriptor.AnimLayerType layerType)
        {
            var merge = obj.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = controller;
            merge.layerType = layerType;
            merge.deleteAttachedAnimator = true;
            merge.pathMode = MergeAnimatorPathMode.Absolute;
            merge.matchAvatarWriteDefaults = false;
            EditorUtility.SetDirty(merge);
        }

        /// <summary>
        /// MA Parameters に登録する同期種別を返す。
        /// </summary>
        private static ParameterSyncType ResolveParameterSyncType(VrcftSyncedParameter parameter)
        {
            if (IsAnimatorOnlyParameter(parameter.name))
            {
                // AAP の書き込み先は Debug 表示で正しい値を確認できないため、Animator のみに留める。
                return ParameterSyncType.NotSynced;
            }

            return parameter.kind == VrcftParameterKind.Bool
                ? ParameterSyncType.Bool
                : ParameterSyncType.Float;
        }

        /// <summary>
        /// Expression Parameters に出さず、Animator Controller 内だけで使う補助パラメーターか判定する。
        /// </summary>
        private static bool IsAnimatorOnlyParameter(string parameterName)
        {
            return parameterName != null && parameterName.StartsWith("OSCm/Smooth/");
        }
#endif
    }
}
