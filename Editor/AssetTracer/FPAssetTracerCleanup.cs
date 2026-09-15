// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>Conservative, target-specific review of missing-prefab property overrides.</summary>
    internal static class FPAssetTracerCleanup
    {
        internal sealed class Review
        {
            internal string Path;
            internal string Text;
            internal string Guid;
            internal readonly List<Match> Orphans = new List<Match>();
            internal readonly HashSet<string> MissingPrefabs = new HashSet<string>();
            internal int OtherReferences;
            internal string Note;
            internal bool OnlyOrphans => Orphans.Count > 0 && OtherReferences == 0 && Note == null;
        }

        // Accept only Unity's standard four-line object-reference override. Anything else stays unresolved.
        private static readonly Regex Override = new Regex(
            @"^    - target: \{fileID: [-\d]+, guid: (?<source>[a-f0-9]{32}), type: 3\}\r?\n" +
            @"      propertyPath: (?<property>[^\r\n]+)\r?\n" +
            @"      value: *\r?\n" +
            @"      objectReference: \{fileID: [-\d]+, guid: (?<asset>[a-f0-9]{32}), type: \d+\}\r?\n",
            RegexOptions.Multiline);

        internal static Review Inspect(string path, string guid)
        {
            var review = new Review { Path = path, Guid = guid };
            try
            {
                if (string.IsNullOrEmpty(guid) || !(path.EndsWith(".unity") || path.EndsWith(".prefab")))
                { review.Note = "Potential real use / importer dependency. Not a scene or prefab override."; return review; }
                review.Text = File.ReadAllText(FullPath(path));
                if (!review.Text.StartsWith("%YAML", StringComparison.Ordinal))
                { review.Note = "Not a text-serialized asset; unresolved."; return review; }
                int total = Regex.Matches(review.Text, @"guid: " + Regex.Escape(guid) + @"\b").Count;
                var documents = Regex.Matches(review.Text, @"^--- !u!(?<type>\d+) &[^\r\n]+\r?\n(?:(?!^--- !u!).)*",
                    RegexOptions.Multiline | RegexOptions.Singleline);
                foreach (Match document in documents)
                {
                    if (document.Groups["type"].Value != "1001") continue;
                    var source = Regex.Match(document.Value, @"^  m_SourcePrefab: \{fileID: [-\d]+, guid: (?<guid>[a-f0-9]{32}), type: 3\}", RegexOptions.Multiline);
                    if (!source.Success) continue;
                    string sourceGuid = source.Groups["guid"].Value;
                    if (!Missing(sourceGuid)) continue;
                    foreach (Match modification in Override.Matches(review.Text, document.Index))
                    {
                        if (modification.Index >= document.Index + document.Length) break;
                        if (modification.Groups["source"].Value != sourceGuid || modification.Groups["asset"].Value != guid) continue;
                        review.Orphans.Add(modification);
                        review.MissingPrefabs.Add(sourceGuid);
                    }
                }
                review.OtherReferences = total - review.Orphans.Count;
                if (total == 0) review.Note = "No text GUID match; dependency is unresolved/importer-managed.";
            }
            catch (Exception ex) { review.Note = "Review failed: " + ex.Message; }
            return review;
        }

        internal static bool Missing(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // The deleted path can remain in AssetDatabase until a refresh. Check both disk and Unity.
            return string.IsNullOrEmpty(path) || (!File.Exists(FullPath(path)) && AssetDatabase.LoadMainAssetAtPath(path) == null);
        }

        internal static string FullPath(string path) => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", path));

        internal static bool IsLoaded(string path)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == path) return true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == path) return true;
            return false;
        }

        internal static string Describe(Review review)
        {
            var lines = new List<string> { review.Path,
                $"Missing-prefab overrides: {review.Orphans.Count}; other GUID references: {review.OtherReferences}.",
                review.Note ?? (review.OnlyOrphans ? "All direct text references are overrides targeting missing prefab sources." : "Other references need review; real usage is not ruled out.") };
            var identities = new FPAssetTracerIdentity(review.Text ?? "");
            foreach (string guid in review.MissingPrefabs) lines.Add("Missing source prefab: " + FPAssetTracerIdentity.Asset(guid));
            foreach (Match match in review.Orphans)
            {
                lines.Add(identities.At(match.Index));
                lines.Add(FPAssetTracerIdentity.SourceTarget(match.Value));
                lines.Add("  " + match.Groups["property"].Value + " → " + FPAssetTracerIdentity.Asset(review.Guid));
            }
            return string.Join("\n", lines);
        }

        internal static string RemoveOrphans(Review approved)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for Edit mode and asset import to finish.");
            if (!approved.Path.StartsWith("Assets/", StringComparison.Ordinal) || IsLoaded(approved.Path))
                throw new InvalidOperationException("Close this scene/prefab first. Package assets cannot be edited here.");
            var fresh = Inspect(approved.Path, approved.Guid);
            if (fresh.Text != approved.Text || fresh.Note != null || fresh.Orphans.Count != approved.Orphans.Count || fresh.Orphans.Count == 0)
                throw new InvalidOperationException("Asset or prefab sources changed. Review again before cleanup.");
            if (!AssetDatabase.IsOpenForEdit(approved.Path, out string reason, StatusQueryOptions.ForceUpdate))
                throw new InvalidOperationException("Check out the asset in Unity Version Control first. " + reason);
            string fullPath = FullPath(approved.Path);
            string backupRoot = FullPath("Library/FP_Utility/AssetTracerCleanup/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupRoot);
            string backup = System.IO.Path.Combine(backupRoot, System.IO.Path.GetFileName(approved.Path));
            File.Copy(fullPath, backup);
            File.WriteAllText(System.IO.Path.Combine(backupRoot, "restore.txt"), "Original: " + fullPath + "\nClose the scene/prefab before restoring this backup.\n" + Describe(fresh));
            string text = fresh.Text;
            foreach (Match match in fresh.Orphans.OrderByDescending(m => m.Index)) text = text.Remove(match.Index, match.Length);
            // Keep all other YAML bytes/line endings intact (Unity text assets are UTF-8).
            bool bom = File.ReadAllBytes(fullPath).Take(3).SequenceEqual(new byte[] { 239, 187, 191 });
            File.WriteAllText(fullPath, text, new System.Text.UTF8Encoding(bom));
            AssetDatabase.ImportAsset(approved.Path, ImportAssetOptions.ForceUpdate);
            var after = Inspect(approved.Path, approved.Guid);
            if (after.Orphans.Count != 0) throw new InvalidOperationException("Cleanup verification failed. Backup: " + backup);
            return $"Removed {fresh.Orphans.Count} selected-asset override(s). Backup: {backup}\nRescan to verify downstream dependencies. Lighting data is never edited.";
        }

        internal static string RemoveUnusedInLoadedScene(string path, UnityEngine.Object target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Cleanup is unavailable for this scene or in Play mode.");
            var scene = SceneManager.GetSceneByPath(path);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the scene first.");
            var roots = new HashSet<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var instance = PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject);
                    if (instance == null) continue;
                    var mods = PrefabUtility.GetPropertyModifications(instance);
                    if (mods != null && mods.Any(m => m.objectReference == target)) roots.Add(instance);
                }
            if (roots.Count == 0) return "No loaded prefab roots with overrides referencing this object. Missing prefab records may require closed-asset cleanup.";
            PrefabUtility.RemoveUnusedOverrides(roots.ToArray(), InteractionMode.UserAction);
            return $"Unity Remove Unused Overrides ran on {roots.Count} matching prefab root(s). It removes all unused overrides on those roots, not valid overrides. Review the scene, save explicitly, then rescan. Undo is available.";
        }
    }
}
