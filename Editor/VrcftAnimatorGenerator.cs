using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// FX/Additive AnimatorController + AnimationClip の生成本体。
    /// </summary>
    public static class VrcftAnimatorGenerator
    {
        private const string BlendSet = "OSCm/BlendSet";
        private const string SmoothPrefix = "OSCm/Smooth/";
        private const string LocalSmoothing = "OSCm/Local/FloatSmoothing";
        private const string RemoteSmoothing = "OSCm/Remote/FloatSmoothing";

        /// <summary>
        /// 生成対象パラメーター (Eye系特別扱いを含む)。
        /// </summary>
        public sealed class Target
        {
            public ParameterMatch Match;
            public VrcftCatalogEntry Entry;
            public int Bits;
            public bool HasShapes;     // シェイプマッチがあるか (駆動レイヤー対象)
            public bool IsEyeSpecial;  // EyeLook用に追加されたシェイプ未検知Eye系
        }

        public static VrcftGenerationResult Generate(VRCAvatarDescriptor avatar, VrcftDetectionReport report, VrcftAutoSetupSettings settings)
        {
            var result = new VrcftGenerationResult();

            string baseFolder = settings.outputFolder.Replace('\\', '/').TrimEnd('/');
            string outputDir = baseFolder + "/" + Sanitize(report.AvatarName);
            result.outputDir = outputDir;

            // 既存削除 → 再作成
            VrcftAssetUtility.RecreateFolder(outputDir);
            VrcftAssetUtility.EnsureFolder(outputDir + "/Animations");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Animations/Binary");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Animations/Smooth");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Additive");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Additive/Animations");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Additive/Animations/Binary");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Additive/Animations/Smooth");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Additive/Animations/EyeLook");
            VrcftAssetUtility.EnsureFolder(outputDir + "/Additive/Masks");

            string controllerPath = outputDir + "/FX_FaceTracking.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // 既定で1つ空レイヤーができるので除去
            while (controller.layers.Length > 0)
            {
                controller.RemoveLayer(0);
            }

            result.fxController = controller;

            // ---------- 対象パラメーターを決定 ----------
            var targets = BuildTargets(report, settings);

            // ---------- パラメーター宣言 ----------
            DeclareParameters(controller, settings, targets, result);

            // ---------- レイヤー構築 ----------
            if (settings.useBinary)
            {
                BuildBinaryDecodeLayer(controller, settings, targets, outputDir + "/Animations/Binary", result);
            }

            if (settings.enableSmoothing)
            {
                BuildSmoothingLayer(controller, settings, targets, outputDir + "/Animations/Smooth");
            }

            BuildDrivingLayer(avatar, controller, settings, targets, result);

            bool eyeLookEnabled = settings.enableEyeLook
                && settings.eyeLookMode == EyeLookMode.HumanoidMuscleFixed;
            if (eyeLookEnabled)
            {
                BuildAdditiveEyeLookController(settings, targets, result);
            }

            // 制御対象レイヤーの index を名前から解決 (LayerControl 用)。
            int eyeDrivingIndex = FindLayerIndex(controller, "VRCFT_Driving_Eye");
            int lipDrivingIndex = FindLayerIndex(controller, "VRCFT_Driving_Lip");

            BuildControlLayer(controller, settings, "VRCFT_Control", "EyeTrackingActive", trackingEyes: true,
                onWeightLayers: ToList(eyeDrivingIndex), clipOutputDir: outputDir + "/Animations", applyTrackingControl: result.additiveController == null);
            BuildControlLayer(controller, settings, "VRCFT_LipControl", "LipTrackingActive", trackingEyes: false,
                onWeightLayers: ToList(lipDrivingIndex), clipOutputDir: outputDir + "/Animations");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        // ============================================================
        // 対象パラメーター構築
        // ============================================================
        private static List<Target> BuildTargets(VrcftDetectionReport report, VrcftAutoSetupSettings settings)
        {
            var targets = new List<Target>();
            var byName = new Dictionary<string, Target>();

            foreach (var m in report.Matches)
            {
                if (!m.Enabled || !m.HasAnyMatch) continue;
                if ((int)m.Entry.Preset > (int)settings.preset) continue;

                var t = new Target
                {
                    Match = m,
                    Entry = m.Entry,
                    Bits = settings.ResolveBits(m.Entry, m.Bits),
                    HasShapes = true,
                    IsEyeSpecial = false,
                };
                targets.Add(t);
                byName[m.Entry.ParameterName] = t;
            }

            // Eye系特別扱い: シェイプ未検知でも EyeLook で使うものを対象に含める
            bool eyeLookOn = settings.enableEyeLook
                && settings.eyeLookMode == EyeLookMode.HumanoidMuscleFixed;
            if (eyeLookOn)
            {
                foreach (var pname in new[] { "EyeLeftX", "EyeRightX", "EyeY" })
                {
                    if (byName.ContainsKey(pname)) continue;
                    var m = report.Matches.FirstOrDefault(x => x.Entry.ParameterName == pname);
                    if (m == null) continue;
                    var t = new Target
                    {
                        Match = m,
                        Entry = m.Entry,
                        Bits = settings.ResolveBits(m.Entry, m.Bits),
                        HasShapes = false,
                        IsEyeSpecial = true,
                    };
                    targets.Add(t);
                    byName[pname] = t;
                }
            }

            return targets;
        }

        // ============================================================
        // パラメーター宣言
        // ============================================================
        private static void DeclareParameters(AnimatorController controller, VrcftAutoSetupSettings settings, List<Target> targets, VrcftGenerationResult result)
        {
            DeclareControllerParameters(controller, settings, targets);

            // ---------- syncedParameters 記録 ----------
            foreach (var t in targets)
            {
                string full = t.Entry.FullName;
                if (settings.useBinary)
                {
                    // DefaultValue (例: EyeLid=0.75) をビット列に量子化して初期値に反映する
                    // (全bit=false だと同期初期値0=閉眼相当になるため)
                    int quantizedDefault = Mathf.RoundToInt(Mathf.Clamp01(t.Entry.DefaultValue) * ((1 << t.Bits) - 1));
                    for (int i = 0; i < t.Bits; i++)
                    {
                        int weight = 1 << i;
                        float bitDefault = ((quantizedDefault >> i) & 1) == 1 ? 1f : 0f;
                        result.syncedParameters.Add(new VrcftSyncedParameter($"{full}{weight}", VrcftParameterKind.Bool, bitDefault, saved: false));
                    }
                    if (t.Entry.TwoSided)
                    {
                        result.syncedParameters.Add(new VrcftSyncedParameter($"{full}Negative", VrcftParameterKind.Bool, 0f, saved: false));
                    }
                }
                else
                {
                    result.syncedParameters.Add(new VrcftSyncedParameter(full, VrcftParameterKind.Float, t.Entry.DefaultValue, saved: false));
                }
            }

            result.syncedParameters.Add(new VrcftSyncedParameter("EyeTrackingActive", VrcftParameterKind.Bool, 0f, saved: true));
            result.syncedParameters.Add(new VrcftSyncedParameter("LipTrackingActive", VrcftParameterKind.Bool, 0f, saved: true));
            result.syncedParameters.Add(new VrcftSyncedParameter(LocalSmoothing, VrcftParameterKind.Float, settings.localSmoothness, saved: true, localOnly: true));
        }

        /// <summary>
        /// AnimatorController 内で参照するローカルパラメーターを宣言する。
        /// </summary>
        private static void DeclareControllerParameters(AnimatorController controller, VrcftAutoSetupSettings settings, List<Target> targets)
        {
            // 共通定数 / スムージング設定
            VrcftAssetUtility.CheckAndCreateParameter(controller, BlendSet, AnimatorControllerParameterType.Float, defaultFloat: 1f);
            VrcftAssetUtility.CheckAndCreateParameter(controller, LocalSmoothing, AnimatorControllerParameterType.Float, defaultFloat: settings.localSmoothness);
            VrcftAssetUtility.CheckAndCreateParameter(controller, RemoteSmoothing, AnimatorControllerParameterType.Float, defaultFloat: settings.remoteSmoothness);

            // 制御 Bool
            VrcftAssetUtility.CheckAndCreateParameter(controller, "IsLocal", AnimatorControllerParameterType.Bool);
            VrcftAssetUtility.CheckAndCreateParameter(controller, "EyeTrackingActive", AnimatorControllerParameterType.Bool);
            VrcftAssetUtility.CheckAndCreateParameter(controller, "LipTrackingActive", AnimatorControllerParameterType.Bool);

            foreach (var t in targets)
            {
                string full = t.Entry.FullName;

                // 受信値 / デコード先 FT/v2/X (Float)
                VrcftAssetUtility.CheckAndCreateParameter(controller, full, AnimatorControllerParameterType.Float,
                    defaultFloat: t.Entry.DefaultValue);

                if (settings.useBinary)
                {
                    for (int i = 0; i < t.Bits; i++)
                    {
                        int weight = 1 << i;
                        VrcftAssetUtility.CheckAndCreateParameter(controller, $"{full}{weight}", AnimatorControllerParameterType.Float);
                    }
                    if (t.Entry.TwoSided)
                    {
                        VrcftAssetUtility.CheckAndCreateParameter(controller, $"{full}Negative", AnimatorControllerParameterType.Float);
                    }
                }

                if (settings.enableSmoothing)
                {
                    VrcftAssetUtility.CheckAndCreateParameter(controller, SmoothPrefix + full, AnimatorControllerParameterType.Float,
                        defaultFloat: t.Entry.DefaultValue);
                }
            }
        }

        // ============================================================
        // Layer 1: Binary デコード
        // ============================================================
        private static void BuildBinaryDecodeLayer(AnimatorController controller, VrcftAutoSetupSettings settings, List<Target> targets, string clipOutputDir, VrcftGenerationResult result)
        {
            var master = VrcftAssetUtility.CreateBlendTree(controller, "BinaryDecode_Master", BlendTreeType.Direct);
            var children = new List<ChildMotion>();

            foreach (var t in targets)
            {
                var tree = BuildDecodeTree(controller, t, clipOutputDir, result);
                children.Add(new ChildMotion { motion = tree, directBlendParameter = BlendSet, timeScale = 1f });
            }
            master.children = children.ToArray();

            AddSingleStateLayer(controller, "VRCFT_BinaryDecode", master, settings.writeDefaults);
        }

        private static BlendTree BuildDecodeTree(AnimatorController controller, Target t, string clipOutputDir, VrcftGenerationResult result)
        {
            string full = t.Entry.FullName;
            string fileBase = full.Replace('/', '_');

            // ゼロクリップ (このパラメーター用、Pos/Neg/ビット間で共有)
            var zeroClip = CreateAnimatorClip(clipOutputDir.TrimEnd('/') + "/" + fileBase + "_Zero.anim",
                fileBase + "_Zero", full, 0f, result);

            BlendTree posTree = BuildBitTree(controller, t, full, fileBase, zeroClip, negative: false, clipOutputDir, result);

            if (!t.Entry.TwoSided)
            {
                return posTree;
            }

            BlendTree negTree = BuildBitTree(controller, t, full, fileBase, zeroClip, negative: true, clipOutputDir, result);

            var sel = VrcftAssetUtility.CreateBlendTree(controller, fileBase + "_Decode", BlendTreeType.Simple1D);
            sel.blendParameter = $"{full}Negative";
            sel.children = new[]
            {
                new ChildMotion { motion = posTree, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = negTree, threshold = 1f, timeScale = 1f },
            };
            return sel;
        }

        private static BlendTree BuildBitTree(AnimatorController controller, Target t, string full, string fileBase, AnimationClip zeroClip, bool negative, string clipOutputDir, VrcftGenerationResult result)
        {
            int n = t.Bits;
            float denom = (1 << n) - 1; // 2^N - 1
            var dir = VrcftAssetUtility.CreateBlendTree(controller, fileBase + (negative ? "_Neg" : "_Pos"), BlendTreeType.Direct);

            var children = new List<ChildMotion>();
            for (int i = 0; i < n; i++)
            {
                int weight = 1 << i;
                float value = (negative ? -1f : 1f) * weight / denom;

                var oneClip = CreateAnimatorClip(
                    clipOutputDir.TrimEnd('/') + $"/{fileBase}_Bit{weight}_{(negative ? "Neg" : "Pos")}.anim",
                    $"{fileBase}_Bit{weight}_{(negative ? "Neg" : "Pos")}", full, value, result);

                var bitTree = VrcftAssetUtility.CreateBlendTree(controller, $"{fileBase}_Bit{weight}{(negative ? "N" : "P")}", BlendTreeType.Simple1D);
                bitTree.blendParameter = $"{full}{weight}";
                bitTree.children = new[]
                {
                    new ChildMotion { motion = zeroClip, threshold = 0f, timeScale = 1f },
                    new ChildMotion { motion = oneClip, threshold = 1f, timeScale = 1f },
                };

                children.Add(new ChildMotion { motion = bitTree, directBlendParameter = BlendSet, timeScale = 1f });
            }
            dir.children = children.ToArray();
            return dir;
        }

        // ============================================================
        // Layer 2: スムージング
        // ============================================================
        private static void BuildSmoothingLayer(AnimatorController controller, VrcftAutoSetupSettings settings, List<Target> targets, string clipOutputDir)
        {
            var layer = new AnimatorControllerLayer
            {
                name = "VRCFT_Smoothing",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine { name = "VRCFT_Smoothing", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);

            var sm = layer.stateMachine;
            var clipCache = new Dictionary<string, AnimationClip>();

            var localTree = BuildSmoothMaster(controller, settings, targets, LocalSmoothing, "Smooth_Local_Master", clipOutputDir, clipCache);
            var remoteTree = BuildSmoothMaster(controller, settings, targets, RemoteSmoothing, "Smooth_Remote_Master", clipOutputDir, clipCache);

            var localState = sm.AddState("Local");
            localState.motion = localTree;
            localState.writeDefaultValues = settings.writeDefaults;

            var remoteState = sm.AddState("Remote");
            remoteState.motion = remoteTree;
            remoteState.writeDefaultValues = settings.writeDefaults;

            sm.defaultState = localState;

            var toRemote = localState.AddTransition(remoteState);
            toRemote.hasExitTime = false;
            toRemote.duration = 0f;
            toRemote.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsLocal");

            var toLocal = remoteState.AddTransition(localState);
            toLocal.hasExitTime = false;
            toLocal.duration = 0f;
            toLocal.AddCondition(AnimatorConditionMode.If, 0f, "IsLocal");
        }

        private static BlendTree BuildSmoothMaster(AnimatorController controller, VrcftAutoSetupSettings settings, List<Target> targets, string smoothingParam, string name, string clipOutputDir, Dictionary<string, AnimationClip> clipCache)
        {
            var master = VrcftAssetUtility.CreateBlendTree(controller, name, BlendTreeType.Direct);
            var children = new List<ChildMotion>();
            foreach (var t in targets)
            {
                var tree = BuildSmoothTree(controller, t, smoothingParam, clipOutputDir, clipCache);
                children.Add(new ChildMotion { motion = tree, directBlendParameter = BlendSet, timeScale = 1f });
            }
            master.children = children.ToArray();
            return master;
        }

        private static BlendTree BuildSmoothTree(AnimatorController controller, Target t, string smoothingParam, string clipOutputDir, Dictionary<string, AnimationClip> clipCache)
        {
            string full = t.Entry.FullName;
            string fileBase = full.Replace('/', '_');

            // クリップは Smooth/ に2個 (値 -1, +1)。Generate前に生成済みのものを使い回すため都度生成しないよう注意。
            // ここではアセットがまだ無い可能性があるので生成し、共有のため辞書で管理。
            var negClip = GetOrCreateSmoothClip(clipOutputDir, clipCache, fileBase, full, -1f);
            var posClip = GetOrCreateSmoothClip(clipOutputDir, clipCache, fileBase, full, +1f);

            var input = VrcftAssetUtility.CreateBlendTree(controller, fileBase + "_SmIn", BlendTreeType.Simple1D);
            input.blendParameter = full;
            input.children = new[]
            {
                new ChildMotion { motion = negClip, threshold = -1f, timeScale = 1f },
                new ChildMotion { motion = posClip, threshold = 1f, timeScale = 1f },
            };

            var driver = VrcftAssetUtility.CreateBlendTree(controller, fileBase + "_SmDr", BlendTreeType.Simple1D);
            driver.blendParameter = SmoothPrefix + full;
            driver.children = new[]
            {
                new ChildMotion { motion = negClip, threshold = -1f, timeScale = 1f },
                new ChildMotion { motion = posClip, threshold = 1f, timeScale = 1f },
            };

            var root = VrcftAssetUtility.CreateBlendTree(controller, fileBase + "_Smooth", BlendTreeType.Simple1D);
            root.blendParameter = smoothingParam;
            root.children = new[]
            {
                new ChildMotion { motion = input, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = driver, threshold = 1f, timeScale = 1f },
            };
            return root;
        }

        private static AnimationClip GetOrCreateSmoothClip(string clipOutputDir, Dictionary<string, AnimationClip> clipCache, string fileBase, string full, float value)
        {
            string key = fileBase + "_" + (value < 0 ? "Neg" : "Pos");
            if (clipCache.TryGetValue(key, out var existing)) return existing;

            string path = clipOutputDir.TrimEnd('/') + "/" + key + ".anim";
            var clip = new AnimationClip { name = key };
            VrcftAssetUtility.AddAnimatorParameterConstant(clip, SmoothPrefix + full, value);
            AssetDatabase.CreateAsset(clip, path);
            clipCache[key] = clip;
            return clip;
        }

        // ============================================================
        // Layer 3: 駆動
        // ============================================================
        private static void BuildDrivingLayer(VRCAvatarDescriptor avatar, AnimatorController controller, VrcftAutoSetupSettings settings, List<Target> targets, VrcftGenerationResult result)
        {
            // Eye系 (ParameterName が "Eye"/"Pupil" で始まる) と それ以外 (Lip系) で2レイヤーに分割。
            // トラッキングOFF時に Eye と Lip を独立して止められるようにする。
            BuildDrivingLayerFor(avatar, controller, settings, targets, result,
                "VRCFT_Driving_Eye", IsEyeParameter);
            BuildDrivingLayerFor(avatar, controller, settings, targets, result,
                "VRCFT_Driving_Lip", n => !IsEyeParameter(n));
        }

        private static bool IsEyeParameter(string parameterName)
        {
            return parameterName.StartsWith("Eye") || parameterName.StartsWith("Pupil");
        }

        private static void BuildDrivingLayerFor(VRCAvatarDescriptor avatar, AnimatorController controller, VrcftAutoSetupSettings settings, List<Target> targets, VrcftGenerationResult result, string layerName, System.Func<string, bool> include)
        {
            var master = VrcftAssetUtility.CreateBlendTree(controller, layerName + "_Master", BlendTreeType.Direct);
            var children = new List<ChildMotion>();

            foreach (var t in targets)
            {
                if (!t.HasShapes) continue;
                if (!include(t.Entry.ParameterName)) continue;
                var tree = BuildDriveTree(avatar, controller, settings, t, result);
                if (tree == null) continue;
                children.Add(new ChildMotion { motion = tree, directBlendParameter = BlendSet, timeScale = 1f });
            }
            master.children = children.ToArray();

            // defaultWeight = 0: 制御レイヤーの LayerControl がONにするまで動かない。
            AddSingleStateLayer(controller, layerName, master, settings.writeDefaults, defaultWeight: 0f);
        }

        private static Motion BuildDriveTree(VRCAvatarDescriptor avatar, AnimatorController controller, VrcftAutoSetupSettings settings, Target t, VrcftGenerationResult result)
        {
            string full = t.Entry.FullName;
            string fileBase = full.Replace('/', '_');
            string drive = settings.enableSmoothing ? SmoothPrefix + full : full;

            // 全対象シェイプ (この駆動ツリーが触る全スロット) を集める
            var allSlots = t.Match.SlotMatches;

            var tree = VrcftAssetUtility.CreateBlendTree(controller, fileBase + "_Drive", BlendTreeType.Simple1D);
            tree.blendParameter = drive;

            if (t.Entry.IsEyeLid)
            {
                var blink = allSlots.Where(s => s.SlotKind == "blink").ToList();
                var wide = allSlots.Where(s => s.SlotKind == "wide").ToList();

                var closedClip = NewClip(result, t, "_Closed");
                foreach (var s in blink) VrcftAssetUtility.AddBlendShapeConstant(closedClip, Path(avatar, s.Mesh), s.BlendShapeName, 100f);
                foreach (var s in wide) VrcftAssetUtility.AddBlendShapeConstant(closedClip, Path(avatar, s.Mesh), s.BlendShapeName, 0f);
                FinishClip(closedClip, t, "_Closed", result);

                var neutralClip = NewClip(result, t, "_Neutral");
                foreach (var s in allSlots) VrcftAssetUtility.AddBlendShapeConstant(neutralClip, Path(avatar, s.Mesh), s.BlendShapeName, 0f);
                FinishClip(neutralClip, t, "_Neutral", result);

                if (wide.Count > 0)
                {
                    var wideClip = NewClip(result, t, "_Wide");
                    foreach (var s in wide) VrcftAssetUtility.AddBlendShapeConstant(wideClip, Path(avatar, s.Mesh), s.BlendShapeName, 100f);
                    foreach (var s in blink) VrcftAssetUtility.AddBlendShapeConstant(wideClip, Path(avatar, s.Mesh), s.BlendShapeName, 0f);
                    FinishClip(wideClip, t, "_Wide", result);

                    tree.children = new[]
                    {
                        new ChildMotion { motion = closedClip, threshold = 0f, timeScale = 1f },
                        new ChildMotion { motion = neutralClip, threshold = 0.75f, timeScale = 1f },
                        new ChildMotion { motion = wideClip, threshold = 1f, timeScale = 1f },
                    };
                }
                else
                {
                    tree.children = new[]
                    {
                        new ChildMotion { motion = closedClip, threshold = 0f, timeScale = 1f },
                        new ChildMotion { motion = neutralClip, threshold = 0.75f, timeScale = 1f },
                    };
                }
                return tree;
            }

            if (t.Entry.TwoSided)
            {
                var posSlots = allSlots.Where(s => s.SlotKind == "positive").ToList();
                var negSlots = allSlots.Where(s => s.SlotKind == "negative").ToList();

                var negClip = NewClip(result, t, "_Neg");
                foreach (var s in negSlots) VrcftAssetUtility.AddBlendShapeConstant(negClip, Path(avatar, s.Mesh), s.BlendShapeName, 100f);
                foreach (var s in posSlots) VrcftAssetUtility.AddBlendShapeConstant(negClip, Path(avatar, s.Mesh), s.BlendShapeName, 0f);
                FinishClip(negClip, t, "_Neg", result);

                var neutralClip = NewClip(result, t, "_Neutral");
                foreach (var s in allSlots) VrcftAssetUtility.AddBlendShapeConstant(neutralClip, Path(avatar, s.Mesh), s.BlendShapeName, 0f);
                FinishClip(neutralClip, t, "_Neutral", result);

                var posClip = NewClip(result, t, "_Max");
                foreach (var s in posSlots) VrcftAssetUtility.AddBlendShapeConstant(posClip, Path(avatar, s.Mesh), s.BlendShapeName, 100f);
                foreach (var s in negSlots) VrcftAssetUtility.AddBlendShapeConstant(posClip, Path(avatar, s.Mesh), s.BlendShapeName, 0f);
                FinishClip(posClip, t, "_Max", result);

                tree.children = new[]
                {
                    new ChildMotion { motion = negClip, threshold = -1f, timeScale = 1f },
                    new ChildMotion { motion = neutralClip, threshold = 0f, timeScale = 1f },
                    new ChildMotion { motion = posClip, threshold = 1f, timeScale = 1f },
                };
                return tree;
            }

            // 単方向
            var minClip = NewClip(result, t, "_Min");
            foreach (var s in allSlots) VrcftAssetUtility.AddBlendShapeConstant(minClip, Path(avatar, s.Mesh), s.BlendShapeName, 0f);
            FinishClip(minClip, t, "_Min", result);

            var maxClip = NewClip(result, t, "_Max");
            foreach (var s in allSlots) VrcftAssetUtility.AddBlendShapeConstant(maxClip, Path(avatar, s.Mesh), s.BlendShapeName, 100f);
            FinishClip(maxClip, t, "_Max", result);

            tree.children = new[]
            {
                new ChildMotion { motion = minClip, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = maxClip, threshold = 1f, timeScale = 1f },
            };
            return tree;
        }

        // ============================================================
        // Additive: Humanoid EyeLook
        // ============================================================
        /// <summary>
        /// Humanoid の目 muscle だけを駆動する Additive 用 AnimatorController を生成する。
        /// </summary>
        private static void BuildAdditiveEyeLookController(VrcftAutoSetupSettings settings, List<Target> targets, VrcftGenerationResult result)
        {
            var eyeTargets = targets.Where(t => IsEyeLookRotationParameter(t.Entry.ParameterName)).ToList();
            if (eyeTargets.Count == 0) return;

            string controllerPath = result.outputDir + "/Additive/Additive_EyeTracking.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            while (controller.layers.Length > 0)
            {
                controller.RemoveLayer(0);
            }

            DeclareControllerParameters(controller, settings, eyeTargets);

            var eyeLookMotion = VrcftEyeLookGenerator.BuildMotion(
                controller,
                settings,
                result.outputDir + "/Additive/Animations/EyeLook",
                result);
            if (eyeLookMotion == null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
                return;
            }

            if (settings.useBinary)
            {
                BuildBinaryDecodeLayer(controller, settings, eyeTargets, result.outputDir + "/Additive/Animations/Binary", result);
            }

            if (settings.enableSmoothing)
            {
                BuildSmoothingLayer(controller, settings, eyeTargets, result.outputDir + "/Additive/Animations/Smooth");
            }

            var mask = CreateHeadOnlyMask(result.outputDir + "/Additive/Masks/VRCFT_HeadOnly.mask");
            AddEyeTrackingLayer(controller, settings, eyeLookMotion, mask, result.outputDir + "/Additive/Animations/EyeLook");
            result.additiveController = controller;

            EditorUtility.SetDirty(controller);
        }

        /// <summary>
        /// Additive 側へ複製する EyeLook 用のFTパラメーターか判定する。
        /// </summary>
        private static bool IsEyeLookRotationParameter(string parameterName)
        {
            return parameterName == "EyeLeftX" || parameterName == "EyeRightX" || parameterName == "EyeY";
        }

        /// <summary>
        /// 目 muscle の影響範囲を頭部だけに制限する AvatarMask を作成する。
        /// </summary>
        private static AvatarMask CreateHeadOnlyMask(string path)
        {
            var mask = new AvatarMask { name = "VRCFT_HeadOnly" };
            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }
            // Humanoid の目 muscle は Head パートに属するため、Additive 側の影響範囲を頭部だけに限定する。
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            AssetDatabase.CreateAsset(mask, path);
            return mask;
        }

        /// <summary>
        /// Additive controller に EyeTrackingActive で Native/VRCFT を切り替える目回転レイヤーを追加する。
        /// </summary>
        private static void AddEyeTrackingLayer(AnimatorController controller, VrcftAutoSetupSettings settings, Motion eyeLookMotion, AvatarMask mask, string clipOutputDir)
        {
            var layer = new AnimatorControllerLayer
            {
                name = "VRCFT_EyeLook_Additive",
                defaultWeight = 1f,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "VRCFT_EyeLook_Additive", hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);

            var nativeState = layer.stateMachine.AddState("Native Eye Tracking");
            nativeState.writeDefaultValues = settings.writeDefaults;
            var nativeTracking = nativeState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            nativeTracking.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Tracking;

            var vrcftState = layer.stateMachine.AddState("VRCFT Eye Tracking");
            vrcftState.motion = eyeLookMotion;
            vrcftState.writeDefaultValues = settings.writeDefaults;
            var vrcftTracking = vrcftState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            vrcftTracking.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Animation;

            ConfigureBoolDrivenAnyState(layer.stateMachine, settings, "EyeTrackingActive", offState: nativeState, onState: vrcftState, clipOutputDir);
        }

        // ============================================================
        // Layer 5/6: 制御 (TrackingControl)
        // ============================================================
        private static void BuildControlLayer(AnimatorController controller, VrcftAutoSetupSettings settings, string layerName, string boolParam, bool trackingEyes, List<int> onWeightLayers, string clipOutputDir, bool applyTrackingControl = true)
        {
            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);

            var sm = layer.stateMachine;

            string offName = trackingEyes ? "FT_Off" : "Lip_Off";
            string onName = trackingEyes ? "FT_On" : "Lip_On";

            var offState = sm.AddState(offName);
            offState.writeDefaultValues = settings.writeDefaults;
            var onState = sm.AddState(onName);
            onState.writeDefaultValues = settings.writeDefaults;

            if (applyTrackingControl)
            {
                // Additive 側が目 tracking を管理する場合、FX 側ではレイヤー weight の切替だけに留める。
                var offTc = offState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                var onTc = onState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                if (trackingEyes)
                {
                    offTc.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    onTc.trackingEyes = VRC_AnimatorTrackingControl.TrackingType.Animation;
                }
                else
                {
                    offTc.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.Tracking;
                    onTc.trackingMouth = VRC_AnimatorTrackingControl.TrackingType.Animation;
                }
            }

            // LayerControl: 対象レイヤーの weight を ON=1 / OFF=0 に切替。
            // VRCAnimatorLayerControl は1 behaviour で1レイヤーのみ制御するため、レイヤー数だけ付与する。
            if (onWeightLayers != null)
            {
                foreach (var layerIndex in onWeightLayers)
                {
                    if (layerIndex < 0) continue;
                    AddLayerControl(onState, layerIndex, 1f);
                    AddLayerControl(offState, layerIndex, 0f);
                }
            }

            ConfigureBoolDrivenAnyState(sm, settings, boolParam, offState, onState, clipOutputDir);
        }

        /// <summary>
        /// Bool パラメーターのロード済み初期値に従って、Entry 直後から正しい制御ステートへ入る構造を作る。
        /// </summary>
        private static void ConfigureBoolDrivenAnyState(AnimatorStateMachine stateMachine, VrcftAutoSetupSettings settings, string boolParam, AnimatorState offState, AnimatorState onState, string clipOutputDir)
        {
            var emptyClip = CreateEmptyControlClip(clipOutputDir, stateMachine.name);
            FillEmptyMotion(offState, emptyClip);
            FillEmptyMotion(onState, emptyClip);

            // TrackingControl は起動直後に OFF 側を経由すると状態が残ることがあるため、Entry は副作用のないダミーにする。
            var entryState = stateMachine.AddState("Entry");
            entryState.motion = emptyClip;
            entryState.writeDefaultValues = settings.writeDefaults;
            stateMachine.defaultState = entryState;

            AddAnyStateTransition(stateMachine, onState, AnimatorConditionMode.If, boolParam);
            AddAnyStateTransition(stateMachine, offState, AnimatorConditionMode.IfNot, boolParam);
        }

        /// <summary>
        /// Motion 未設定の制御 State に、何も書き込まない AnimationClip を割り当てる。
        /// </summary>
        private static void FillEmptyMotion(AnimatorState state, AnimationClip emptyClip)
        {
            if (state.motion != null) return;
            state.motion = emptyClip;
        }

        /// <summary>
        /// 制御 State 用に、何も書き込まない AnimationClip アセットを作成する。
        /// </summary>
        private static AnimationClip CreateEmptyControlClip(string clipOutputDir, string stateMachineName)
        {
            // VRChat の animator 初期化では空 Motion がある前提の方が TrackingControl の初期適用が安定する。
            string clipName = stateMachineName + "_Empty";
            string path = clipOutputDir.TrimEnd('/') + "/" + clipName + ".anim";
            return VrcftAssetUtility.CreateClip(path, clipName);
        }

        /// <summary>
        /// Any State から指定ステートへ即時遷移する条件付き遷移を追加する。
        /// </summary>
        private static void AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination, AnimatorConditionMode mode, string boolParam)
        {
            var transition = stateMachine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(mode, 0f, boolParam);
        }

        private static int FindLayerIndex(AnimatorController controller, string layerName)
        {
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName) return i;
            }
            return -1;
        }

        private static List<int> ToList(params int[] values)
        {
            return values.Where(v => v >= 0).ToList();
        }

        private static void AddLayerControl(AnimatorState state, int layerIndex, float goalWeight)
        {
            var lc = state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            lc.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
            lc.layer = layerIndex;
            lc.goalWeight = goalWeight;
            lc.blendDuration = 0.1f;
        }

        // ============================================================
        // ヘルパー
        // ============================================================
        internal static void AddSingleStateLayer(AnimatorController controller, string layerName, Motion motion, bool writeDefaults, float defaultWeight = 1f)
        {
            var layer = new AnimatorControllerLayer
            {
                name = layerName,
                defaultWeight = defaultWeight,
                stateMachine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy },
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
            controller.AddLayer(layer);

            var state = layer.stateMachine.AddState(layerName + "_State");
            state.motion = motion;
            state.writeDefaultValues = writeDefaults;
            layer.stateMachine.defaultState = state;
        }

        internal static string Path(VRCAvatarDescriptor avatar, SkinnedMeshRenderer smr)
        {
            return AnimationUtility.CalculateTransformPath(smr.transform, avatar.transform);
        }

        private static AnimationClip CreateAnimatorClip(string path, string name, string parameter, float value, VrcftGenerationResult result)
        {
            var clip = new AnimationClip { name = name };
            VrcftAssetUtility.AddAnimatorParameterConstant(clip, parameter, value);
            AssetDatabase.CreateAsset(clip, path);
            result.generatedClipPaths.Add(path);
            return clip;
        }

        // 駆動シェイプクリップ用 (作成→カーブ追加後 FinishClip で保存)
        private static AnimationClip NewClip(VrcftGenerationResult result, Target t, string suffix)
        {
            return new AnimationClip { name = t.Entry.FullName.Replace('/', '_') + suffix };
        }

        private static void FinishClip(AnimationClip clip, Target t, string suffix, VrcftGenerationResult result)
        {
            string path = result.outputDir + "/Animations/" + t.Entry.FullName.Replace('/', '_') + suffix + ".anim";
            AssetDatabase.CreateAsset(clip, path);
            result.generatedClipPaths.Add(path);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Avatar";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

    }
}
