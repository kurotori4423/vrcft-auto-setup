using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Kurotori.VrcftAutoSetup.Editor.Tests
{
    /// <summary>
    /// VRCFT公式docsの通常/簡略パラメーター構造と互換シェイプ名に基づき、
    /// Hybrid/Simplifiedモードの競合解決が意図どおり動くことを検証する。
    /// </summary>
    public sealed class VrcftParameterProfileTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void Catalog_ClassifiesDocsSimplifiedGroupsAndDetailedAlternatives()
        {
            AssertSimplified("CheekPuffSuck", "CheekPuffSuck", "Left", "Right");
            AssertDetailed("CheekPuffSuckLeft", "CheekPuffSuck", "Left");
            AssertDetailed("CheekPuffSuckRight", "CheekPuffSuck", "Right");

            AssertSimplified("MouthUpperUp", "MouthUpperUp", "Left", "Right");
            AssertDetailed("MouthUpperUpLeft", "MouthUpperUp", "Left");
            AssertDetailed("MouthUpperUpRight", "MouthUpperUp", "Right");

            AssertSimplified("SmileFrownLeft", "MouthCornerLeft", "Smile", "Frown");
            AssertDetailed("MouthCornerPullLeft", "MouthCornerLeft", "Smile");
            AssertDetailed("MouthFrownLeft", "MouthCornerLeft", "Frown");

            AssertSimplified("LipPucker", "LipPucker", "Upper", "Lower");
            AssertDetailed("LipPuckerUpper", "LipPucker", "Upper");
            AssertDetailed("LipPuckerLower", "LipPucker", "Lower");

            AssertSimplified("NoseSneer", "NoseSneer", "Left", "Right");
            AssertDetailed("NoseSneerLeft", "NoseSneer", "Left");
            AssertDetailed("NoseSneerRight", "NoseSneer", "Right");
        }

        [Test]
        public void Detector_MatchesCompatibilityShapeNamesAcrossStandards()
        {
            var report = Detect(
                "Cheek_Puff_Left", "CHEEK_PUFF_R", "Cheek_Suck", "CHEEK_SUCK_R",
                "Mouth_Upper_Up_Left", "UPPER_LIP_RAISER_R",
                "Mouth_Smile_Left", "LIP_CORNER_DEPRESSOR_L",
                "Mouth_Pout", "LipPuckerLower",
                "NOSE_WRINKLER_L", "noseSneerRight");

            AssertMatch(report, "CheekPuffSuck", "Cheek_Puff_Left", "CHEEK_PUFF_R", "Cheek_Suck", "CHEEK_SUCK_R");
            AssertMatch(report, "MouthUpperUp", "Mouth_Upper_Up_Left", "UPPER_LIP_RAISER_R");
            AssertMatch(report, "SmileFrownLeft", "Mouth_Smile_Left", "LIP_CORNER_DEPRESSOR_L");
            AssertMatch(report, "LipPucker", "Mouth_Pout", "LipPuckerLower");
            AssertMatch(report, "NoseSneer", "NOSE_WRINKLER_L", "noseSneerRight");
        }

        [Test]
        public void Hybrid_UsesDetailedOnlyWhenRequiredSidesAreComplete()
        {
            var complete = Detect("CheekPuffLeft", "CheekPuffRight");
            AssertSelectable(complete, VrcftParameterProfile.Hybrid, "CheekPuffSuck", false);
            AssertSelectable(complete, VrcftParameterProfile.Hybrid, "CheekPuffSuckLeft", true);
            AssertSelectable(complete, VrcftParameterProfile.Hybrid, "CheekPuffSuckRight", true);

            var partial = Detect("CheekPuffLeft");
            AssertSelectable(partial, VrcftParameterProfile.Hybrid, "CheekPuffSuck", true);
            AssertSelectable(partial, VrcftParameterProfile.Hybrid, "CheekPuffSuckLeft", false);
            AssertSelectable(partial, VrcftParameterProfile.Hybrid, "CheekPuffSuckRight", false);
        }

        [Test]
        public void Simplified_UsesCombinedParameterWithDetailedOnlyShapeKeys()
        {
            var report = Detect("CheekPuffLeft", "CheekPuffRight");

            AssertSelectable(report, VrcftParameterProfile.Simplified, "CheekPuffSuck", true);
            AssertSelectable(report, VrcftParameterProfile.Simplified, "CheekPuffSuckLeft", false);
            AssertSelectable(report, VrcftParameterProfile.Simplified, "CheekPuffSuckRight", false);
            AssertMatch(report, "CheekPuffSuck", "CheekPuffLeft", "CheekPuffRight");
        }

        [Test]
        public void Hybrid_AppliesCompleteKeyRuleToOtherCombinedGroups()
        {
            var mouth = Detect("MouthUpperUpLeft", "MouthUpperUpRight", "MouthFrownLeft");
            AssertSelectable(mouth, VrcftParameterProfile.Hybrid, "MouthUpperUp", false);
            AssertSelectable(mouth, VrcftParameterProfile.Hybrid, "MouthUpperUpLeft", true);
            AssertSelectable(mouth, VrcftParameterProfile.Hybrid, "MouthUpperUpRight", true);
            AssertSelectable(mouth, VrcftParameterProfile.Hybrid, "SmileFrownLeft", true);
            AssertSelectable(mouth, VrcftParameterProfile.Hybrid, "MouthFrownLeft", false);

            var lip = Detect("LipPuckerUpper", "LipPuckerLower");
            AssertSelectable(lip, VrcftParameterProfile.Hybrid, "LipPucker", false);
            AssertSelectable(lip, VrcftParameterProfile.Hybrid, "LipPuckerUpper", true);
            AssertSelectable(lip, VrcftParameterProfile.Hybrid, "LipPuckerLower", true);
        }

        private static void AssertSimplified(string parameterName, string group, params string[] keys)
        {
            var entry = Entry(parameterName);
            Assert.That(entry.Family, Is.EqualTo(VrcftParameterFamily.Simplified), parameterName);
            Assert.That(entry.ConflictGroups, Does.Contain(group), parameterName);
            CollectionAssert.AreEquivalent(keys, entry.ConflictKeys, parameterName);
        }

        private static void AssertDetailed(string parameterName, string group, params string[] keys)
        {
            var entry = Entry(parameterName);
            Assert.That(entry.Family, Is.EqualTo(VrcftParameterFamily.Detailed), parameterName);
            Assert.That(entry.ConflictGroups, Does.Contain(group), parameterName);
            CollectionAssert.AreEquivalent(keys, entry.ConflictKeys, parameterName);
        }

        private static VrcftCatalogEntry Entry(string parameterName)
        {
            return VrcftShapeCatalog.Entries.First(e => e.ParameterName == parameterName);
        }

        private void AssertSelectable(VrcftDetectionReport report, VrcftParameterProfile profile, string parameterName, bool expected)
        {
            var settings = new VrcftAutoSetupSettings
            {
                preset = VrcftPreset.Full,
                parameterProfile = profile,
            };

            Assert.That(report.IsSelectable(Match(report, parameterName), settings), Is.EqualTo(expected), parameterName);
        }

        private static void AssertMatch(VrcftDetectionReport report, string parameterName, params string[] shapeNames)
        {
            var matched = Match(report, parameterName).SlotMatches.Select(m => m.BlendShapeName).ToList();
            foreach (var shapeName in shapeNames)
            {
                Assert.That(matched, Does.Contain(shapeName), parameterName);
            }
        }

        private static ParameterMatch Match(VrcftDetectionReport report, string parameterName)
        {
            return report.Matches.First(m => m.Entry.ParameterName == parameterName);
        }

        private VrcftDetectionReport Detect(params string[] blendShapeNames)
        {
            var avatarObject = new GameObject("Avatar");
            _createdObjects.Add(avatarObject);

            var descriptor = avatarObject.AddComponent<VRCAvatarDescriptor>();
            var meshObject = new GameObject("Face");
            meshObject.transform.SetParent(avatarObject.transform, false);

            var renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = CreateMesh(blendShapeNames);
            descriptor.VisemeSkinnedMesh = renderer;

            return VrcftAvatarDetector.Detect(descriptor);
        }

        private static Mesh CreateMesh(IEnumerable<string> blendShapeNames)
        {
            var mesh = new Mesh
            {
                name = "VRCFT Test Mesh",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                triangles = new[] { 0, 1, 2 },
            };

            var deltaVertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            foreach (var shapeName in blendShapeNames)
            {
                mesh.AddBlendShapeFrame(shapeName, 100f, deltaVertices, null, null);
            }

            return mesh;
        }
    }
}
