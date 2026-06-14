using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// VRCFT Auto Setup のメインウィンドウ。
    /// アバター指定 → 検知 → レポート表示 → 設定 → (Phase Bで生成)。
    /// </summary>
    public sealed class VrcftAutoSetupWindow : EditorWindow
    {
        private VRCAvatarDescriptor _avatar;
        private VrcftDetectionReport _report;
        private readonly VrcftAutoSetupSettings _settings = new VrcftAutoSetupSettings();

        private Vector2 _scroll;
        private bool _showSettings = true;

        // Phase B 生成結果 (Phase C で使用)
        private VrcftGenerationResult _generationResult;
        private string _generateLog;
        private GameObject _installedObject;

        [MenuItem("Tools/Kurotori/VRCFT Auto Setup")]
        public static void Open()
        {
            var win = GetWindow<VrcftAutoSetupWindow>("VRCFT Auto Setup");
            win.minSize = new Vector2(420, 480);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            DrawLanguageSelector();
            EditorGUILayout.LabelField(VrcftLocalization.T("title"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(VrcftLocalization.T("description"), MessageType.Info);

            DrawAvatarField();
            EditorGUILayout.Space();
            DrawPresetAndSettings();
            EditorGUILayout.Space();

            if (_report != null)
            {
                DrawSummary();
                DrawReportList();
            }

            EditorGUILayout.Space();
            DrawGenerateButton();
        }

        private void DrawAvatarField()
        {
            EditorGUI.BeginChangeCheck();
            _avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
                VrcftLocalization.Content("avatar", "tooltip.avatar"), _avatar, typeof(VRCAvatarDescriptor), true);
            if (EditorGUI.EndChangeCheck())
            {
                _report = null; // アバター変更で検知結果をクリア
            }

            using (new EditorGUI.DisabledScope(_avatar == null))
            {
                if (GUILayout.Button(VrcftLocalization.T("detect"), GUILayout.Height(28)))
                {
                    _report = VrcftAvatarDetector.Detect(_avatar);
                }
            }
        }

        /// <summary>
        /// UI言語をEditorPrefsに保存し、プロジェクトをまたいでも同じ表示言語で開けるようにする。
        /// </summary>
        private void DrawLanguageSelector()
        {
            var next = DrawLanguagePopup(VrcftLocalization.Content("language", "tooltip.language"), VrcftLocalization.EditorLanguage);
            if (next != VrcftLocalization.EditorLanguage)
            {
                VrcftLocalization.EditorLanguage = next;
            }
        }

        private void DrawPresetAndSettings()
        {
            EditorGUI.BeginChangeCheck();
            _settings.preset = (VrcftPreset)EditorGUILayout.EnumPopup(VrcftLocalization.Content("preset", "tooltip.preset"), _settings.preset);
            _settings.parameterProfile = DrawEnumPopup(VrcftLocalization.Content("parameterProfile", "tooltip.parameterProfile"), _settings.parameterProfile);
            if (EditorGUI.EndChangeCheck())
            {
                // プリセット/モード変更時、競合する通常系・簡略系が同時駆動しないよう有効化フラグを調整。
                if (_report != null) ApplyPresetFilter();
            }

            _showSettings = EditorGUILayout.Foldout(_showSettings, VrcftLocalization.T("settings"), true);
            if (!_showSettings) return;

            using (new EditorGUI.IndentLevelScope())
            {
                _settings.useBinary = EditorGUILayout.Toggle(VrcftLocalization.Content("useBinary", "tooltip.useBinary"), _settings.useBinary);
                if (_settings.useBinary)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.defaultBinaryBitsOverride = EditorGUILayout.IntField(
                            VrcftLocalization.Content("bitsOverride", "tooltip.bitsOverride"), _settings.defaultBinaryBitsOverride);
                    }
                }

                _settings.writeDefaultsMode = (VrcftWriteDefaultsMode)EditorGUILayout.EnumPopup(VrcftLocalization.Content("writeDefaults", "tooltip.writeDefaults"), _settings.writeDefaultsMode);

                bool smoothingAllowed = _settings.writeDefaultsMode != VrcftWriteDefaultsMode.Off;
                if (!smoothingAllowed)
                {
                    _settings.enableSmoothing = false;
                }

                using (new EditorGUI.DisabledScope(!smoothingAllowed))
                {
                    _settings.enableSmoothing = EditorGUILayout.Toggle(VrcftLocalization.Content("smoothing", "tooltip.smoothing"), _settings.enableSmoothing);
                }
                if (!smoothingAllowed)
                {
                    EditorGUILayout.HelpBox(VrcftLocalization.T("smoothingDisabled"), MessageType.Info);
                }
                if (_settings.enableSmoothing)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.localSmoothness = EditorGUILayout.Slider(VrcftLocalization.Content("local", "tooltip.local"), _settings.localSmoothness, 0f, 1f);
                        _settings.remoteSmoothness = EditorGUILayout.Slider(VrcftLocalization.Content("remote", "tooltip.remote"), _settings.remoteSmoothness, 0f, 1f);
                    }
                }

                _settings.enableEyeLook = EditorGUILayout.Toggle(VrcftLocalization.Content("eyeLook", "tooltip.eyeLook"), _settings.enableEyeLook);
                if (_settings.enableEyeLook)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.eyeLookMode = DrawEnumPopup(VrcftLocalization.Content("driveMode", "tooltip.driveMode"), _settings.eyeLookMode);
                    }
                }

                _settings.enableVoiceLipSyncBlend = EditorGUILayout.Toggle(VrcftLocalization.Content("voiceLipSyncBlend", "tooltip.voiceLipSyncBlend"), _settings.enableVoiceLipSyncBlend);
                if (_settings.enableVoiceLipSyncBlend)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.voiceLipSyncThreshold = EditorGUILayout.Slider(
                            VrcftLocalization.Content("voiceThreshold", "tooltip.voiceThreshold"), Mathf.Clamp01(_settings.voiceLipSyncThreshold), 0f, 1f);
                    }
                }

                _settings.addMenu = EditorGUILayout.Toggle(VrcftLocalization.Content("addMenu", "tooltip.addMenu"), _settings.addMenu);
                if (_settings.addMenu)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.generatedMenuLanguage = DrawLanguagePopup(VrcftLocalization.Content("menuLanguage", "tooltip.menuLanguage"), _settings.generatedMenuLanguage);
                    }
                }
                _settings.outputFolder = EditorGUILayout.TextField(VrcftLocalization.Content("outputFolder", "tooltip.outputFolder"), _settings.outputFolder);
            }
        }

        private void ApplyPresetFilter()
        {
            foreach (var m in _report.Matches)
            {
                bool selectable = _report.IsSelectable(m, _settings);
                if (!selectable) m.Enabled = false;
                else if (m.HasAnyMatch) m.Enabled = true;
            }
        }

        private void DrawSummary()
        {
            int matched = _report.MatchedCount(_settings);
            int total = _report.TotalCount(_settings);
            EditorGUILayout.LabelField(VrcftLocalization.Format("detectionResult", matched, total), EditorStyles.boldLabel);

            EditorGUILayout.LabelField(VrcftLocalization.Format("syncBits", ComputeSyncBits()));
        }

        /// <summary>
        /// 合計同期ビット数を計算。
        /// Binary時: Σ(bits + twoSidedなら+1) + 制御パラメーター(EyeTrackingActive=1, LipTrackingActive=1, 任意VoiceLipSyncBlend=1)。
        /// Float時: 有効パラメーター数 × 8。
        /// </summary>
        private int ComputeSyncBits()
        {
            var active = _report.Matches
                .Where(m => _report.IsSelectable(m, _settings) && m.Enabled && m.HasAnyMatch)
                .ToList();

            if (!_settings.useBinary)
            {
                return active.Count * 8;
            }

            int total = 0;
            foreach (var m in active)
            {
                int bits = _settings.ResolveBits(m.Entry, m.Bits);
                total += bits;
                if (m.Entry.TwoSided) total += 1; // Negativeビット
            }
            total += 2; // EyeTrackingActive + LipTrackingActive
            if (_settings.enableVoiceLipSyncBlend) total += 1; // VoiceLipSyncBlend
            return total;
        }

        private void DrawReportList()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(VrcftLocalization.T("parameterList"), EditorStyles.boldLabel);

            // ヘッダー
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(VrcftLocalization.Content("manual", "tooltip.manual"), GUILayout.Width(48));
                EditorGUILayout.LabelField(VrcftLocalization.Content("enabled", "tooltip.enabled"), GUILayout.Width(56));
                EditorGUILayout.LabelField(VrcftLocalization.Content("parameter", "tooltip.parameter"), GUILayout.Width(140));
                EditorGUILayout.LabelField(VrcftLocalization.Content("bits", "tooltip.bits"), GUILayout.Width(50));
                EditorGUILayout.LabelField(VrcftLocalization.Content("detectedShape", "tooltip.detectedShape"));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180));

            var grayStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } };

            foreach (var m in _report.Matches)
            {
                if (!_report.IsSelectable(m, _settings)) continue;

                bool matched = m.HasAnyMatch;
                bool canEnable = matched || m.HasManualOverride;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var foldoutRect = GUILayoutUtility.GetRect(48f, EditorGUIUtility.singleLineHeight, GUILayout.Width(48));
                    m.ShowManualSettings = EditorGUI.Foldout(foldoutRect, m.ShowManualSettings, string.Empty, true);

                    using (new EditorGUI.DisabledScope(!canEnable))
                    {
                        m.Enabled = EditorGUILayout.Toggle(m.Enabled && canEnable, GUILayout.Width(56));
                    }

                    var labelStyle = matched ? EditorStyles.label : grayStyle;
                    EditorGUILayout.LabelField(m.Entry.ParameterName, labelStyle, GUILayout.Width(140));

                    using (new EditorGUI.DisabledScope(!matched || !_settings.useBinary))
                    {
                        m.Bits = EditorGUILayout.IntField(m.Bits, GUILayout.Width(50));
                    }

                    EditorGUILayout.LabelField(m.MatchedShapesLabel, matched ? EditorStyles.label : grayStyle);
                }

                if (m.ShowManualSettings)
                {
                    DrawManualOverrides(m);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 自動検知で拾えない接頭辞・接尾辞付きシェイプや誤検知を、スロット単位で手動上書きする欄を描画する。
        /// </summary>
        private void DrawManualOverrides(ParameterMatch match)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var manual in match.ManualOverrides)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(36);
                        EditorGUILayout.LabelField(manual.DisplayName, GUILayout.Width(180));
                        manual.BlendShapeName = EditorGUILayout.TextField(manual.BlendShapeName ?? string.Empty);
                    }
                }
            }
        }

        private void DrawGenerateButton()
        {
            using (new EditorGUI.DisabledScope(_avatar == null || _report == null))
            {
                if (GUILayout.Button(VrcftLocalization.T("generate"), GUILayout.Height(32)))
                {
                    RunGenerate();
                }
            }

            if (!string.IsNullOrEmpty(_generateLog))
            {
                EditorGUILayout.HelpBox(_generateLog, MessageType.Info);
            }

            if (_installedObject != null)
            {
                if (GUILayout.Button(VrcftLocalization.T("selectInstalled")))
                {
                    Selection.activeGameObject = _installedObject;
                    EditorGUIUtility.PingObject(_installedObject);
                }
            }
        }

        /// <summary>
        /// 同期ビット合計 (Bool=1, Float=8, localOnly除く) を syncedParameters から計算。
        /// </summary>
        private static int ComputeSyncedBitsFromResult(VrcftGenerationResult result)
        {
            int bits = 0;
            foreach (var p in result.syncedParameters)
            {
                if (p.localOnly) continue;
                bits += p.kind == VrcftParameterKind.Bool ? 1 : 8;
            }
            return bits;
        }

        private void RunGenerate()
        {
            try
            {
                _installedObject = null;

                // 1. FX 生成
                _generationResult = VrcftAnimatorGenerator.Generate(_avatar, _report, _settings);

                // 2. MA インストール
                _installedObject = VrcftModularAvatarInstaller.Install(_avatar, _generationResult, _settings);

                int totalBits = ComputeSyncedBitsFromResult(_generationResult);
                string prefabPath = _generationResult.outputDir.TrimEnd('/') + "/" + _avatar.name + "_VRCFT.prefab";

                var sb = new System.Text.StringBuilder();
                if (_installedObject != null)
                {
                    sb.AppendLine(VrcftLocalization.T("installComplete"));
                    sb.AppendLine(VrcftLocalization.Format("installedObject", _installedObject.name));
                    sb.AppendLine(VrcftLocalization.Format("prefab", prefabPath));
                }
                else
                {
                    sb.AppendLine(VrcftLocalization.T("installFailed"));
                }
                sb.AppendLine(VrcftLocalization.Format("output", _generationResult.outputDir));
                sb.AppendLine(VrcftLocalization.Format("layerCount", _generationResult.fxController.layers.Length));
                sb.AppendLine(VrcftLocalization.Format("syncedParameterCount", _generationResult.syncedParameters.Count));
                sb.AppendLine(VrcftLocalization.Format("clipCount", _generationResult.generatedClipPaths.Count));
                sb.AppendLine(VrcftLocalization.Format("syncedBitsTotal", totalBits));
                _generateLog = sb.ToString();

                Debug.Log("[VRCFT Auto Setup] " + _generateLog);

                if (_installedObject != null)
                {
                    Selection.activeGameObject = _installedObject;
                    EditorGUIUtility.PingObject(_installedObject);
                }
            }
            catch (System.Exception e)
            {
                _generateLog = VrcftLocalization.Format("generateFailed", e.Message);
                Debug.LogException(e);
            }
        }

        private static VrcftLanguage DrawLanguagePopup(GUIContent label, VrcftLanguage value)
        {
            var values = (VrcftLanguage[])Enum.GetValues(typeof(VrcftLanguage));
            var labels = values.Select(VrcftLocalization.LanguageLabel).ToArray();
            int index = Math.Max(0, Array.IndexOf(values, value));
            int nextIndex = EditorGUILayout.Popup(label, index, labels);
            return values[nextIndex];
        }

        private static T DrawEnumPopup<T>(GUIContent label, T value) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var labels = values.Select(v => VrcftLocalization.EnumLabel(v)).ToArray();
            int index = Math.Max(0, Array.IndexOf(values, value));
            int nextIndex = EditorGUILayout.Popup(label, index, labels);
            return values[nextIndex];
        }
    }
}
