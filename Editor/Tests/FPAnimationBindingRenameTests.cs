// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class FPAnimationBindingRenameTests
    {
        private const string OldName = "blendShape.blendShape1.jawFowad";
        private const string NewName = "blendShape.blendShape1.jawForward";
        private AnimationClip clip;
        private EditorCurveBinding binding;
        private string folder;

        [SetUp]
        public void SetUp()
        {
            clip = new AnimationClip { name = "Face Recording", frameRate = 90, wrapMode = WrapMode.Loop };
            binding = EditorCurveBinding.FloatCurve("Character/Head", typeof(SkinnedMeshRenderer), OldName);
            var curve = new AnimationCurve(new Keyframe(0, 2, -1, 3, 0.2f, 0.35f) { weightedMode = WeightedMode.Both },
                new Keyframe(1.25f, 85, -3, 0, 0.4f, 0.15f) { weightedMode = WeightedMode.Both });
            curve.preWrapMode = WrapMode.PingPong;
            curve.postWrapMode = WrapMode.Loop;
            AnimationUtility.SetKeyBroken(curve, 0, true);
            AnimationUtility.SetKeyLeftTangentMode(curve, 0, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyRightTangentMode(curve, 0, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            var other = EditorCurveBinding.FloatCurve("Character/Head", typeof(SkinnedMeshRenderer), "blendShape.blink");
            AnimationUtility.SetEditorCurve(clip, other, AnimationCurve.Linear(0, 0, 1.25f, 100));
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = 0.5f, functionName = "Marker", stringParameter = "keep", intParameter = 7 } });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        [TearDown]
        public void TearDown()
        {
            if (clip != null) Undo.ClearUndo(clip);
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
            if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void Rename_ChangesOneBindingPreservingKeysTangentsPathAndClipMetadata()
        {
            AnimationCurve before = AnimationUtility.GetEditorCurve(clip, binding);
            var other = AnimationUtility.GetCurveBindings(clip).Single(b => b.propertyName != OldName);
            AnimationCurve otherBefore = AnimationUtility.GetEditorCurve(clip, other);
            FPAnimationBindingRenameUtility.Rename(clip, binding, NewName, false);
            Assert.That(AnimationUtility.GetEditorCurve(clip, binding), Is.Null);
            EditorCurveBinding renamed = AnimationUtility.GetCurveBindings(clip).Single(b => b.propertyName == NewName);
            Assert.That(renamed.path, Is.EqualTo(binding.path));
            Assert.That(renamed.type, Is.EqualTo(binding.type));
            AssertCurveEqual(before, AnimationUtility.GetEditorCurve(clip, renamed));
            AssertCurveEqual(otherBefore, AnimationUtility.GetEditorCurve(clip, other));
            AssertMetadata(clip);
        }

        [Test]
        public void Rename_SupportsUndoAndRedo()
        {
            FPAnimationBindingRenameUtility.Rename(clip, binding, NewName);
            Undo.FlushUndoRecordObjects();
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == NewName), Is.True);
            Undo.PerformUndo();
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == OldName), Is.True);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == NewName), Is.False);
            Undo.PerformRedo();
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == NewName), Is.True);
        }

        [Test]
        public void Rename_ObjectReferenceCurveKeepsEveryKeyAndReference()
        {
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                var original = EditorCurveBinding.PPtrCurve("Body", typeof(MeshRenderer), "m_Materials.Array.data[0]");
                var keys = new[] { new ObjectReferenceKeyframe { time = 0, value = material }, new ObjectReferenceKeyframe { time = 1, value = null } };
                AnimationUtility.SetObjectReferenceCurve(clip, original, keys);
                FPAnimationBindingRenameUtility.Rename(clip, original, "m_Materials.Array.data[1]", false);
                var renamed = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                Assert.That(renamed.propertyName, Is.EqualTo("m_Materials.Array.data[1]"));
                Assert.That(renamed.path, Is.EqualTo(original.path));
                Assert.That(renamed.isPPtrCurve, Is.True);
                Assert.That(AnimationUtility.GetObjectReferenceCurve(clip, renamed).Select(k => k.time), Is.EqualTo(keys.Select(k => k.time)));
                Assert.That(AnimationUtility.GetObjectReferenceCurve(clip, renamed).Select(k => k.value), Is.EqualTo(keys.Select(k => k.value)));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void Rename_DiscreteCurveRetainsItsBindingFlags()
        {
            var original = EditorCurveBinding.DiscreteCurve("Body", typeof(GameObject), "m_IsActive");
            AnimationUtility.SetEditorCurve(clip, original, AnimationCurve.Constant(0, 1, 1));
            original = AnimationUtility.GetCurveBindings(clip).Single(b => b.propertyName == "m_IsActive");
            FPAnimationBindingRenameUtility.Rename(clip, original, "m_IsActiveRenamed", false);
            var renamed = AnimationUtility.GetCurveBindings(clip).Single(b => b.propertyName == "m_IsActiveRenamed");
            Assert.That(renamed.isDiscreteCurve, Is.EqualTo(original.isDiscreteCurve));
            Assert.That(AnimationUtility.GetEditorCurve(clip, renamed).keys.Select(k => k.value), Is.All.EqualTo(1));
        }

        [Test]
        public void CollisionOrStaleBindingIsRejectedWithoutRemovingCurves()
        {
            string snapshot = EditorJsonUtility.ToJson(clip);
            Assert.Throws<ArgumentException>(() => FPAnimationBindingRenameUtility.Rename(clip, binding, "blendShape.blink", false));
            var missing = binding;
            missing.propertyName = "blendShape.missing";
            Assert.Throws<ArgumentException>(() => FPAnimationBindingRenameUtility.Rename(clip, missing, NewName, false));
            Assert.That(EditorJsonUtility.ToJson(clip), Is.EqualTo(snapshot));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("bad\nname")]
        [TestCase("bad\0name")]
        [TestCase(" blendShape.name")]
        [TestCase("blendShape.")]
        [TestCase("jawForward")]
        public void InvalidBlendshapePropertiesAreRejected(string name)
        {
            Assert.Throws<ArgumentException>(() => FPAnimationBindingRenameUtility.Rename(clip, binding, name, false));
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == OldName), Is.True);
        }

        [Test]
        public void SameNameOnAnotherRendererDoesNotCauseACollision()
        {
            var other = binding;
            other.path = "Other/Head";
            other.propertyName = NewName;
            AnimationUtility.SetEditorCurve(clip, other, AnimationCurve.Constant(0, 1, 5));
            FPAnimationBindingRenameUtility.Rename(clip, binding, NewName, false);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Count(b => b.propertyName == NewName), Is.EqualTo(2));
        }

        [Test]
        public void SaveCopy_RoundTripsNewAnimAndPreservesOriginalBytesAndGuid()
        {
            CreateFolder();
            string sourcePath = folder + "/Original.anim";
            AssetDatabase.CreateAsset(clip, sourcePath);
            AssetDatabase.SaveAssetIfDirty(clip);
            byte[] before = File.ReadAllBytes(sourcePath);
            string guid = AssetDatabase.AssetPathToGUID(sourcePath);
            Assert.That(FPAnimationBindingRenameUtility.CanEditInPlace(clip), Is.True);
            string outputPath = folder + "/Renamed.anim";
            AnimationClip copy = FPAnimationBindingRenameUtility.SaveCopy(clip, binding, NewName, outputPath);
            Assert.That(AnimationUtility.GetCurveBindings(copy).Any(b => b.propertyName == NewName), Is.True);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == OldName), Is.True);
            Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(sourcePath), Is.EqualTo(guid));
            Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.Not.EqualTo(guid));
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            var renamed = AnimationUtility.GetCurveBindings(copy).Single(b => b.propertyName == NewName);
            AssertCurveEqual(AnimationUtility.GetEditorCurve(clip, binding), AnimationUtility.GetEditorCurve(copy, renamed));
            AssertMetadata(copy);
            Assert.Throws<IOException>(() => FPAnimationBindingRenameUtility.SaveCopy(clip, binding, NewName, outputPath));
            Assert.Throws<IOException>(() => FPAnimationBindingRenameUtility.SaveCopy(clip, binding, NewName, sourcePath));
            Assert.Throws<ArgumentException>(() => FPAnimationBindingRenameUtility.SaveCopy(clip, binding, NewName, "../Invalid.anim"));
        }

        [Test]
        public void InPlace_SaveKeepsAssetIdentityAndReloadsCorrectBinding()
        {
            CreateFolder();
            string path = folder + "/Original.anim";
            AssetDatabase.CreateAsset(clip, path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            FPAnimationBindingRenameUtility.Rename(clip, binding, NewName);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == NewName), Is.True);
            Assert.That(AnimationUtility.GetCurveBindings(clip).Any(b => b.propertyName == OldName), Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
        }

        private void CreateFolder()
        {
            folder = AssetDatabase.GenerateUniqueAssetPath("Assets/__FPAnimationBindingTests");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        }

        private static void AssertMetadata(AnimationClip target)
        {
            Assert.That(target.frameRate, Is.EqualTo(90));
            Assert.That(target.wrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(AnimationUtility.GetAnimationClipSettings(target).loopTime, Is.True);
            AnimationEvent e = AnimationUtility.GetAnimationEvents(target).Single();
            Assert.That(e.time, Is.EqualTo(0.5f));
            Assert.That(e.functionName, Is.EqualTo("Marker"));
            Assert.That(e.stringParameter, Is.EqualTo("keep"));
            Assert.That(e.intParameter, Is.EqualTo(7));
        }

        private static void AssertCurveEqual(AnimationCurve expected, AnimationCurve actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.keys, Is.EqualTo(expected.keys));
            Assert.That(actual.preWrapMode, Is.EqualTo(expected.preWrapMode));
            Assert.That(actual.postWrapMode, Is.EqualTo(expected.postWrapMode));
            for (int i = 0; i < expected.length; i++)
            {
                Assert.That(AnimationUtility.GetKeyBroken(actual, i), Is.EqualTo(AnimationUtility.GetKeyBroken(expected, i)));
                Assert.That(AnimationUtility.GetKeyLeftTangentMode(actual, i), Is.EqualTo(AnimationUtility.GetKeyLeftTangentMode(expected, i)));
                Assert.That(AnimationUtility.GetKeyRightTangentMode(actual, i), Is.EqualTo(AnimationUtility.GetKeyRightTangentMode(expected, i)));
            }
        }
    }
}
