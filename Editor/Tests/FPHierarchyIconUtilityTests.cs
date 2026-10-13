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
    using System.IO;
    using System.IO.Compression;
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
        private string previousSettingsPath;
        private string temporaryDirectory;

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
            previousSettingsPath = FPHeaderProjectSettings.SettingsPath;
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "FPHeaderTests_" + System.Guid.NewGuid().ToString("N"));
            FPHeaderProjectSettings.SettingsPath = Path.Combine(temporaryDirectory, "settings.json");
        }

        [TearDown]
        public void TearDown()
        {
            FPHeaderProjectSettings.SettingsPath = previousSettingsPath;
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
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
        public void Palette_BulkAddAppendsUniqueAssetsAndSkipsInvalidItems()
        {
            Texture2D openIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/FP_Utility/Editor/Icons/HH_Open.png");
            Texture2D closeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/FP_Utility/Editor/Icons/HH_Close.png");
            Assert.That(openIcon, Is.Not.Null);
            Assert.That(closeIcon, Is.Not.Null);
            FPHierarchyIconUtility.AddPaletteIcon(closeIcon);

            int addedCount = FPHierarchyIconUtility.AddPaletteIcons(
                new Object[] { closeIcon, null, root, icon, openIcon, openIcon });

            Assert.That(addedCount, Is.EqualTo(1));
            Assert.That(FPHierarchyIconUtility.GetPaletteIcons(), Is.EqualTo(new[] { closeIcon, openIcon }));
            string savedPalette = File.ReadAllText(FPHeaderProjectSettings.SettingsPath);
            Assert.That(FPHierarchyIconUtility.AddPaletteIcons(new Object[] { openIcon, closeIcon }), Is.Zero);
            Assert.That(FPHierarchyIconUtility.AddPaletteIcons(new Object[] { null, root, icon }), Is.Zero);
            Assert.That(FPHierarchyIconUtility.AddPaletteIcons(null), Is.Zero);
            Assert.That(File.ReadAllText(FPHeaderProjectSettings.SettingsPath), Is.EqualTo(savedPalette));
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

        [Test]
        public void Palette_MigratesLegacyOnceAndDoesNotResurrectRemovedEntries()
        {
            Texture2D openIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FP_Utility/Editor/Icons/HH_Open.png");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(openIcon));
            EditorPrefs.SetString(paletteEditorPrefsKey, "{\"Guids\":[\"" + guid + "\"]}");

            Assert.That(FPHierarchyIconUtility.GetPaletteIcons(), Is.EqualTo(new[] { openIcon }));
            Assert.That(File.Exists(FPHeaderProjectSettings.SettingsPath), Is.True);
            Assert.That(FPHierarchyIconUtility.RemovePaletteIcon(openIcon), Is.True);
            Assert.That(FPHierarchyIconUtility.GetPaletteIcons(), Is.Empty);
            Assert.That(EditorPrefs.HasKey(paletteEditorPrefsKey), Is.True, "Keep the source preferences as recovery evidence.");
        }

        [Test]
        public void Palette_ReorderAndRemovalRetainMissingGuids()
        {
            Texture2D openIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FP_Utility/Editor/Icons/HH_Open.png");
            Texture2D closeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FP_Utility/Editor/Icons/HH_Close.png");
            const string missingGuid = "ffffffffffffffffffffffffffffffff";
            FPHeaderProjectSettings.Save(new FPHeaderProjectSettings.Data
            {
                Guids = new List<string> { missingGuid }
            });
            FPHierarchyIconUtility.AddPaletteIcons(new Object[] { openIcon, closeIcon });
            FPHierarchyIconUtility.SetPaletteIcons(new[] { closeIcon, openIcon });
            Assert.That(FPHeaderProjectSettings.Load().Guids[0], Is.EqualTo(missingGuid));
            FPHierarchyIconUtility.RemovePaletteIcon(closeIcon);
            Assert.That(FPHeaderProjectSettings.Load().Guids, Does.Contain(missingGuid));
            Assert.That(FPHeaderProjectSettings.GetMissingReferences(), Does.Contain("Icon GUID: " + missingGuid));
        }

        [Test]
        public void SharedSettings_ExplicitRecoveryMergesLegacyPaletteIntoExistingSharedFile()
        {
            Texture2D openIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FP_Utility/Editor/Icons/HH_Open.png");
            Texture2D closeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FP_Utility/Editor/Icons/HH_Close.png");
            FPHierarchyIconUtility.AddPaletteIcon(closeIcon);
            string closeGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(closeIcon));
            string openGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(openIcon));
            string source = "{\"Guids\":[\"" + openGuid + "\",\"ffffffffffffffffffffffffffffffff\"]}";
            EditorPrefs.SetString(paletteEditorPrefsKey, source);
            Assert.That(FPHierarchyIconUtility.GetPaletteIcons(), Is.EqualTo(new[] { closeIcon }));
            FPHeaderProjectSettings.RecoverLocalSettings();
            Assert.That(FPHeaderProjectSettings.Load().Guids,
                Is.EqualTo(new[] { closeGuid, openGuid, "ffffffffffffffffffffffffffffffff" }));
            Assert.That(EditorPrefs.GetString(paletteEditorPrefsKey), Is.EqualTo(source));
        }

        [Test]
        public void SharedSettings_MergePreservesExistingSceneSelectionAndIconOrder()
        {
            var current = new FPHeaderProjectSettings.Data
            {
                Guids = new List<string> { "first", "missing" },
                Scenes = new List<FPHeaderProjectSettings.SceneStyle>
                {
                    new() { SceneGuid = "scene", StyleGuid = "currentStyle" }
                }
            };
            var incoming = new FPHeaderProjectSettings.Data
            {
                Guids = new List<string> { "missing", "second" },
                Scenes = new List<FPHeaderProjectSettings.SceneStyle>
                {
                    new() { SceneGuid = "scene", StyleGuid = "oldStyle" },
                    new() { SceneGuid = "otherScene", StyleGuid = "otherStyle" }
                }
            };
            Assert.That(FPHeaderProjectSettings.Merge(current, incoming), Is.EqualTo(2));
            Assert.That(current.Guids, Is.EqualTo(new[] { "first", "missing", "second" }));
            Assert.That(current.Scenes[0].StyleGuid, Is.EqualTo("currentStyle"));
            Assert.That(FPHeaderProjectSettings.Merge(current, incoming), Is.Zero);
        }

        [Test]
        public void SharedSettings_CorruptOrFutureFileIsNotOverwritten()
        {
            Directory.CreateDirectory(temporaryDirectory);
            const string original = "{\"Version\":99,\"Guids\":[]}";
            File.WriteAllText(FPHeaderProjectSettings.SettingsPath, original);
            Assert.Throws<InvalidDataException>(() => FPHierarchyIconUtility.AddPaletteIcon(
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FP_Utility/Editor/Icons/HH_Open.png")));
            Assert.That(File.ReadAllText(FPHeaderProjectSettings.SettingsPath), Is.EqualTo(original));
            Assert.Throws<InvalidDataException>(() => FPHeaderProjectSettings.Parse("{}"));
        }

        [Test]
        public void SharedSettings_ExportAndMergeRoundTripRetainsMissingReferencesAndAssets()
        {
            Texture2D openIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FP_Utility/Editor/Icons/HH_Open.png");
            FPHierarchyIconUtility.AddPaletteIcon(openIcon);
            var data = FPHeaderProjectSettings.Load();
            data.Guids.Add("ffffffffffffffffffffffffffffffff");
            const string stylePath = "Assets/FP_Utility/Editor/FP_HHeader/FP_HHeaderData_JFUZZ.asset";
            data.Scenes.Add(new FPHeaderProjectSettings.SceneStyle
            {
                SceneGuid = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })[0],
                StyleGuid = AssetDatabase.AssetPathToGUID(stylePath),
                LegacyStylePath = stylePath
            });
            FPHeaderProjectSettings.Save(data);
            string transferPath = Path.Combine(temporaryDirectory, "transfer.json");
            FPHeaderProjectSettings.Export(transferPath, false);
            Assert.That(File.Exists(Path.ChangeExtension(transferPath, ".unitypackage")), Is.True);
            Assert.That(new FileInfo(Path.ChangeExtension(transferPath, ".unitypackage")).Length, Is.GreaterThan(0));
            using (var package = File.OpenRead(Path.ChangeExtension(transferPath, ".unitypackage")))
            using (var archive = new GZipStream(package, CompressionMode.Decompress))
            using (var reader = new StreamReader(archive))
            {
                string contents = reader.ReadToEnd();
                Assert.That(contents, Does.Contain("Assets/FP_Utility/Editor/Icons/HH_Open.png"));
                Assert.That(contents, Does.Contain("guid: " + data.Guids[0]));
                Assert.That(contents, Does.Contain(stylePath));
                Assert.That(contents, Does.Not.Contain("FP_HHeaderData.cs"), "The destination supplies the installed package code.");
            }
            FPHeaderProjectSettings.Save(new FPHeaderProjectSettings.Data());
            Assert.That(FPHeaderProjectSettings.Import(transferPath), Is.EqualTo(3));
            Assert.That(FPHeaderProjectSettings.Load().Guids, Is.EqualTo(data.Guids));
            Assert.That(FPHierarchyIconUtility.GetPaletteIcons(), Is.EqualTo(new[] { openIcon }));
            Assert.That(FPHeaderProjectSettings.Import(transferPath), Is.Zero);
        }

        [Test]
        public void SharedSettings_MissingStyleGuidDoesNotBindAssetAtLegacyPath()
        {
            var style = new FPHeaderProjectSettings.SceneStyle
            {
                SceneGuid = "scene",
                StyleGuid = "ffffffffffffffffffffffffffffffff",
                LegacyStylePath = "Assets/FP_Utility/Editor/FP_HHeader/FP_HHeaderData_JFUZZ.asset"
            };
            Assert.That(FPHeaderProjectSettings.ResolveStylePath(style), Is.Empty);
        }

        [Test]
        public void SharedSettings_SceneStyleMigratesAndSurvivesSceneRename()
        {
            string folderName = "_FPHeaderTests_" + System.Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            string scenePath = folder + "/" + folderName + ".unity";
            string legacyKey = FP_UtilityData.FP_HEADERSTYLE_VALUE + "_" + folderName;
            const string stylePath = "Assets/FP_Utility/Editor/FP_HHeader/FP_HHeaderData_JFUZZ.asset";
            try
            {
                AssetDatabase.CreateFolder("Assets", folderName);
                string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                Assert.That(scenes, Is.Not.Empty);
                Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GUIDToAssetPath(scenes[0]), scenePath), Is.True);
                EditorPrefs.SetString(legacyKey, stylePath);
                Assert.That(FPHeaderProjectSettings.GetStylePath(scenePath, folderName), Is.EqualTo(stylePath));
                EditorPrefs.DeleteKey(legacyKey);
                string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                Assert.That(FPHeaderProjectSettings.Load().Scenes[0].SceneGuid, Is.EqualTo(sceneGuid));
                string movedPath = folder + "/Renamed.unity";
                Assert.That(AssetDatabase.MoveAsset(scenePath, movedPath), Is.Empty);
                Assert.That(FPHeaderProjectSettings.GetStylePath(movedPath, "Renamed"), Is.EqualTo(stylePath));
            }
            finally
            {
                EditorPrefs.DeleteKey(legacyKey);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
