// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    internal static class FPHierarchyIconUtility
    {
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
                if (target != null &&
                    !EditorUtility.IsPersistent(target) &&
                    target.scene.IsValid() &&
                    target.scene.isLoaded &&
                    !FP_HHeader.IsHeaderObject(target) &&
                    EditorGUIUtility.GetIconForObject(target) != icon)
                {
                    changedTargets.Add(target);
                }
            }

            if (changedTargets.Count == 0)
            {
                return 0;
            }

            var undoTargets = new Object[changedTargets.Count];
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
    }
}
