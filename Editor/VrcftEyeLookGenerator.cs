using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// EyeLook (視線) レイヤーの構築。Descriptor の RotationStates を Humanoid muscle 値へ変換し、
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
            VRCAvatarDescriptor avatar,
            AnimatorController controller,
            VrcftAutoSetupSettings settings,
            VrcftDetectionReport report,
            string clipOutputDir,
            VrcftGenerationResult result)
        {
            var info = report.EyeLook;
            var sampler = HumanoidEyePoseSampler.Create(avatar, info);
            if (sampler == null)
            {
                Debug.LogWarning("[VRCFT Auto Setup] EyeLook は Humanoid Avatar が必要なため生成をスキップしました。");
                return null;
            }

            string SmoothName(string param) => settings.enableSmoothing ? "OSCm/Smooth/" + param : param;
            const string blendSet = "OSCm/BlendSet";

            string leftX = SmoothName("FT/v2/EyeLeftX");
            string rightX = SmoothName("FT/v2/EyeRightX");
            string eyeY = SmoothName("FT/v2/EyeY");

            var leftTree = BuildEyeTree(controller, info, isLeft: true,
                blendParamX: leftX, blendParamY: eyeY, clipOutputDir, "EyeLeft", sampler, result);
            var rightTree = BuildEyeTree(controller, info, isLeft: false,
                blendParamX: rightX, blendParamY: eyeY, clipOutputDir, "EyeRight", sampler, result);

            var master = VrcftAssetUtility.CreateBlendTree(controller, "EyeLook_Master", BlendTreeType.Direct);
            master.children = new[]
            {
                new ChildMotion { motion = leftTree, directBlendParameter = blendSet, timeScale = 1f },
                new ChildMotion { motion = rightTree, directBlendParameter = blendSet, timeScale = 1f },
            };
            return master;
        }

        private static BlendTree BuildEyeTree(
            AnimatorController controller,
            EyeLookInfo info,
            bool isLeft,
            string blendParamX,
            string blendParamY,
            string clipOutputDir,
            string namePrefix,
            HumanoidEyePoseSampler sampler,
            VrcftGenerationResult result)
        {
            // RotationStates は目ボーンの基準姿勢からの相対回転なので、Humanoid pose に通して muscle 値へ変換する。
            var straight = sampler.Sample(isLeft, Quaternion.identity);
            var up = sampler.Sample(isLeft, isLeft ? info.LeftUp : info.RightUp);
            var down = sampler.Sample(isLeft, isLeft ? info.LeftDown : info.RightDown);
            var left = sampler.Sample(isLeft, isLeft ? info.LeftLeft : info.RightLeft);
            var right = sampler.Sample(isLeft, isLeft ? info.LeftRight : info.RightRight);

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
            public float DownUp;
            public float InOut;
        }

        /// <summary>
        /// Descriptor の目ボーン相対回転を、対象アバターの Humanoid 定義を通した muscle 値として採取する。
        /// </summary>
        private sealed class HumanoidEyePoseSampler
        {
            private readonly HumanPoseHandler _poseHandler;
            private readonly Transform _leftEyeBone;
            private readonly Transform _rightEyeBone;
            private readonly Quaternion _leftBaseRotation;
            private readonly Quaternion _rightBaseRotation;
            private readonly int _leftDownUpIndex;
            private readonly int _leftInOutIndex;
            private readonly int _rightDownUpIndex;
            private readonly int _rightInOutIndex;
            private HumanPose _pose;

            private HumanoidEyePoseSampler(
                HumanPoseHandler poseHandler,
                EyeLookInfo info,
                int leftDownUpIndex,
                int leftInOutIndex,
                int rightDownUpIndex,
                int rightInOutIndex)
            {
                _poseHandler = poseHandler;
                _leftEyeBone = info.LeftEyeBone;
                _rightEyeBone = info.RightEyeBone;
                _leftBaseRotation = _leftEyeBone.localRotation;
                _rightBaseRotation = _rightEyeBone.localRotation;
                _leftDownUpIndex = leftDownUpIndex;
                _leftInOutIndex = leftInOutIndex;
                _rightDownUpIndex = rightDownUpIndex;
                _rightInOutIndex = rightInOutIndex;
                _pose = new HumanPose();
            }

            /// <summary>
            /// Humanoid Avatar と目用 muscle が取得できる場合だけサンプラーを作成する。
            /// </summary>
            public static HumanoidEyePoseSampler Create(VRCAvatarDescriptor avatar, EyeLookInfo info)
            {
                var animator = avatar != null ? avatar.GetComponent<Animator>() : null;
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return null;
                if (info.LeftEyeBone == null || info.RightEyeBone == null) return null;

                int leftDownUp = MuscleIndex(LeftEyeDownUp);
                int leftInOut = MuscleIndex(LeftEyeInOut);
                int rightDownUp = MuscleIndex(RightEyeDownUp);
                int rightInOut = MuscleIndex(RightEyeInOut);
                if (leftDownUp < 0 || leftInOut < 0 || rightDownUp < 0 || rightInOut < 0) return null;

                return new HumanoidEyePoseSampler(
                    new HumanPoseHandler(animator.avatar, avatar.transform),
                    info,
                    leftDownUp,
                    leftInOut,
                    rightDownUp,
                    rightInOut);
            }

            /// <summary>
            /// 片目だけ RotationState を適用し、Humanoid pose から該当する目 muscle を読み取る。
            /// </summary>
            public HumanoidEyePose Sample(bool isLeft, Quaternion relativeRotation)
            {
                Quaternion leftOriginal = _leftEyeBone.localRotation;
                Quaternion rightOriginal = _rightEyeBone.localRotation;
                try
                {
                    _leftEyeBone.localRotation = _leftBaseRotation;
                    _rightEyeBone.localRotation = _rightBaseRotation;
                    if (isLeft)
                    {
                        _leftEyeBone.localRotation = _leftBaseRotation * relativeRotation;
                    }
                    else
                    {
                        _rightEyeBone.localRotation = _rightBaseRotation * relativeRotation;
                    }

                    _poseHandler.GetHumanPose(ref _pose);
                    return new HumanoidEyePose
                    {
                        DownUp = Mathf.Clamp(_pose.muscles[isLeft ? _leftDownUpIndex : _rightDownUpIndex], -1f, 1f),
                        InOut = Mathf.Clamp(_pose.muscles[isLeft ? _leftInOutIndex : _rightInOutIndex], -1f, 1f),
                    };
                }
                finally
                {
                    _leftEyeBone.localRotation = leftOriginal;
                    _rightEyeBone.localRotation = rightOriginal;
                }
            }

            private static int MuscleIndex(string muscleName)
            {
                var names = HumanTrait.MuscleName;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == muscleName) return i;
                }
                return -1;
            }
        }
    }
}
