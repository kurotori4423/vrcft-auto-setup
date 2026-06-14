using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// VRCAvatarDescriptor を走査し、カタログ各エントリ・各スロットに対して
    /// ブレンドシェイプを正規化マッチで割り当てる。
    /// </summary>
    public static class VrcftAvatarDetector
    {
        /// <summary>
        /// アバターを検知し、レポートを返す。
        /// </summary>
        public static VrcftDetectionReport Detect(VRCAvatarDescriptor descriptor)
        {
            var report = new VrcftDetectionReport();
            if (descriptor == null)
            {
                report.AvatarName = "(null)";
                return report;
            }

            report.AvatarName = descriptor.gameObject.name;

            // 全SkinnedMeshRendererを収集 (ブレンドシェイプを持つもののみ)
            var allSmrs = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.sharedMesh != null && s.sharedMesh.blendShapeCount > 0)
                .ToList();
            report.ScannedMeshes = allSmrs;

            // フェイスメッシュ優先順を決定
            var faceMesh = PickFaceMesh(descriptor, allSmrs);
            report.FaceMesh = faceMesh;

            // 走査順: フェイスメッシュ優先、続いて他メッシュ
            var orderedMeshes = new List<SkinnedMeshRenderer>();
            if (faceMesh != null) orderedMeshes.Add(faceMesh);
            orderedMeshes.AddRange(allSmrs.Where(s => s != faceMesh));

            // 各メッシュの正規化シェイプ名 → (mesh, index, originalName) を事前構築
            var meshLookups = orderedMeshes
                .Select(m => new MeshLookup(m))
                .ToList();

            // カタログ各エントリをマッチング
            foreach (var entry in VrcftShapeCatalog.Entries)
            {
                var pm = new ParameterMatch
                {
                    Entry = entry,
                    Bits = entry.DefaultBinaryBits,
                };

                if (entry.IsEyeLid)
                {
                    AddSlotMatch(pm, entry.EyeLidBlink, "blink", meshLookups);
                    AddSlotMatch(pm, entry.EyeLidWide, "wide", meshLookups);
                }
                else
                {
                    foreach (var slot in entry.PositiveShapes)
                        AddSlotMatch(pm, slot, "positive", meshLookups);
                    foreach (var slot in entry.NegativeShapes)
                        AddSlotMatch(pm, slot, "negative", meshLookups);
                }

                pm.Enabled = pm.HasAnyMatch;
                report.Matches.Add(pm);
            }

            // EyeLook情報を収集
            CollectEyeLook(descriptor, report);

            return report;
        }

        /// <summary>
        /// 手動割り当てを検知結果へ反映する。
        /// </summary>
        public static void ApplyManualOverrides(VRCAvatarDescriptor descriptor, VrcftDetectionReport report)
        {
            if (descriptor == null || report == null) return;

            var allSmrs = descriptor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.sharedMesh != null && s.sharedMesh.blendShapeCount > 0)
                .ToList();
            var faceMesh = PickFaceMesh(descriptor, allSmrs);

            var orderedMeshes = new List<SkinnedMeshRenderer>();
            if (faceMesh != null) orderedMeshes.Add(faceMesh);
            orderedMeshes.AddRange(allSmrs.Where(s => s != faceMesh));

            var meshLookups = orderedMeshes.Select(m => new MeshLookup(m)).ToList();

            foreach (var match in report.Matches)
            {
                foreach (var manual in match.ManualOverrides)
                {
                    if (string.IsNullOrWhiteSpace(manual.BlendShapeName)) continue;

                    var slotMatch = FindManualMatch(manual, meshLookups);
                    if (slotMatch == null) continue;

                    match.SlotMatches.RemoveAll(x => x.Slot == manual.Slot && x.SlotKind == manual.SlotKind);
                    match.SlotMatches.Add(slotMatch);
                }
            }
        }

        private static void AddSlotMatch(ParameterMatch pm, ShapeSlot slot, string kind, List<MeshLookup> meshLookups)
        {
            if (slot == null) return;

            pm.ManualOverrides.Add(new ManualSlotOverride
            {
                Slot = slot,
                SlotKind = kind,
                DisplayName = BuildManualDisplayName(slot, kind),
            });

            var hit = FindBestAutoMatch(slot, kind, meshLookups);
            if (hit != null)
            {
                pm.SlotMatches.Add(hit);
            }
        }

        /// <summary>
        /// 規格優先度と一致品質で、スロットに最も適した自動検知候補を選ぶ。
        /// </summary>
        private static SlotMatch FindBestAutoMatch(ShapeSlot slot, string kind, List<MeshLookup> meshLookups)
        {
            var candidates = new List<MatchCandidate>();

            for (int aliasIndex = 0; aliasIndex < slot.Aliases.Length; aliasIndex++)
            {
                string alias = slot.Aliases[aliasIndex];
                string aliasKey = VrcftShapeCatalog.Normalize(alias);
                if (string.IsNullOrEmpty(aliasKey)) continue;

                int standardRank = ResolveStandardRank(alias);

                for (int meshIndex = 0; meshIndex < meshLookups.Count; meshIndex++)
                {
                    foreach (var shape in meshLookups[meshIndex].Shapes)
                    {
                        int quality = MatchQuality(shape.Key, aliasKey);
                        if (quality < 0) continue;

                        candidates.Add(new MatchCandidate
                        {
                            Slot = slot,
                            SlotKind = kind,
                            Mesh = meshLookups[meshIndex].Mesh,
                            BlendShapeName = shape.Name,
                            BlendShapeIndex = shape.Index,
                            StandardRank = standardRank,
                            MatchQuality = quality,
                            MeshIndex = meshIndex,
                            AliasIndex = aliasIndex,
                        });
                    }
                }
            }

            return candidates
                .OrderBy(c => c.StandardRank)
                .ThenBy(c => c.MatchQuality)
                .ThenBy(c => c.MeshIndex)
                .ThenBy(c => c.AliasIndex)
                .Select(c => c.ToSlotMatch(isManual: false))
                .FirstOrDefault();
        }

        /// <summary>
        /// 手動入力名に一致するシェイプを探す。入力ミスに強くするため、自動検知と同じ正規化を使う。
        /// </summary>
        private static SlotMatch FindManualMatch(ManualSlotOverride manual, List<MeshLookup> meshLookups)
        {
            string key = VrcftShapeCatalog.Normalize(manual.BlendShapeName);
            if (string.IsNullOrEmpty(key)) return null;

            for (int meshIndex = 0; meshIndex < meshLookups.Count; meshIndex++)
            {
                foreach (var shape in meshLookups[meshIndex].Shapes)
                {
                    if (MatchQuality(shape.Key, key) < 0) continue;
                    return new SlotMatch
                    {
                        Slot = manual.Slot,
                        SlotKind = manual.SlotKind,
                        Mesh = meshLookups[meshIndex].Mesh,
                        BlendShapeName = shape.Name,
                        BlendShapeIndex = shape.Index,
                        IsManual = true,
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// 正規化済みシェイプ名とエイリアスの一致品質を返す。0=完全一致、1=接頭辞/接尾辞込み一致。
        /// </summary>
        private static int MatchQuality(string shapeKey, string aliasKey)
        {
            if (shapeKey == aliasKey) return 0;
            return shapeKey.EndsWith(aliasKey) ? 1 : -1;
        }

        /// <summary>
        /// Unified > ARKit > SRanipal > Meta Movement の順で小さい順位を返す。
        /// </summary>
        private static int ResolveStandardRank(string alias)
        {
            if (string.IsNullOrEmpty(alias)) return 99;

            // Meta Movement は大文字スネークケース、SRanipal は PascalCase と '_' の組み合わせ、ARKit は camelCase が多い。
            if (alias.Any(char.IsLower) && char.IsLower(alias[0])) return 1;
            if (alias.Contains("_"))
            {
                return alias.ToUpperInvariant() == alias ? 3 : 2;
            }
            return 0;
        }

        private static string BuildManualDisplayName(ShapeSlot slot, string kind)
        {
            string alias = slot.Aliases.FirstOrDefault() ?? "(slot)";
            return string.IsNullOrEmpty(kind) ? alias : $"{kind}: {alias}";
        }

        /// <summary>
        /// フェイスメッシュを優先順位で選ぶ:
        /// 1. descriptor.VisemeSkinnedMesh
        /// 2. 名前 "Body" / "Face"
        /// 3. ブレンドシェイプ数最大
        /// </summary>
        private static SkinnedMeshRenderer PickFaceMesh(VRCAvatarDescriptor descriptor, List<SkinnedMeshRenderer> smrs)
        {
            if (smrs.Count == 0) return null;

            if (descriptor.VisemeSkinnedMesh != null &&
                descriptor.VisemeSkinnedMesh.sharedMesh != null &&
                descriptor.VisemeSkinnedMesh.sharedMesh.blendShapeCount > 0)
            {
                return descriptor.VisemeSkinnedMesh;
            }

            var byName = smrs.FirstOrDefault(s =>
                s.name.Equals("Body", System.StringComparison.OrdinalIgnoreCase) ||
                s.name.Equals("Face", System.StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName;

            return smrs.OrderByDescending(s => s.sharedMesh.blendShapeCount).First();
        }

        private static void CollectEyeLook(VRCAvatarDescriptor descriptor, VrcftDetectionReport report)
        {
            var info = report.EyeLook;
            info.EnableEyeLook = descriptor.enableEyeLook;

            var s = descriptor.customEyeLookSettings;
            info.LeftEyeBone = s.leftEye;
            info.RightEyeBone = s.rightEye;
            info.EyelidType = s.eyelidType.ToString();

            // Descriptorをコード生成した直後はRotationStateがnullのことがあるため、未設定時はidentityのまま扱う。
            if (s.eyesLookingUp != null)
            {
                info.LeftUp = s.eyesLookingUp.left;
                info.RightUp = s.eyesLookingUp.right;
            }
            if (s.eyesLookingDown != null)
            {
                info.LeftDown = s.eyesLookingDown.left;
                info.RightDown = s.eyesLookingDown.right;
            }
            if (s.eyesLookingLeft != null)
            {
                info.LeftLeft = s.eyesLookingLeft.left;
                info.RightLeft = s.eyesLookingLeft.right;
            }
            if (s.eyesLookingRight != null)
            {
                info.LeftRight = s.eyesLookingRight.left;
                info.RightRight = s.eyesLookingRight.right;
            }
        }

        /// <summary>
        /// 1メッシュの正規化シェイプ名ルックアップ。
        /// </summary>
        private sealed class MeshLookup
        {
            public readonly SkinnedMeshRenderer Mesh;
            public readonly Dictionary<string, (string Name, int Index)> Map =
                new Dictionary<string, (string, int)>();
            public readonly List<(string Key, string Name, int Index)> Shapes =
                new List<(string, string, int)>();

            public MeshLookup(SkinnedMeshRenderer mesh)
            {
                Mesh = mesh;
                var sm = mesh.sharedMesh;
                for (int i = 0; i < sm.blendShapeCount; i++)
                {
                    var name = sm.GetBlendShapeName(i);
                    var key = VrcftShapeCatalog.Normalize(name);
                    Shapes.Add((key, name, i));
                    if (!Map.ContainsKey(key)) Map[key] = (name, i);
                }
            }
        }

        /// <summary>
        /// 自動検知候補の順位付けに必要な情報。
        /// </summary>
        private sealed class MatchCandidate
        {
            public ShapeSlot Slot;
            public string SlotKind;
            public SkinnedMeshRenderer Mesh;
            public string BlendShapeName;
            public int BlendShapeIndex;
            public int StandardRank;
            public int MatchQuality;
            public int MeshIndex;
            public int AliasIndex;

            public SlotMatch ToSlotMatch(bool isManual)
            {
                return new SlotMatch
                {
                    Slot = Slot,
                    SlotKind = SlotKind,
                    Mesh = Mesh,
                    BlendShapeName = BlendShapeName,
                    BlendShapeIndex = BlendShapeIndex,
                    IsManual = isManual,
                };
            }
        }
    }
}
