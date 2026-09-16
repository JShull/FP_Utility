// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    internal static class FPHierarchyIconUtility
    {
        [Serializable]
        private sealed class IconPaletteData
        {
            public List<string> Guids = new();
        }

        internal static List<GameObject> CollectSelectionTargets(
            bool includeChildren,
            out int skippedHeaderCount,
            out int skippedNonSceneCount)
        {
            return CollectTargets(
                Selection.gameObjects,
                includeChildren,
                out skippedHeaderCount,
                out skippedNonSceneCount);
        }

        internal static List<GameObject> CollectTargets(
            IEnumerable<GameObject> roots,
            bool includeChildren,
            out int skippedHeaderCount,
            out int skippedNonSceneCount)
        {
            var targets = new List<GameObject>();
            var targetIds = new HashSet<EntityId>();
            skippedHeaderCount = 0;
            skippedNonSceneCount = 0;

            if (roots == null)
            {
                return targets;
            }

            foreach (GameObject root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                if (!includeChildren)
                {
                    TryAddTarget(root, targets, targetIds, ref skippedHeaderCount, ref skippedNonSceneCount);
                    continue;
                }

                Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < hierarchy.Length; i++)
                {
                    TryAddTarget(
                        hierarchy[i].gameObject,
                        targets,
                        targetIds,
                        ref skippedHeaderCount,
                        ref skippedNonSceneCount);
                }
            }

            return targets;
        }

        internal static int ApplyIcon(IReadOnlyList<GameObject> targets, Texture2D icon)
        {
            if (icon == null)
            {
                return 0;
            }

            return SetIcon(targets, icon, "Apply Hierarchy Icon");
        }

        internal static int ClearIcons(IReadOnlyList<GameObject> targets)
        {
            return SetIcon(targets, null, "Clear Hierarchy Icons");
        }

        internal static Texture2D GetIcon(GameObject target)
        {
            return target == null ? null : EditorGUIUtility.GetIconForObject(target);
        }

        internal static bool IsEligibleTarget(GameObject target)
        {
            return target != null &&
                   !EditorUtility.IsPersistent(target) &&
                   target.scene.IsValid() &&
                   target.scene.isLoaded &&
                   !FP_HHeader.IsHeaderObject(target);
        }

        internal static List<Texture2D> GetPaletteIcons()
        {
            IconPaletteData data = LoadPaletteData();
            var icons = new List<Texture2D>(data.Guids.Count);
            for (int i = 0; i < data.Guids.Count; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(data.Guids[i]);
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon != null)
                {
                    icons.Add(icon);
                }
            }

            return icons;
        }

        internal static bool PaletteContains(Texture2D icon)
        {
            string guid = GetAssetGuid(icon);
            return !string.IsNullOrWhiteSpace(guid) && LoadPaletteData().Guids.Contains(guid);
        }

        internal static bool AddPaletteIcon(Texture2D icon)
        {
            string guid = GetAssetGuid(icon);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            IconPaletteData data = LoadPaletteData();
            if (data.Guids.Contains(guid))
            {
                return false;
            }

            data.Guids.Add(guid);
            SavePaletteData(data);
            return true;
        }

        internal static int AddPaletteIcons(IReadOnlyList<UnityEngine.Object> objects)
        {
            if (objects == null)
            {
                return 0;
            }

            IconPaletteData data = LoadPaletteData();
            var uniqueGuids = new HashSet<string>(data.Guids);
            int addedCount = 0;
            for (int i = 0; i < objects.Count; i++)
            {
                string guid = GetAssetGuid(objects[i] as Texture2D);
                if (!string.IsNullOrWhiteSpace(guid) && uniqueGuids.Add(guid))
                {
                    data.Guids.Add(guid);
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                SavePaletteData(data);
            }

            return addedCount;
        }

        internal static bool RemovePaletteIcon(Texture2D icon)
        {
            string guid = GetAssetGuid(icon);
            if (string.IsNullOrWhiteSpace(guid))
            {
                return false;
            }

            IconPaletteData data = LoadPaletteData();
            if (!data.Guids.Remove(guid))
            {
                return false;
            }

            SavePaletteData(data);
            return true;
        }

        internal static bool SetPaletteIcons(IReadOnlyList<Texture2D> icons)
        {
            var orderedGuids = new List<string>();
            var uniqueGuids = new HashSet<string>();
            if (icons != null)
            {
                for (int i = 0; i < icons.Count; i++)
                {
                    string guid = GetAssetGuid(icons[i]);
                    if (!string.IsNullOrWhiteSpace(guid) && uniqueGuids.Add(guid))
                    {
                        orderedGuids.Add(guid);
                    }
                }
            }

            IconPaletteData currentData = LoadPaletteData();
            if (ListsMatch(currentData.Guids, orderedGuids))
            {
                return false;
            }

            currentData.Guids = orderedGuids;
            SavePaletteData(currentData);
            return true;
        }

        internal static string PaletteEditorPrefsKey => GetPaletteKey();

        private static void TryAddTarget(
            GameObject candidate,
            List<GameObject> targets,
            HashSet<EntityId> targetIds,
            ref int skippedHeaderCount,
            ref int skippedNonSceneCount)
        {
            if (candidate == null)
            {
                return;
            }

            if (EditorUtility.IsPersistent(candidate) || !candidate.scene.IsValid() || !candidate.scene.isLoaded)
            {
                skippedNonSceneCount++;
                return;
            }

            if (FP_HHeader.IsHeaderObject(candidate))
            {
                skippedHeaderCount++;
                return;
            }

            EntityId candidateId = candidate.GetEntityId();
            if (targetIds.Add(candidateId))
            {
                targets.Add(candidate);
            }
        }

        private static int SetIcon(IReadOnlyList<GameObject> targets, Texture2D icon, string undoName)
        {
            if (targets == null || targets.Count == 0)
            {
                return 0;
            }

            var changedTargets = new List<GameObject>();
            for (int i = 0; i < targets.Count; i++)
            {
                GameObject target = targets[i];
                if (IsEligibleTarget(target) &&
                    EditorGUIUtility.GetIconForObject(target) != icon)
                {
                    changedTargets.Add(target);
                }
            }

            if (changedTargets.Count == 0)
            {
                return 0;
            }

            var undoTargets = new UnityEngine.Object[changedTargets.Count];
            for (int i = 0; i < changedTargets.Count; i++)
            {
                undoTargets[i] = changedTargets[i];
            }

            Undo.RecordObjects(undoTargets, undoName);

            var dirtyScenes = new HashSet<UnityEngine.SceneManagement.Scene>();
            for (int i = 0; i < changedTargets.Count; i++)
            {
                GameObject target = changedTargets[i];
                EditorGUIUtility.SetIconForObject(target, icon);
                if (dirtyScenes.Add(target.scene))
                {
                    EditorSceneManager.MarkSceneDirty(target.scene);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            SceneView.RepaintAll();
            return changedTargets.Count;
        }

        private static IconPaletteData LoadPaletteData()
        {
            string json = EditorPrefs.GetString(GetPaletteKey(), string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new IconPaletteData();
            }

            IconPaletteData data = JsonUtility.FromJson<IconPaletteData>(json);
            if (data == null || data.Guids == null)
            {
                return new IconPaletteData();
            }

            var uniqueGuids = new HashSet<string>();
            var normalizedGuids = new List<string>(data.Guids.Count);
            for (int i = 0; i < data.Guids.Count; i++)
            {
                string guid = data.Guids[i];
                if (!string.IsNullOrWhiteSpace(guid) && uniqueGuids.Add(guid))
                {
                    normalizedGuids.Add(guid);
                }
            }

            data.Guids = normalizedGuids;
            return data;
        }

        private static void SavePaletteData(IconPaletteData data)
        {
            EditorPrefs.SetString(GetPaletteKey(), JsonUtility.ToJson(data));
        }

        private static bool ListsMatch(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetPaletteKey()
        {
            return $"{FP_UtilityData.FP_HIERARCHY_ICON_PALETTE_KEY}_{Hash128.Compute(Application.dataPath)}";
        }

        private static string GetAssetGuid(Texture2D icon)
        {
            if (icon == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(icon);
            return string.IsNullOrWhiteSpace(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }
    }
}
