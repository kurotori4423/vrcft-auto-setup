using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// EyeLook (視線) レイヤーの構築。Humanoid muscle を -1..1 の固定範囲で
    /// 左右の目を SimpleDirectional2D BT で合成する。
    /// </summary>
    public static class VrcftEyeLookGenerator
    {
        private const string LeftEyeDownUp = "Left Eye Down-Up";
        private const string LeftEyeInOut = "Left Eye In-Out";
        private const string RightEyeDownUp = "Right Eye Down-Up";
        private const string RightEyeInOut = "Right Eye In-Out";

        /// <summary>
        /// EyeLook 用の Direct BlendTree を構築する。Playable Layer の差し替えを容易にするためレイヤーは追加しない。
        /// </summary>
        public static BlendTree BuildMotion(
            AnimatorController controller,
            VrcftAutoSetupSettings settings,
            string clipOutputDir,
            VrcftGenerationResult result)
        {
            string SmoothName(string param) => VrcftAnimatorGenerator.UseSmoothing(settings) ? "OSCm/Smooth/" + param : param;
            const string blendSet = "OSCm/BlendSet";

            string leftX = SmoothName("FT/v2/EyeLeftX");
            string rightX = SmoothName("FT/v2/EyeRightX");
            string eyeY = SmoothName("FT/v2/EyeY");

            var leftTree = BuildEyeTree(controller, isLeft: true,
                blendParamX: leftX, blendParamY: eyeY, clipOutputDir, "EyeLeft", result);
            var rightTree = BuildEyeTree(controller, isLeft: false,
                blendParamX: rightX, blendParamY: eyeY, clipOutputDir, "EyeRight", result);

            var master = VrcftAssetUtility.CreateBlendTree(controller, "EyeLook_Master", BlendTreeType.Direct);
            master.children = new[]
            {
                new ChildMotion { motion = leftTree, directBlendParameter = blendSet, timeScale = 1f },
                new ChildMotion { motion = rightTree, directBlendParameter = blendSet, timeScale = 1f },
            };
            return master;
        }

        /// <summary>
        /// 片目分の X/Y 入力を Humanoid muscle の5方向クリップへ割り当てる。
        /// </summary>
        private static BlendTree BuildEyeTree(
            AnimatorController controller,
            bool isLeft,
            string blendParamX,
            string blendParamY,
            string clipOutputDir,
            string namePrefix,
            VrcftGenerationResult result)
        {
            // アバター固有値や Unity の標準可動域に依存せず、FT入力を -1..1 の muscle 値へ対応させる。
            var straight = new HumanoidEyePose(0f, 0f);
            var up = new HumanoidEyePose(1f, 0f);
            var down = new HumanoidEyePose(-1f, 0f);
            // 参考テンプレートの配置に合わせ、左目だけ X 入力と In-Out の符号対応を反転する。
            float leftInOut = isLeft ? 1f : -1f;
            float rightInOut = isLeft ? -1f : 1f;
            var left = new HumanoidEyePose(0f, leftInOut);
            var right = new HumanoidEyePose(0f, rightInOut);

            var straightClip = EyeClip(clipOutputDir, namePrefix + "_Straight", isLeft, straight, result);
            var upClip = EyeClip(clipOutputDir, namePrefix + "_Up", isLeft, up, result);
            var downClip = EyeClip(clipOutputDir, namePrefix + "_Down", isLeft, down, result);
            var leftClip = EyeClip(clipOutputDir, namePrefix + "_Left", isLeft, left, result);
            var rightClip = EyeClip(clipOutputDir, namePrefix + "_Right", isLeft, right, result);

            var tree = VrcftAssetUtility.CreateBlendTree(controller, namePrefix + "_2D", BlendTreeType.SimpleDirectional2D);
            tree.blendParameter = blendParamX;
            tree.blendParameterY = blendParamY;
            tree.children = new[]
            {
                new ChildMotion { motion = straightClip, position = new Vector2(0f, 0f), timeScale = 1f },
                new ChildMotion { motion = rightClip, position = new Vector2(1f, 0f), timeScale = 1f },
                new ChildMotion { motion = leftClip, position = new Vector2(-1f, 0f), timeScale = 1f },
                new ChildMotion { motion = upClip, position = new Vector2(0f, 1f), timeScale = 1f },
                new ChildMotion { motion = downClip, position = new Vector2(0f, -1f), timeScale = 1f },
            };
            return tree;
        }

        /// <summary>
        /// 片目の上下/内外 muscle を定数値で保持する EyeLook クリップを作成する。
        /// </summary>
        private static AnimationClip EyeClip(string clipOutputDir, string name, bool isLeft, HumanoidEyePose pose, VrcftGenerationResult result)
        {
            var clip = new AnimationClip { name = name };
            VrcftAssetUtility.AddHumanoidMuscleConstant(clip, isLeft ? LeftEyeDownUp : RightEyeDownUp, pose.DownUp);
            VrcftAssetUtility.AddHumanoidMuscleConstant(clip, isLeft ? LeftEyeInOut : RightEyeInOut, pose.InOut);
            string p = clipOutputDir.TrimEnd('/') + "/" + name + ".anim";
            UnityEditor.AssetDatabase.CreateAsset(clip, p);
            result.generatedClipPaths.Add(p);
            return clip;
        }

        /// <summary>
        /// 片目に必要な Humanoid muscle 値。
        /// </summary>
        private struct HumanoidEyePose
        {
            public HumanoidEyePose(float downUp, float inOut)
            {
                DownUp = downUp;
                InOut = inOut;
            }

            public float DownUp;
            public float InOut;
        }
    }
}
