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
            Debug.LogError("[VRCFT Auto Setup] Modular Avatarが必要です。パッケージをインストールしてください。");
            return null;
#else
            if (avatar == null)
            {
                Debug.LogError("[VRCFT Auto Setup] アバターが指定されていません。");
                return null;
            }
            if (result == null || result.fxController == null)
            {
                Debug.LogError("[VRCFT Auto Setup] FX生成結果が無効です。");
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
                    syncType = p.kind == VrcftParameterKind.Bool
                        ? ParameterSyncType.Bool
                        : ParameterSyncType.Float,
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
                VRCExpressionsMenu rootMenu = VrcftMenuBuilder.BuildMenu(
                    result,
                    result.outputDir,
                    VrcftAnimatorGenerator.UseSmoothing(settings));
                var installer = obj.AddComponent<ModularAvatarMenuInstaller>();
                installer.menuToAppend = rootMenu;
                EditorUtility.SetDirty(installer);
            }

            // 7. プレハブ保存
            string prefabPath = result.outputDir.Replace('\\', '/').TrimEnd('/') + "/" + objName + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.AutomatedAction);
            if (prefab == null)
            {
                Debug.LogWarning("[VRCFT Auto Setup] プレハブ保存に失敗しました: " + prefabPath);
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
#endif
    }
}
