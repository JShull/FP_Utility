// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    // Project-owned data deliberately lives outside the updatable package and local preferences.
    internal static class FPHeaderProjectSettings
    {
        [Serializable]
        internal sealed class SceneStyle
        {
            public string SceneGuid;
            public string StyleGuid;
            public string LegacyStylePath;
        }

        [Serializable]
        internal sealed class Data
        {
            public int Version = 1;
            public List<string> Guids = new();
            public List<SceneStyle> Scenes = new();
        }

        internal static string SettingsPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "../ProjectSettings/FPHeaderSettings.json"));

        internal static Data Load()
        {
            if (File.Exists(SettingsPath))
            {
                return Parse(File.ReadAllText(SettingsPath));
            }

            string legacy = EditorPrefs.GetString(FPHierarchyIconUtility.PaletteEditorPrefsKey, string.Empty);
            Data data = string.IsNullOrWhiteSpace(legacy) ? new Data() : Parse(legacy, true);
            Save(data);
            return data;
        }

        internal static Data Parse(string json, bool legacy = false)
        {
            // FromJson ignores unknown fields. Require our schema before accepting a transfer file.
            if (string.IsNullOrWhiteSpace(json) || !json.Contains("\"Guids\"") ||
                (!legacy && !json.Contains("\"Version\"")))
            {
                throw new InvalidDataException("This is not an FP Header settings file.");
            }
            Data data = JsonUtility.FromJson<Data>(json);
            if (data == null || (!legacy && data.Version != 1) || data.Guids == null)
            {
                throw new InvalidDataException("Unsupported or invalid FP Header settings. The original file was preserved.");
            }
            data.Version = 1;
            data.Scenes ??= new List<SceneStyle>();
            var unique = new HashSet<string>();
            data.Guids.RemoveAll(guid => string.IsNullOrWhiteSpace(guid) || !unique.Add(guid));
            unique.Clear();
            data.Scenes.RemoveAll(scene => scene == null || string.IsNullOrWhiteSpace(scene.SceneGuid) ||
                !unique.Add(scene.SceneGuid));
            return data;
        }

        internal static void Save(Data data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            string temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
            if (File.Exists(SettingsPath))
            {
                File.Replace(temporaryPath, SettingsPath, SettingsPath + ".bak");
            }
            else
            {
                File.Move(temporaryPath, SettingsPath);
            }
            EditorApplication.RepaintHierarchyWindow();
        }

        internal static string GetStylePath(Scene scene) => GetStylePath(scene.path, scene.name);

        internal static string GetStylePath(string scenePath, string sceneName)
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            Data data = Load();
            SceneStyle style = data.Scenes.Find(item => item.SceneGuid == sceneGuid);
            if (style != null)
            {
                if (string.IsNullOrEmpty(style.StyleGuid))
                {
                    string guid = AssetDatabase.AssetPathToGUID(style.LegacyStylePath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        style.StyleGuid = guid;
                        Save(data);
                    }
                }
                return ResolveStylePath(style);
            }

            string legacyPath = EditorPrefs.GetString(FP_UtilityData.FP_HEADERSTYLE_VALUE + "_" + sceneName, string.Empty);
            legacyPath = legacyPath.Replace('\\', '/');
            if (!string.IsNullOrEmpty(sceneGuid) && !string.IsNullOrEmpty(legacyPath))
            {
                if (!IsUniqueSceneName(sceneName))
                {
                    return string.Empty;
                }
                SetStyle(data, sceneGuid, legacyPath);
                Save(data);
            }
            return legacyPath;
        }

        private static bool IsUniqueSceneName(string name)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == name && ++count > 1)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool SetStyle(Scene scene, string stylePath)
        {
            string sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
            if (string.IsNullOrEmpty(sceneGuid))
            {
                Debug.LogWarning("Save the scene, then apply the FP Header style to share its selection.");
                return false;
            }
            Data data = Load();
            SetStyle(data, sceneGuid, stylePath);
            Save(data);
            return true;
        }

        private static void SetStyle(Data data, string sceneGuid, string stylePath)
        {
            stylePath = stylePath.Replace('\\', '/');
            SceneStyle style = data.Scenes.Find(item => item.SceneGuid == sceneGuid);
            if (style == null)
            {
                style = new SceneStyle { SceneGuid = sceneGuid };
                data.Scenes.Add(style);
            }
            style.StyleGuid = AssetDatabase.AssetPathToGUID(stylePath);
            style.LegacyStylePath = stylePath;
        }

        internal static string ResolveStylePath(SceneStyle style)
        {
            // Once a GUID exists, never bind a replacement asset that happens to occupy the old path.
            return string.IsNullOrEmpty(style.StyleGuid)
                ? style.LegacyStylePath
                : AssetDatabase.GUIDToAssetPath(style.StyleGuid);
        }

        internal static int Merge(Data current, Data incoming)
        {
            int added = 0;
            foreach (string guid in incoming.Guids)
            {
                if (!current.Guids.Contains(guid))
                {
                    current.Guids.Add(guid);
                    added++;
                }
            }
            foreach (SceneStyle scene in incoming.Scenes)
            {
                if (!current.Scenes.Exists(item => item.SceneGuid == scene.SceneGuid))
                {
                    current.Scenes.Add(scene);
                    added++;
                }
            }
            return added;
        }

        internal static int RecoverLocalSettings()
        {
            Data current = Load();
            string legacy = EditorPrefs.GetString(FPHierarchyIconUtility.PaletteEditorPrefsKey, string.Empty);
            Data recovered = string.IsNullOrWhiteSpace(legacy) ? new Data() : Parse(legacy, true);
            foreach (string sceneGuid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuid);
                string stylePath = EditorPrefs.GetString(FP_UtilityData.FP_HEADERSTYLE_VALUE + "_" +
                    Path.GetFileNameWithoutExtension(path), string.Empty);
                if (!string.IsNullOrEmpty(stylePath) && IsUniqueSceneName(Path.GetFileNameWithoutExtension(path)))
                {
                    SetStyle(recovered, sceneGuid, stylePath);
                }
            }
            int added = Merge(current, recovered);
            Save(current);
            return added;
        }

        internal static List<string> GetMissingReferences()
        {
            Data data = Load();
            var missing = new List<string>();
            foreach (string guid in data.Guids)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)) == null)
                {
                    missing.Add("Icon GUID: " + guid);
                }
            }
            foreach (SceneStyle scene in data.Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<FP_HHeaderData>(ResolveStylePath(scene)) == null)
                {
                    missing.Add("Style for scene GUID " + scene.SceneGuid + ": " +
                        (string.IsNullOrEmpty(scene.StyleGuid) ? scene.LegacyStylePath : scene.StyleGuid));
                }
            }
            return missing;
        }

        internal static void Export(string jsonPath, bool includeScenes)
        {
            Data data = Load();
            var paths = new HashSet<string>();
            foreach (string guid in data.Guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null)
                {
                    paths.Add(path);
                }
            }
            foreach (SceneStyle scene in data.Scenes)
            {
                string path = ResolveStylePath(scene);
                if (AssetDatabase.LoadAssetAtPath<FP_HHeaderData>(path) != null)
                {
                    paths.Add(path);
                }
            }
            if (includeScenes)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }
            if (paths.Count > 0)
            {
                var assets = new string[paths.Count];
                paths.CopyTo(assets);
                ExportPackageOptions options = ExportPackageOptions.IncludeDependencies;
                if (!includeScenes)
                {
                    // Styles only reference textures. Transfer these without exporting installed package scripts.
                    foreach (string dependency in AssetDatabase.GetDependencies(assets, true))
                    {
                        if (AssetDatabase.LoadAssetAtPath<Texture2D>(dependency) != null)
                        {
                            paths.Add(dependency);
                        }
                    }
                    assets = new string[paths.Count];
                    paths.CopyTo(assets);
                    options = ExportPackageOptions.Default;
                }
#if UNITY_6000_6_OR_NEWER
                UnityEditor.AssetPackage.Package.Export(new UnityEditor.AssetPackage.ExportPackageParameters(
                    assets, Path.ChangeExtension(jsonPath, ".unitypackage"), null, options));
#else
                AssetDatabase.ExportPackage(assets, Path.ChangeExtension(jsonPath, ".unitypackage"), options);
#endif
            }
            else if (File.Exists(Path.ChangeExtension(jsonPath, ".unitypackage")))
            {
                throw new IOException("No assets are available to export, but an older companion package exists. Choose a fresh export filename.");
            }
            File.WriteAllText(jsonPath, JsonUtility.ToJson(data, true));
        }

        internal static int Import(string jsonPath)
        {
            Data incoming = Parse(File.ReadAllText(jsonPath));
            Data current = Load();
            int added = Merge(current, incoming);
            Save(current);
            return added;
        }
    }
}
