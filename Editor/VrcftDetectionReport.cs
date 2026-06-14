using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// 1スロットに対するブレンドシェイプのマッチ結果。
    /// </summary>
    public sealed class SlotMatch
    {
        public ShapeSlot Slot;
        public SkinnedMeshRenderer Mesh;
        public string BlendShapeName;
        public int BlendShapeIndex;

        /// <summary>EyeLid専用スロット種別 ("blink"/"wide")。通常スロットでは null。</summary>
        public string SlotKind;

        /// <summary>手動指定によって選ばれたマッチか。</summary>
        public bool IsManual;
    }

    /// <summary>
    /// 1スロットの手動割り当て情報。
    /// </summary>
    public sealed class ManualSlotOverride
    {
        public ShapeSlot Slot;
        public string SlotKind;
        public string DisplayName;

        /// <summary>未入力なら自動検知結果を使う。入力時はこの名前に一致するシェイプを優先する。</summary>
        public string BlendShapeName;
    }

    /// <summary>
    /// 1つのFTパラメーター(カタログエントリ)に対するマッチ結果。
    /// </summary>
    public sealed class ParameterMatch
    {
        public VrcftCatalogEntry Entry;
        public List<SlotMatch> SlotMatches = new List<SlotMatch>();
        public List<ManualSlotOverride> ManualOverrides = new List<ManualSlotOverride>();

        /// <summary>UI操作用: 生成対象として有効か。</summary>
        public bool Enabled = true;

        /// <summary>UI操作用: 手動割り当て欄を表示するか。</summary>
        public bool ShowManualSettings;

        /// <summary>Binary時のビット数 (UIで上書き可能)。初期値はカタログのDefaultBinaryBits。</summary>
        public int Bits;

        /// <summary>1つ以上のシェイプがマッチしたか。</summary>
        public bool HasAnyMatch => SlotMatches.Count > 0;

        /// <summary>手動割り当てが1つ以上入力されているか。</summary>
        public bool HasManualOverride => ManualOverrides.Any(o => !string.IsNullOrWhiteSpace(o.BlendShapeName));

        /// <summary>マッチしたシェイプ名をカンマ区切りで返す (UI表示用)。</summary>
        public string MatchedShapesLabel
        {
            get
            {
                if (SlotMatches.Count == 0) return "(未検知)";
                return string.Join(", ", SlotMatches.Select(m => m.BlendShapeName));
            }
        }
    }

    /// <summary>
    /// EyeLook (視線) の検知情報。
    /// </summary>
    public sealed class EyeLookInfo
    {
        public bool EnableEyeLook;
        public Transform LeftEyeBone;
        public Transform RightEyeBone;

        // RotationStates (Descriptor の customEyeLookSettings 由来)
        public Quaternion LeftUp = Quaternion.identity;
        public Quaternion LeftDown = Quaternion.identity;
        public Quaternion LeftLeft = Quaternion.identity;
        public Quaternion LeftRight = Quaternion.identity;
        public Quaternion RightUp = Quaternion.identity;
        public Quaternion RightDown = Quaternion.identity;
        public Quaternion RightLeft = Quaternion.identity;
        public Quaternion RightRight = Quaternion.identity;

        /// <summary>まぶた制御方式 ("Bones"/"Blendshapes"/"None")。</summary>
        public string EyelidType = "None";
    }

    /// <summary>
    /// アバター1体の検知結果全体。
    /// </summary>
    public sealed class VrcftDetectionReport
    {
        /// <summary>検知対象アバター名。</summary>
        public string AvatarName;

        /// <summary>選択されたフェイスメッシュ (優先メッシュ)。</summary>
        public SkinnedMeshRenderer FaceMesh;

        /// <summary>走査した全SkinnedMeshRenderer。</summary>
        public List<SkinnedMeshRenderer> ScannedMeshes = new List<SkinnedMeshRenderer>();

        /// <summary>全カタログエントリのマッチ結果。</summary>
        public List<ParameterMatch> Matches = new List<ParameterMatch>();

        /// <summary>EyeLook情報。</summary>
        public EyeLookInfo EyeLook = new EyeLookInfo();

        /// <summary>指定プリセット内でマッチしたエントリ数。</summary>
        public int MatchedCount(VrcftAutoSetupSettings settings)
        {
            return Matches.Count(m => IsSelectable(m, settings) && m.HasAnyMatch);
        }

        /// <summary>指定プリセット内の全エントリ数。</summary>
        public int TotalCount(VrcftAutoSetupSettings settings)
        {
            return Matches.Count(m => IsSelectable(m, settings));
        }

        /// <summary>
        /// プリセットと通常/簡略プロファイルを加味して、このパラメーターを候補に含めるか判定する。
        /// </summary>
        public bool IsSelectable(ParameterMatch match, VrcftAutoSetupSettings settings)
        {
            if (match == null || settings == null) return false;
            if ((int)match.Entry.Preset > (int)settings.preset) return false;
            if (match.Entry.ConflictGroups.Count == 0) return true;

            bool hasDetailed = HasMatchedAlternative(match, VrcftParameterFamily.Detailed, settings);
            bool hasSimplified = HasMatchedAlternative(match, VrcftParameterFamily.Simplified, settings);
            bool hasCompleteDetailed = HasCompleteDetailedAlternative(match, settings);

            switch (settings.parameterProfile)
            {
                case VrcftParameterProfile.Simplified:
                    return match.Entry.Family == VrcftParameterFamily.Simplified || !hasSimplified;
                case VrcftParameterProfile.Detailed:
                    return match.Entry.Family == VrcftParameterFamily.Detailed || !hasDetailed;
                case VrcftParameterProfile.Hybrid:
                    if (match.Entry.Family == VrcftParameterFamily.Simplified)
                    {
                        return !hasCompleteDetailed;
                    }
                    return !hasSimplified || hasCompleteDetailed;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 同じ競合グループに、指定分類の検知済みパラメーターが存在するか調べる。
        /// </summary>
        private bool HasMatchedAlternative(ParameterMatch match, VrcftParameterFamily family, VrcftAutoSetupSettings settings)
        {
            return Matches.Any(other =>
                other != match &&
                other.Entry.Family == family &&
                other.HasAnyMatch &&
                (int)other.Entry.Preset <= (int)settings.preset &&
                other.Entry.ConflictGroups.Any(g => match.Entry.ConflictGroups.Contains(g)));
        }

        /// <summary>
        /// Hybrid用に、同じ競合グループの詳細系が簡略系の担当範囲をすべて満たすか判定する。
        /// </summary>
        private bool HasCompleteDetailedAlternative(ParameterMatch match, VrcftAutoSetupSettings settings)
        {
            var requiredKeys = RequiredConflictKeys(match, settings);
            if (requiredKeys.Count == 0)
            {
                return HasMatchedAlternative(match, VrcftParameterFamily.Detailed, settings);
            }

            var coveredKeys = Matches
                .Where(other =>
                    other.Entry.Family == VrcftParameterFamily.Detailed &&
                    other.HasAnyMatch &&
                    (int)other.Entry.Preset <= (int)settings.preset &&
                    other.Entry.ConflictGroups.Any(g => match.Entry.ConflictGroups.Contains(g)))
                .SelectMany(other => other.Entry.ConflictKeys)
                .ToHashSet();

            return requiredKeys.All(coveredKeys.Contains);
        }

        /// <summary>
        /// 簡略系エントリが要求する左右などの担当範囲を取得する。
        /// </summary>
        private HashSet<string> RequiredConflictKeys(ParameterMatch match, VrcftAutoSetupSettings settings)
        {
            var keys = new HashSet<string>();

            foreach (var simplified in Matches)
            {
                if (simplified.Entry.Family != VrcftParameterFamily.Simplified) continue;
                if ((int)simplified.Entry.Preset > (int)settings.preset) continue;
                if (!simplified.Entry.ConflictGroups.Any(g => match.Entry.ConflictGroups.Contains(g))) continue;

                foreach (var key in simplified.Entry.ConflictKeys)
                {
                    keys.Add(key);
                }
            }

            if (keys.Count == 0)
            {
                foreach (var key in match.Entry.ConflictKeys)
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        /// <summary>
        /// 検知結果の要約文字列を生成する (検知数 / 未検知一覧)。
        /// </summary>
        public string BuildSummary(VrcftAutoSetupSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"アバター: {AvatarName}");
            sb.AppendLine($"フェイスメッシュ: {(FaceMesh != null ? FaceMesh.name : "(なし)")}");
            sb.AppendLine($"走査メッシュ数: {ScannedMeshes.Count}");
            sb.AppendLine($"プリセット: {settings.preset}");
            sb.AppendLine($"パラメーターモード: {settings.parameterProfile}");

            var inPreset = Matches.Where(m => IsSelectable(m, settings)).ToList();
            int matched = inPreset.Count(m => m.HasAnyMatch);
            sb.AppendLine($"検知: {matched} / {inPreset.Count} パラメーター");

            var missing = inPreset.Where(m => !m.HasAnyMatch).Select(m => m.Entry.ParameterName).ToList();
            if (missing.Count > 0)
                sb.AppendLine($"未検知 ({missing.Count}): {string.Join(", ", missing)}");
            else
                sb.AppendLine("未検知: なし");

            sb.AppendLine($"EyeLook: {(EyeLook.EnableEyeLook ? "有効" : "無効")} " +
                          $"(L={(EyeLook.LeftEyeBone != null ? EyeLook.LeftEyeBone.name : "なし")}, " +
                          $"R={(EyeLook.RightEyeBone != null ? EyeLook.RightEyeBone.name : "なし")}, " +
                          $"eyelid={EyeLook.EyelidType})");

            return sb.ToString();
        }
    }
}
