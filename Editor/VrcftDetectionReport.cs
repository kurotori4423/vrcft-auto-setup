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
    }

    /// <summary>
    /// 1つのFTパラメーター(カタログエントリ)に対するマッチ結果。
    /// </summary>
    public sealed class ParameterMatch
    {
        public VrcftCatalogEntry Entry;
        public List<SlotMatch> SlotMatches = new List<SlotMatch>();

        /// <summary>UI操作用: 生成対象として有効か。</summary>
        public bool Enabled = true;

        /// <summary>Binary時のビット数 (UIで上書き可能)。初期値はカタログのDefaultBinaryBits。</summary>
        public int Bits;

        /// <summary>1つ以上のシェイプがマッチしたか。</summary>
        public bool HasAnyMatch => SlotMatches.Count > 0;

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
        public int MatchedCount(VrcftPreset preset)
        {
            return Matches.Count(m => (int)m.Entry.Preset <= (int)preset && m.HasAnyMatch);
        }

        /// <summary>指定プリセット内の全エントリ数。</summary>
        public int TotalCount(VrcftPreset preset)
        {
            return Matches.Count(m => (int)m.Entry.Preset <= (int)preset);
        }

        /// <summary>
        /// 検知結果の要約文字列を生成する (検知数 / 未検知一覧)。
        /// </summary>
        public string BuildSummary(VrcftPreset preset)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"アバター: {AvatarName}");
            sb.AppendLine($"フェイスメッシュ: {(FaceMesh != null ? FaceMesh.name : "(なし)")}");
            sb.AppendLine($"走査メッシュ数: {ScannedMeshes.Count}");
            sb.AppendLine($"プリセット: {preset}");

            var inPreset = Matches.Where(m => (int)m.Entry.Preset <= (int)preset).ToList();
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
