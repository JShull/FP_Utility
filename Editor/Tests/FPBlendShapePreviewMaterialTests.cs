// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.Reflection;
    using System.IO;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class FPBlendShapePreviewMaterialTests
    {
        private FPBlendShapeRenameWindow window;
        private GameObject source;
        private Material first;
        private Material second;
        private Material replacement;
        private Renderer[] sourceRenderers;
        private string folder;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            first = new Material(shader) { name = "First original" };
            second = new Material(shader) { name = "Second original" };
            replacement = new Material(shader) { name = "Replacement" };
            folder = AssetDatabase.GenerateUniqueAssetPath("Assets/__FPPreviewMaterialTests");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            AssetDatabase.CreateAsset(first, folder + "/First.mat");
            AssetDatabase.CreateAsset(second, folder + "/Second.mat");
            AssetDatabase.CreateAsset(replacement, folder + "/Replacement.mat");
            source = new GameObject("Material source");
            var a = new GameObject("Mesh");
            var b = new GameObject("Mesh");
            a.transform.SetParent(source.transform, false);
            b.transform.SetParent(source.transform, false);
            var rendererA = a.AddComponent<MeshRenderer>();
            var rendererB = b.AddComponent<SkinnedMeshRenderer>();
            rendererA.sharedMaterials = new[] { first, second };
            rendererB.sharedMaterials = new[] { first };
            sourceRenderers = new Renderer[] { rendererA, rendererB };
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Model.prefab");
            window = ScriptableObject.CreateInstance<FPBlendShapeRenameWindow>();
            var settings = new SerializedObject(window);
            settings.FindProperty("sourceModel").objectReferenceValue = prefab;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Invoke("BuildPreview");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(window);
            Object.DestroyImmediate(source);
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void Discovery_ListsEverySlotIncludingSharedMaterialsAndDuplicateRendererNames()
        {
            var settings = new SerializedObject(window);
            var slots = settings.FindProperty("materialSlots");
            Assert.That(slots.arraySize, Is.EqualTo(3));
            Assert.That(slots.GetArrayElementAtIndex(0).FindPropertyRelative("original").objectReferenceValue, Is.SameAs(first));
            Assert.That(slots.GetArrayElementAtIndex(1).FindPropertyRelative("original").objectReferenceValue, Is.SameAs(second));
            Assert.That(slots.GetArrayElementAtIndex(2).FindPropertyRelative("original").objectReferenceValue, Is.SameAs(first));
            Assert.That(slots.GetArrayElementAtIndex(0).FindPropertyRelative("key").stringValue,
                Is.Not.EqualTo(slots.GetArrayElementAtIndex(2).FindPropertyRelative("key").stringValue));
        }

        [Test]
        public void AllSlots_OverridesEveryRendererAndClearingRestoresEachOriginal()
        {
            SetGlobal(replacement);
            Renderer[] previewRenderers = PreviewRenderers();
            Assert.That(previewRenderers[0].sharedMaterials, Is.EqualTo(new[] { replacement, replacement }));
            Assert.That(previewRenderers[1].sharedMaterials, Is.EqualTo(new[] { replacement }));
            AssertSourceUnchanged();
            SetGlobal(null);
            Assert.That(previewRenderers[0].sharedMaterials, Is.EqualTo(new[] { first, second }));
            Assert.That(previewRenderers[1].sharedMaterials, Is.EqualTo(new[] { first }));
        }

        [Test]
        public void IndividualSlots_ChangesOnlySelectedSlotEvenWhenOriginalIsShared()
        {
            SetGlobal(second);
            SetSlot(0, replacement);
            Renderer[] previewRenderers = PreviewRenderers();
            Assert.That(previewRenderers[0].sharedMaterials, Is.EqualTo(new[] { replacement, second }));
            Assert.That(previewRenderers[1].sharedMaterials, Is.EqualTo(new[] { first }));
            AssertSourceUnchanged();
            SetSlot(2, replacement);
            Assert.That(previewRenderers[1].sharedMaterials, Is.EqualTo(new[] { replacement }));
            SetSlot(0, null);
            Assert.That(previewRenderers[0].sharedMaterials, Is.EqualTo(new[] { first, second }));
            Assert.That(previewRenderers[1].sharedMaterials, Is.EqualTo(new[] { replacement }));
        }

        [Test]
        public void Rebuild_RetainsSlotOverridesAndNewSourceClearsThem()
        {
            SetSlot(1, replacement);
            Invoke("CleanupPreview");
            Invoke("BuildPreview");
            Assert.That(PreviewRenderers()[0].sharedMaterials, Is.EqualTo(new[] { first, replacement }));
            Assert.That(PreviewRenderers()[1].sharedMaterials, Is.EqualTo(new[] { first }));
            AssertSourceUnchanged();
            window.LoadSource(null);
            Assert.That(new SerializedObject(window).FindProperty("materialSlots").arraySize, Is.Zero);
        }

        private void SetGlobal(Material material)
        {
            var settings = new SerializedObject(window);
            settings.FindProperty("materialOverrideMode").enumValueIndex = 0;
            settings.FindProperty("previewMaterial").objectReferenceValue = material;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Invoke("ApplyPreviewMaterials");
        }

        private void SetSlot(int index, Material material)
        {
            var settings = new SerializedObject(window);
            settings.FindProperty("materialOverrideMode").enumValueIndex = 1;
            settings.FindProperty("materialSlots").GetArrayElementAtIndex(index).FindPropertyRelative("replacement").objectReferenceValue = material;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Invoke("ApplyPreviewMaterials");
        }

        private Renderer[] PreviewRenderers() => (Renderer[])typeof(FPBlendShapeRenameWindow).GetField("renderers", Flags).GetValue(window);
        private void Invoke(string method) => typeof(FPBlendShapeRenameWindow).GetMethod(method, Flags).Invoke(window, null);
        private void AssertSourceUnchanged()
        {
            Assert.That(sourceRenderers[0].sharedMaterials, Is.EqualTo(new[] { first, second }));
            Assert.That(sourceRenderers[1].sharedMaterials, Is.EqualTo(new[] { first }));
        }
    }
}
