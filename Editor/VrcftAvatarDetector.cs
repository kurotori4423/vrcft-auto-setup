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

        private static void AddSlotMatch(ParameterMatch pm, ShapeSlot slot, string kind, List<MeshLookup> meshLookups)
        {
            if (slot == null) return;
            foreach (var alias in slot.Aliases)
            {
                var key = VrcftShapeCatalog.Normalize(alias);
                foreach (var lookup in meshLookups)
                {
                    if (lookup.Map.TryGetValue(key, out var hit))
                    {
                        pm.SlotMatches.Add(new SlotMatch
                        {
                            Slot = slot,
                            Mesh = lookup.Mesh,
                            BlendShapeName = hit.Name,
                            BlendShapeIndex = hit.Index,
                            SlotKind = kind,
                        });
                        return; // このスロットは1つマッチすれば確定 (フェイスメッシュ優先)
                    }
                }
            }
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

            info.LeftUp = s.eyesLookingUp.left;
            info.RightUp = s.eyesLookingUp.right;
            info.LeftDown = s.eyesLookingDown.left;
            info.RightDown = s.eyesLookingDown.right;
            info.LeftLeft = s.eyesLookingLeft.left;
            info.RightLeft = s.eyesLookingLeft.right;
            info.LeftRight = s.eyesLookingRight.left;
            info.RightRight = s.eyesLookingRight.right;
        }

        /// <summary>
        /// 1メッシュの正規化シェイプ名ルックアップ。
        /// </summary>
        private sealed class MeshLookup
        {
            public readonly SkinnedMeshRenderer Mesh;
            public readonly Dictionary<string, (string Name, int Index)> Map =
                new Dictionary<string, (string, int)>();

            public MeshLookup(SkinnedMeshRenderer mesh)
            {
                Mesh = mesh;
                var sm = mesh.sharedMesh;
                for (int i = 0; i < sm.blendShapeCount; i++)
                {
                    var name = sm.GetBlendShapeName(i);
                    var key = VrcftShapeCatalog.Normalize(name);
                    if (!Map.ContainsKey(key)) Map[key] = (name, i);
                }
            }
        }
    }
}
