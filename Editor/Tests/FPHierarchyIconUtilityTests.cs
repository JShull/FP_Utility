// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class FPHierarchyIconUtilityTests
    {
        private Scene previewScene;
        private GameObject root;
        private GameObject child;
        private Texture2D icon;
        private string paletteEditorPrefsKey;
        private string previousPaletteJson;
        private bool hadPreviousPalette;

        [SetUp]
        public void SetUp()
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Root");
            child = new GameObject("Child");
            SceneManager.MoveGameObjectToScene(root, previewScene);
            SceneManager.MoveGameObjectToScene(child, previewScene);
            child.transform.SetParent(root.transform);
            icon = new Texture2D(16, 16);
            paletteEditorPrefsKey = FPHierarchyIconUtility.PaletteEditorPrefsKey;
            hadPreviousPalette = EditorPrefs.HasKey(paletteEditorPrefsKey);
            previousPaletteJson = EditorPrefs.GetString(paletteEditorPrefsKey, string.Empty);
            EditorPrefs.DeleteKey(paletteEditorPrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Undo.ClearUndo(root);
            }

            if (child != null)
            {
                Undo.ClearUndo(child);
            }

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            if (icon != null)
            {
                Object.DestroyImmediate(icon);
            }

            if (hadPreviousPalette)
            {
                EditorPrefs.SetString(paletteEditorPrefsKey, previousPaletteJson);
            }
            else
            {
                EditorPrefs.DeleteKey(paletteEditorPrefsKey);
            }
        }

        [Test]
        public void CollectTargets_RecursiveOff_OnlyIncludesExplicitRoot()
        {
            List<GameObject> targets = FPHierarchyIconUtility.CollectTargets(
                new[] { root },
                false,
                out int skippedHeaders,
                out int skippedNonScene);

            Assert.That(targets, Is.EqualTo(new[] { root }));
            Assert.That(skippedHeaders, Is.Zero);
            Assert.That(skippedNonScene, Is.Zero);
        }

        [Test]
        public void CollectTargets_RecursiveOn_IncludesChildren()
        {
            List<GameObject> targets = FPHierarchyIconUtility.CollectTargets(
                new[] { root },
                true,
                out int skippedHeaders,
                out int skippedNonScene);

            Assert.That(targets, Is.EqualTo(new[] { root, child }));
            Assert.That(skippedHeaders, Is.Zero);
            Assert.That(skippedNonScene, Is.Zero);
        }

        [Test]
        public void CollectTargets_SkipsFPHeaderObjects()
        {
            var header = new GameObject("HEADER");
            SceneManager.MoveGameObjectToScene(header, previewScene);
            header.SetActive(false);

            List<GameObject> targets = FPHierarchyIconUtility.CollectTargets(
                new[] { header },
                false,
                out int skippedHeaders,
                out int skippedNonScene);

            Assert.That(targets, Is.Empty);
            Assert.That(skippedHeaders, Is.EqualTo(1));
            Assert.That(skippedNonScene, Is.Zero);
        }

        [Test]
        public void ApplyAndClearIcon_UpdatesNativeSerializedOverride()
        {
            List<GameObject> targets = FPHierarchyIconUtility.CollectTargets(
                new[] { root },
                false,
                out _,
                out _);

            int appliedCount = FPHierarchyIconUtility.ApplyIcon(targets, icon);
            var serializedRoot = new SerializedObject(root);

            Assert.That(appliedCount, Is.EqualTo(1));
            Assert.That(FPHierarchyIconUtility.GetIcon(root), Is.SameAs(icon));
            Assert.That(serializedRoot.FindProperty("m_Icon").objectReferenceValue, Is.SameAs(icon));
            Assert.That(FPHierarchyIconUtility.GetIcon(child), Is.Null);

            int clearedCount = FPHierarchyIconUtility.ClearIcons(targets);

            Assert.That(clearedCount, Is.EqualTo(1));
            Assert.That(FPHierarchyIconUtility.GetIcon(root), Is.Null);
        }

        [Test]
        public void ApplyIcon_RecordsUndo()
        {
            List<GameObject> targets = FPHierarchyIconUtility.CollectTargets(
                new[] { root },
                false,
                out _,
                out _);

            FPHierarchyIconUtility.ApplyIcon(targets, icon);
            Undo.PerformUndo();

            Assert.That(FPHierarchyIconUtility.GetIcon(root), Is.Null);
        }

        [Test]
        public void ApplyIcon_DirectCallStillSkipsFPHeaderObjects()
        {
            var header = new GameObject("HEADER");
            SceneManager.MoveGameObjectToScene(header, previewScene);
            header.SetActive(false);

            int appliedCount = FPHierarchyIconUtility.ApplyIcon(new[] { header }, icon);

            Assert.That(appliedCount, Is.Zero);
            Assert.That(FPHierarchyIconUtility.GetIcon(header), Is.Null);
        }

        [Test]
        public void Palette_AddPreventsDuplicatesAndRemoveClearsEntry()
        {
            Texture2D assetIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/FP_Utility/Editor/Icons/HH_Open.png");

            Assert.That(assetIcon, Is.Not.Null);
            Assert.That(FPHierarchyIconUtility.AddPaletteIcon(assetIcon), Is.True);
            Assert.That(FPHierarchyIconUtility.AddPaletteIcon(assetIcon), Is.False);
            Assert.That(FPHierarchyIconUtility.PaletteContains(assetIcon), Is.True);
            Assert.That(FPHierarchyIconUtility.GetPaletteIcons(), Is.EqualTo(new[] { assetIcon }));
            Assert.That(FPHierarchyIconUtility.RemovePaletteIcon(assetIcon), Is.True);
            Assert.That(FPHierarchyIconUtility.GetPaletteIcons(), Is.Empty);
        }

        [Test]
        public void Palette_SetOrderPersistsAndRemovesDuplicates()
        {
            Texture2D openIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/FP_Utility/Editor/Icons/HH_Open.png");
            Texture2D closeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/FP_Utility/Editor/Icons/HH_Close.png");

            Assert.That(openIcon, Is.Not.Null);
            Assert.That(closeIcon, Is.Not.Null);
            Assert.That(
                FPHierarchyIconUtility.SetPaletteIcons(new[] { openIcon, closeIcon }),
                Is.True);
            Assert.That(
                FPHierarchyIconUtility.GetPaletteIcons(),
                Is.EqualTo(new[] { openIcon, closeIcon }));

            Assert.That(
                FPHierarchyIconUtility.SetPaletteIcons(new[] { closeIcon, openIcon, closeIcon, null }),
                Is.True);
            Assert.That(
                FPHierarchyIconUtility.GetPaletteIcons(),
                Is.EqualTo(new[] { closeIcon, openIcon }));
            Assert.That(
                FPHierarchyIconUtility.SetPaletteIcons(new[] { closeIcon, openIcon }),
                Is.False);
        }
    }
}
