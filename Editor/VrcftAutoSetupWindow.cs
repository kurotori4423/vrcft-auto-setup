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
            EditorGUILayout.LabelField("VRCFT 自動セットアップ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "対象アバターのブレンドシェイプを検知し、VRCFT用のアニメーションを生成して" +
                "Modular Avatarコンポーネント付きでアバターに配置します。", MessageType.Info);

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
                "対象アバター", _avatar, typeof(VRCAvatarDescriptor), true);
            if (EditorGUI.EndChangeCheck())
            {
                _report = null; // アバター変更で検知結果をクリア
            }

            using (new EditorGUI.DisabledScope(_avatar == null))
            {
                if (GUILayout.Button("検知", GUILayout.Height(28)))
                {
                    _report = VrcftAvatarDetector.Detect(_avatar);
                }
            }
        }

        private void DrawPresetAndSettings()
        {
            EditorGUI.BeginChangeCheck();
            _settings.preset = (VrcftPreset)EditorGUILayout.EnumPopup("プリセット", _settings.preset);
            if (EditorGUI.EndChangeCheck())
            {
                // プリセット変更時、対象外エントリの有効化フラグを調整
                if (_report != null) ApplyPresetFilter();
            }

            _showSettings = EditorGUILayout.Foldout(_showSettings, "生成設定", true);
            if (!_showSettings) return;

            using (new EditorGUI.IndentLevelScope())
            {
                _settings.useBinary = EditorGUILayout.Toggle("Binary同期 (ビット削減)", _settings.useBinary);
                if (_settings.useBinary)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.defaultBinaryBitsOverride = EditorGUILayout.IntField(
                            "ビット数一律上書き (0=個別)", _settings.defaultBinaryBitsOverride);
                    }
                }

                _settings.writeDefaultsMode = (VrcftWriteDefaultsMode)EditorGUILayout.EnumPopup("Write Defaults", _settings.writeDefaultsMode);

                bool smoothingAllowed = _settings.writeDefaultsMode != VrcftWriteDefaultsMode.Off;
                if (!smoothingAllowed)
                {
                    _settings.enableSmoothing = false;
                }

                using (new EditorGUI.DisabledScope(!smoothingAllowed))
                {
                    _settings.enableSmoothing = EditorGUILayout.Toggle("スムージング", _settings.enableSmoothing);
                }
                if (!smoothingAllowed)
                {
                    EditorGUILayout.HelpBox("Write Defaults Off では AAP を使うスムージングが不安定なため無効化されます。", MessageType.Info);
                }
                if (_settings.enableSmoothing)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.localSmoothness = EditorGUILayout.Slider("ローカル", _settings.localSmoothness, 0f, 1f);
                        _settings.remoteSmoothness = EditorGUILayout.Slider("リモート", _settings.remoteSmoothness, 0f, 1f);
                    }
                }

                _settings.enableEyeLook = EditorGUILayout.Toggle("EyeLook (視線)", _settings.enableEyeLook);
                if (_settings.enableEyeLook)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _settings.eyeLookMode = (EyeLookMode)EditorGUILayout.EnumPopup("駆動方式", _settings.eyeLookMode);
                    }
                }

                _settings.addMenu = EditorGUILayout.Toggle("メニュー生成", _settings.addMenu);
                _settings.outputFolder = EditorGUILayout.TextField("出力フォルダ", _settings.outputFolder);
            }
        }

        private void ApplyPresetFilter()
        {
            foreach (var m in _report.Matches)
            {
                bool inPreset = (int)m.Entry.Preset <= (int)_settings.preset;
                if (!inPreset) m.Enabled = false;
                else if (m.HasAnyMatch) m.Enabled = true;
            }
        }

        private void DrawSummary()
        {
            int matched = _report.MatchedCount(_settings.preset);
            int total = _report.TotalCount(_settings.preset);
            EditorGUILayout.LabelField($"検知結果: {matched} / {total} パラメーター", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"同期ビット数 (推定): {ComputeSyncBits()} bits");
        }

        /// <summary>
        /// 合計同期ビット数を計算。
        /// Binary時: Σ(bits + twoSidedなら+1) + 制御パラメーター(EyeTrackingActive=1, LipTrackingActive=1)。
        /// Float時: 有効パラメーター数 × 8。
        /// </summary>
        private int ComputeSyncBits()
        {
            var active = _report.Matches
                .Where(m => (int)m.Entry.Preset <= (int)_settings.preset && m.Enabled && m.HasAnyMatch)
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
            return total;
        }

        private void DrawReportList()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("パラメーター一覧", EditorStyles.boldLabel);

            // ヘッダー
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("有効", GUILayout.Width(36));
                EditorGUILayout.LabelField("パラメーター", GUILayout.Width(140));
                EditorGUILayout.LabelField("ビット", GUILayout.Width(50));
                EditorGUILayout.LabelField("検知シェイプ");
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180));

            var grayStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } };

            foreach (var m in _report.Matches)
            {
                if ((int)m.Entry.Preset > (int)_settings.preset) continue;

                bool matched = m.HasAnyMatch;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!matched))
                    {
                        m.Enabled = EditorGUILayout.Toggle(m.Enabled && matched, GUILayout.Width(36));
                    }

                    var labelStyle = matched ? EditorStyles.label : grayStyle;
                    EditorGUILayout.LabelField(m.Entry.ParameterName, labelStyle, GUILayout.Width(140));

                    using (new EditorGUI.DisabledScope(!matched || !_settings.useBinary))
                    {
                        m.Bits = EditorGUILayout.IntField(m.Bits, GUILayout.Width(50));
                    }

                    EditorGUILayout.LabelField(m.MatchedShapesLabel, matched ? EditorStyles.label : grayStyle);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGenerateButton()
        {
            using (new EditorGUI.DisabledScope(_avatar == null || _report == null))
            {
                if (GUILayout.Button("生成してアバターに配置", GUILayout.Height(32)))
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
                if (GUILayout.Button("配置オブジェクトを選択"))
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
                    sb.AppendLine("生成・配置完了 (Phase C)");
                    sb.AppendLine($"配置オブジェクト: {_installedObject.name}");
                    sb.AppendLine($"プレハブ: {prefabPath}");
                }
                else
                {
                    sb.AppendLine("FX生成は完了しましたが、MA配置に失敗しました (Modular Avatar が必要です)");
                }
                sb.AppendLine($"出力先: {_generationResult.outputDir}");
                sb.AppendLine($"レイヤー数: {_generationResult.fxController.layers.Length}");
                sb.AppendLine($"同期パラメーター数: {_generationResult.syncedParameters.Count}");
                sb.AppendLine($"クリップ数: {_generationResult.generatedClipPaths.Count}");
                sb.AppendLine($"同期ビット合計 (localOnly除く): {totalBits} / 256 bits");
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
                _generateLog = "生成失敗: " + e.Message;
                Debug.LogException(e);
            }
        }
    }
}
