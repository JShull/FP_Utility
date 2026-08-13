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
    using UnityEngine.SceneManagement;

    internal struct FPRemoveCSOptions
    {
        public bool IncludeChildren;
        public bool IncludeInactive;
        public bool KeepAudioSources;
        public bool KeepUiTextComponents;
    }

    internal sealed class FPRemoveCSPlan
    {
        internal readonly List<Component> ComponentsToRemove = new List<Component>();
        internal readonly List<GameObject> MissingScriptOwners = new List<GameObject>();

        internal int EligibleRootCount { get; set; }
        internal int SkippedPersistentRootCount { get; set; }
        internal int ScannedGameObjectCount { get; set; }
        internal int ProgrammingScriptCount { get; set; }
        internal int AudioSourceCount { get; set; }
        internal int UiTextComponentCount { get; set; }
        internal int MissingScriptCount { get; set; }

        internal int TotalRemovalCount => ComponentsToRemove.Count + MissingScriptCount;
    }

    internal struct FPRemoveCSResult
    {
        internal int RemovedComponentCount;
        internal int RemovedMissingScriptCount;

        internal int TotalRemovedCount => RemovedComponentCount + RemovedMissingScriptCount;
    }

    internal sealed class FPRemoveCSSceneCopyResult
    {
        internal readonly List<GameObject> CopiedRoots = new List<GameObject>();
        internal FPRemoveCSResult CleanupResult;
    }

    internal static class FPRemoveCSUtility
    {
        private const string UndoName = "Remove CS Components";

        internal static FPRemoveCSPlan BuildPlan(IList<GameObject> roots, FPRemoveCSOptions options)
        {
            var plan = new FPRemoveCSPlan();
            if (roots == null)
            {
                return plan;
            }

            var visitedRoots = new HashSet<EntityId>();
            var visitedObjects = new HashSet<EntityId>();
            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null || !visitedRoots.Add(root.GetEntityId()))
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(root))
                {
                    plan.SkippedPersistentRootCount++;
                    continue;
                }

                plan.EligibleRootCount++;
                if (!options.IncludeChildren)
                {
                    CollectFromGameObject(root, options, visitedObjects, plan);
                    continue;
                }

                Transform[] hierarchy = root.GetComponentsInChildren<Transform>(options.IncludeInactive);
                for (int objectIndex = 0; objectIndex < hierarchy.Length; objectIndex++)
                {
                    Transform transform = hierarchy[objectIndex];
                    if (transform != null)
                    {
                        CollectFromGameObject(transform.gameObject, options, visitedObjects, plan);
                    }
                }
            }

            return plan;
        }

        internal static FPRemoveCSResult Execute(FPRemoveCSPlan plan)
        {
            return Execute(plan, true);
        }

        internal static FPRemoveCSResult ExecuteImmediate(FPRemoveCSPlan plan)
        {
            return Execute(plan, false);
        }

        internal static List<GameObject> CollectTopLevelRootsInScene(IList<GameObject> roots, Scene scene)
        {
            var eligibleRoots = new List<GameObject>();
            if (roots == null || !scene.IsValid() || !scene.isLoaded)
            {
                return eligibleRoots;
            }

            var eligibleIds = new HashSet<EntityId>();
            for (int i = 0; i < roots.Count; i++)
            {
                GameObject root = roots[i];
                if (root == null || root.scene != scene || EditorUtility.IsPersistent(root))
                {
                    continue;
                }

                EntityId entityId = root.GetEntityId();
                if (eligibleIds.Add(entityId))
                {
                    eligibleRoots.Add(root);
                }
            }

            for (int i = eligibleRoots.Count - 1; i >= 0; i--)
            {
                Transform candidate = eligibleRoots[i].transform;
                for (int otherIndex = 0; otherIndex < eligibleRoots.Count; otherIndex++)
                {
                    if (i == otherIndex || !candidate.IsChildOf(eligibleRoots[otherIndex].transform))
                    {
                        continue;
                    }

                    eligibleRoots.RemoveAt(i);
                    break;
                }
            }

            return eligibleRoots;
        }

        internal static FPRemoveCSSceneCopyResult CopyAndCleanToScene(
            IList<GameObject> roots,
            Scene sourceScene,
            Scene destinationScene,
            FPRemoveCSOptions options)
        {
            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                throw new ArgumentException("The source scene must be valid and loaded.", nameof(sourceScene));
            }

            if (!destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                throw new ArgumentException("The destination scene must be valid and loaded.", nameof(destinationScene));
            }

            if (sourceScene == destinationScene)
            {
                throw new ArgumentException("The destination scene must be different from the source scene.", nameof(destinationScene));
            }

            List<GameObject> sourceRoots = CollectTopLevelRootsInScene(roots, sourceScene);
            var result = new FPRemoveCSSceneCopyResult();
            Scene previousActiveScene = SceneManager.GetActiveScene();
            options.IncludeChildren = true;
            options.IncludeInactive = true;

            try
            {
                if (!SceneManager.SetActiveScene(destinationScene))
                {
                    throw new InvalidOperationException("Unity could not make the destination scene active for copying.");
                }

                for (int i = 0; i < sourceRoots.Count; i++)
                {
                    GameObject source = sourceRoots[i];
                    GameObject copy = UnityEngine.Object.Instantiate(source);
                    copy.name = source.name;
                    copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    copy.transform.localScale = source.transform.lossyScale;

                    if (copy.scene != destinationScene)
                    {
                        SceneManager.MoveGameObjectToScene(copy, destinationScene);
                    }

                    UnpackPrefabHierarchy(copy);
                    result.CopiedRoots.Add(copy);
                }

                FPRemoveCSPlan cleanupPlan = BuildPlan(result.CopiedRoots, options);
                result.CleanupResult = ExecuteImmediate(cleanupPlan);
                if (result.CopiedRoots.Count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(destinationScene);
                }

                return result;
            }
            catch
            {
                DestroyCopiedRoots(result.CopiedRoots);
                throw;
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        internal static void DestroyCopiedRoots(IList<GameObject> copiedRoots)
        {
            if (copiedRoots == null)
            {
                return;
            }

            for (int i = copiedRoots.Count - 1; i >= 0; i--)
            {
                if (copiedRoots[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(copiedRoots[i]);
                }
            }
        }

        private static FPRemoveCSResult Execute(FPRemoveCSPlan plan, bool registerUndo)
        {
            var result = new FPRemoveCSResult();
            if (plan == null || plan.TotalRemovalCount == 0)
            {
                return result;
            }

            int undoGroup = -1;
            if (registerUndo)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
            }

            var touchedObjects = new HashSet<GameObject>();
            for (int i = 0; i < plan.ComponentsToRemove.Count; i++)
            {
                Component component = plan.ComponentsToRemove[i];
                if (component == null)
                {
                    continue;
                }

                GameObject owner = component.gameObject;
                if (registerUndo)
                {
                    Undo.DestroyObjectImmediate(component);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
                result.RemovedComponentCount++;
                if (owner != null)
                {
                    touchedObjects.Add(owner);
                }
            }

            for (int i = 0; i < plan.MissingScriptOwners.Count; i++)
            {
                GameObject owner = plan.MissingScriptOwners[i];
                if (owner == null)
                {
                    continue;
                }

                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(owner);
                if (missingCount == 0)
                {
                    continue;
                }

                if (registerUndo)
                {
                    Undo.RegisterCompleteObjectUndo(owner, UndoName);
                }
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(owner);
                result.RemovedMissingScriptCount += missingCount;
                touchedObjects.Add(owner);
            }

            foreach (GameObject touchedObject in touchedObjects)
            {
                if (touchedObject != null)
                {
                    EditorUtility.SetDirty(touchedObject);
                }
            }

            if (registerUndo)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
            return result;
        }

        private static void UnpackPrefabHierarchy(GameObject root)
        {
            while (root != null)
            {
                GameObject prefabRoot = null;
                Transform[] hierarchy = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < hierarchy.Length; i++)
                {
                    GameObject candidate = hierarchy[i].gameObject;
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(candidate))
                    {
                        prefabRoot = candidate;
                        break;
                    }
                }

                if (prefabRoot == null)
                {
                    return;
                }

                PrefabUtility.UnpackPrefabInstance(
                    prefabRoot,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }
        }

        internal static bool IsUiOrTextComponent(Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (component is Canvas ||
                component is CanvasRenderer ||
                component is CanvasGroup ||
                component is TextMesh)
            {
                return true;
            }

            Type type = component.GetType();
            string typeNamespace = type.Namespace;
            if (string.IsNullOrEmpty(typeNamespace))
            {
                return false;
            }

            return typeNamespace == "UnityEngine.UI" ||
                typeNamespace.StartsWith("UnityEngine.UI.", StringComparison.Ordinal) ||
                typeNamespace == "TMPro" ||
                typeNamespace.StartsWith("TMPro.", StringComparison.Ordinal);
        }

        private static void CollectFromGameObject(
            GameObject gameObject,
            FPRemoveCSOptions options,
            HashSet<EntityId> visitedObjects,
            FPRemoveCSPlan plan)
        {
            if (gameObject == null || !visitedObjects.Add(gameObject.GetEntityId()))
            {
                return;
            }

            plan.ScannedGameObjectCount++;
            int missingScriptCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
            if (missingScriptCount > 0)
            {
                plan.MissingScriptCount += missingScriptCount;
                plan.MissingScriptOwners.Add(gameObject);
            }

            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform)
                {
                    continue;
                }

                if (component is AudioSource)
                {
                    plan.AudioSourceCount++;
                    if (!options.KeepAudioSources)
                    {
                        plan.ComponentsToRemove.Add(component);
                    }

                    continue;
                }

                if (IsUiOrTextComponent(component))
                {
                    plan.UiTextComponentCount++;
                    if (!options.KeepUiTextComponents)
                    {
                        plan.ComponentsToRemove.Add(component);
                    }

                    continue;
                }

                if (component is MonoBehaviour)
                {
                    plan.ProgrammingScriptCount++;
                    plan.ComponentsToRemove.Add(component);
                }
            }
        }
    }
}
