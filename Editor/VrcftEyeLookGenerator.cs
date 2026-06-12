using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// EyeLook (視線) レイヤーの構築。Descriptor の RotationStates から
    /// Eye_L/Eye_R ボーンの回転クリップを生成し、SimpleDirectional2D BT で合成する。
    /// </summary>
    public static class VrcftEyeLookGenerator
    {
        public static void Build(
            VRCAvatarDescriptor avatar,
            AnimatorController controller,
            VrcftAutoSetupSettings settings,
            VrcftDetectionReport report,
            List<VrcftAnimatorGenerator.Target> targets,
            string outputDir,
            VrcftGenerationResult result)
        {
            var info = report.EyeLook;

            string SmoothName(string param) => settings.enableSmoothing ? "OSCm/Smooth/" + param : param;
            const string blendSet = "OSCm/BlendSet";

            string leftX = SmoothName("FT/v2/EyeLeftX");
            string rightX = SmoothName("FT/v2/EyeRightX");
            string eyeY = SmoothName("FT/v2/EyeY");

            var leftTree = BuildEyeTree(avatar, controller, info, isLeft: true,
                blendParamX: leftX, blendParamY: eyeY, outputDir, "EyeLeft", result);
            var rightTree = BuildEyeTree(avatar, controller, info, isLeft: false,
                blendParamX: rightX, blendParamY: eyeY, outputDir, "EyeRight", result);

            var master = VrcftAssetUtility.CreateBlendTree(controller, "EyeLook_Master", BlendTreeType.Direct);
            master.children = new[]
            {
                new ChildMotion { motion = leftTree, directBlendParameter = blendSet, timeScale = 1f },
                new ChildMotion { motion = rightTree, directBlendParameter = blendSet, timeScale = 1f },
            };

            // defaultWeight = 0: VRCFT_Control の LayerControl が EyeTrackingActive 時にONにする。
            VrcftAnimatorGenerator.AddSingleStateLayer(controller, "VRCFT_EyeLook", master, settings.writeDefaults, defaultWeight: 0f);
        }

        private static BlendTree BuildEyeTree(
            VRCAvatarDescriptor avatar,
            AnimatorController controller,
            EyeLookInfo info,
            bool isLeft,
            string blendParamX,
            string blendParamY,
            string outputDir,
            string namePrefix,
            VrcftGenerationResult result)
        {
            Transform bone = isLeft ? info.LeftEyeBone : info.RightEyeBone;
            string path = AnimationUtility.CalculateTransformPath(bone, avatar.transform);

            // Straight = ボーンの現在の localRotation の euler (正規化)
            Vector3 straight = VrcftAssetUtility.NormalizeEuler(bone.localRotation.eulerAngles);

            // RotationStates は「相対回転」。クリップ値 = (現在のlocalRotation × RotationState回転) の euler。
            Quaternion baseRot = bone.localRotation;
            Vector3 up = EulerOf(baseRot * (isLeft ? info.LeftUp : info.RightUp));
            Vector3 down = EulerOf(baseRot * (isLeft ? info.LeftDown : info.RightDown));
            Vector3 left = EulerOf(baseRot * (isLeft ? info.LeftLeft : info.RightLeft));
            Vector3 right = EulerOf(baseRot * (isLeft ? info.LeftRight : info.RightRight));

            var straightClip = EyeClip(outputDir, namePrefix + "_Straight", path, straight, result);
            var upClip = EyeClip(outputDir, namePrefix + "_Up", path, up, result);
            var downClip = EyeClip(outputDir, namePrefix + "_Down", path, down, result);
            var leftClip = EyeClip(outputDir, namePrefix + "_Left", path, left, result);
            var rightClip = EyeClip(outputDir, namePrefix + "_Right", path, right, result);

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

        private static Vector3 EulerOf(Quaternion q)
        {
            return VrcftAssetUtility.NormalizeEuler(q.eulerAngles);
        }

        private static AnimationClip EyeClip(string outputDir, string name, string bonePath, Vector3 euler, VrcftGenerationResult result)
        {
            var clip = new AnimationClip { name = name };
            VrcftAssetUtility.AddTransformEuler(clip, bonePath, euler);
            string p = outputDir + "/Animations/EyeLook/" + name + ".anim";
            UnityEditor.AssetDatabase.CreateAsset(clip, p);
            result.generatedClipPaths.Add(p);
            return clip;
        }
    }
}
