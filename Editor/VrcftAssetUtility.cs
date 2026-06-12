using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// クリップ/アセット生成・保存ユーティリティ。
    /// </summary>
    public static class VrcftAssetUtility
    {
        /// <summary>
        /// "Assets/Foo/Bar" のようなフォルダパスを再帰的に作成する。
        /// </summary>
        public static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            var parts = folderPath.Split('/');
            // 先頭は "Assets" 前提
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        /// <summary>
        /// 既存フォルダがあれば中身ごと削除して作り直す。
        /// </summary>
        public static void RecreateFolder(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.DeleteAsset(folderPath);
            }
            EnsureFolder(folderPath);
        }

        /// <summary>
        /// ブレンドシェイプ1つを定数値で駆動する 1フレームクリップにカーブを追加する。
        /// </summary>
        public static void AddBlendShapeConstant(AnimationClip clip, string relativePath, string blendShapeName, float value)
        {
            var binding = EditorCurveBinding.FloatCurve(relativePath, typeof(SkinnedMeshRenderer), "blendShape." + blendShapeName);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 0f, value));
        }

        /// <summary>
        /// Animatorパラメーター(AAP)を定数値で駆動する 1フレームクリップにカーブを追加する。
        /// </summary>
        public static void AddAnimatorParameterConstant(AnimationClip clip, string parameterName, float value)
        {
            clip.SetCurve("", typeof(Animator), parameterName, AnimationCurve.Constant(0f, 0f, value));
        }

        /// <summary>
        /// Humanoid muscle を定数値で駆動する 1フレームクリップにカーブを追加する。
        /// </summary>
        public static void AddHumanoidMuscleConstant(AnimationClip clip, string muscleName, float value)
        {
            clip.SetCurve("", typeof(Animator), muscleName, AnimationCurve.Constant(0f, 0f, value));
        }

        /// <summary>
        /// Transform の localEulerAnglesRaw.x/y/z を定数で駆動するカーブを追加する。
        /// </summary>
        public static void AddTransformEuler(AnimationClip clip, string relativePath, Vector3 euler)
        {
            clip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, 0f, euler.x));
            clip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Constant(0f, 0f, euler.y));
            clip.SetCurve(relativePath, typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, 0f, euler.z));
        }

        /// <summary>
        /// クリップを生成してアセット保存する。
        /// </summary>
        public static AnimationClip CreateClip(string assetPath, string name)
        {
            var clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        /// <summary>
        /// BlendTree を生成し、コントローラーのサブアセットとして登録する。
        /// </summary>
        public static BlendTree CreateBlendTree(AnimatorController controller, string name, BlendTreeType blendType)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = blendType,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        /// <summary>
        /// パラメーターが未定義なら追加する (存在チェック付き)。
        /// </summary>
        public static void CheckAndCreateParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float defaultFloat = 0f, bool defaultBool = false)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return;
            }
            var param = new AnimatorControllerParameter
            {
                name = name,
                type = type,
                defaultFloat = defaultFloat,
                defaultBool = defaultBool,
            };
            controller.AddParameter(param);
        }

        /// <summary>
        /// オイラー角を ±180° に正規化する (357° → -3°)。
        /// </summary>
        public static Vector3 NormalizeEuler(Vector3 e)
        {
            return new Vector3(NormalizeAngle(e.x), NormalizeAngle(e.y), NormalizeAngle(e.z));
        }

        private static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            else if (a < -180f) a += 360f;
            return a;
        }
    }
}
