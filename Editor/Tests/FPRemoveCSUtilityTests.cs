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
    using TMPro;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public sealed class FPRemoveCSTestProgrammingComponent : MonoBehaviour
    {
    }

    public class FPRemoveCSUtilityTests
    {
        [Test]
        public void BuildPlan_DefaultOptions_RemoveScriptsAndKeepAudioUiAndVisualComponents()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            child.SetActive(false);

            FPRemoveCSTestProgrammingComponent rootScript = root.AddComponent<FPRemoveCSTestProgrammingComponent>();
            FPRemoveCSTestProgrammingComponent childScript = child.AddComponent<FPRemoveCSTestProgrammingComponent>();
            AudioSource audioSource = root.AddComponent<AudioSource>();
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            TextMesh textMesh = root.AddComponent<TextMesh>();
            Animator animator = root.AddComponent<Animator>();

            FPRemoveCSPlan plan = FPRemoveCSUtility.BuildPlan(
                new List<GameObject> { root },
                DefaultOptions());

            Assert.That(plan.EligibleRootCount, Is.EqualTo(1));
            Assert.That(plan.ScannedGameObjectCount, Is.EqualTo(2));
            Assert.That(plan.ProgrammingScriptCount, Is.EqualTo(2));
            Assert.That(plan.AudioSourceCount, Is.EqualTo(1));
            Assert.That(plan.UiTextComponentCount, Is.EqualTo(1));
            Assert.That(plan.TotalRemovalCount, Is.EqualTo(2));
            Assert.That(plan.ComponentsToRemove, Is.EquivalentTo(new Component[] { rootScript, childScript }));
            Assert.That(plan.ComponentsToRemove.Contains(audioSource), Is.False);
            Assert.That(plan.ComponentsToRemove.Contains(textMesh), Is.False);
            Assert.That(plan.ComponentsToRemove.Contains(animator), Is.False);
            Assert.That(plan.ComponentsToRemove.Contains(meshRenderer), Is.False);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void Execute_RemoveOptionsAlsoRemoveAudioAndTextButKeepAnimatorAndTransform()
        {
            var root = new GameObject("Root");
            root.AddComponent<FPRemoveCSTestProgrammingComponent>();
            root.AddComponent<AudioSource>();
            root.AddComponent<TextMesh>();
            root.AddComponent<Animator>();

            var options = DefaultOptions();
            options.KeepAudioSources = false;
            options.KeepUiTextComponents = false;
            FPRemoveCSPlan plan = FPRemoveCSUtility.BuildPlan(new List<GameObject> { root }, options);

            FPRemoveCSResult result = FPRemoveCSUtility.Execute(plan);

            Assert.That(result.RemovedComponentCount, Is.EqualTo(3));
            Assert.That(root.GetComponent<FPRemoveCSTestProgrammingComponent>(), Is.Null);
            Assert.That(root.GetComponent<AudioSource>(), Is.Null);
            Assert.That(root.GetComponent<TextMesh>(), Is.Null);
            Assert.That(root.GetComponent<Animator>(), Is.Not.Null);
            Assert.That(root.transform, Is.Not.Null);

            Undo.PerformUndo();
            Assert.That(root.GetComponent<FPRemoveCSTestProgrammingComponent>(), Is.Not.Null);
            Assert.That(root.GetComponent<AudioSource>(), Is.Not.Null);
            Assert.That(root.GetComponent<TextMesh>(), Is.Not.Null);

            Component[] restoredComponents = root.GetComponents<Component>();
            for (int i = 0; i < restoredComponents.Length; i++)
            {
                Undo.ClearUndo(restoredComponents[i]);
            }
            Undo.ClearUndo(root);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void BuildPlan_TextMeshProComponentsFollowUiTextOption()
        {
            var root = new GameObject(
                "UI Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
            CanvasRenderer canvasRenderer = root.GetComponent<CanvasRenderer>();

            FPRemoveCSPlan keepPlan = FPRemoveCSUtility.BuildPlan(
                new List<GameObject> { root },
                DefaultOptions());

            var removeOptions = DefaultOptions();
            removeOptions.KeepUiTextComponents = false;
            FPRemoveCSPlan removePlan = FPRemoveCSUtility.BuildPlan(
                new List<GameObject> { root },
                removeOptions);

            Assert.That(keepPlan.UiTextComponentCount, Is.EqualTo(2));
            Assert.That(keepPlan.TotalRemovalCount, Is.Zero);
            Assert.That(removePlan.TotalRemovalCount, Is.EqualTo(2));
            Assert.That(removePlan.ComponentsToRemove.Contains(text), Is.True);
            Assert.That(removePlan.ComponentsToRemove.Contains(canvasRenderer), Is.True);
            Assert.That(removePlan.ComponentsToRemove.Contains(root.transform), Is.False);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void BuildPlan_OverlappingRootsDoNotDuplicateComponents()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            root.AddComponent<FPRemoveCSTestProgrammingComponent>();
            child.AddComponent<FPRemoveCSTestProgrammingComponent>();

            FPRemoveCSPlan plan = FPRemoveCSUtility.BuildPlan(
                new List<GameObject> { root, child, root },
                DefaultOptions());

            Assert.That(plan.EligibleRootCount, Is.EqualTo(2));
            Assert.That(plan.ScannedGameObjectCount, Is.EqualTo(2));
            Assert.That(plan.ProgrammingScriptCount, Is.EqualTo(2));
            Assert.That(plan.TotalRemovalCount, Is.EqualTo(2));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void BuildPlan_ExcludeInactiveChildren_SkipsInactiveHierarchy()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            child.AddComponent<FPRemoveCSTestProgrammingComponent>();

            var options = DefaultOptions();
            options.IncludeInactive = false;
            FPRemoveCSPlan plan = FPRemoveCSUtility.BuildPlan(new List<GameObject> { root }, options);

            Assert.That(plan.ScannedGameObjectCount, Is.EqualTo(1));
            Assert.That(plan.ProgrammingScriptCount, Is.Zero);
            Assert.That(plan.TotalRemovalCount, Is.Zero);

            Object.DestroyImmediate(root);
        }

        [Test]
        public void CollectTopLevelRootsInScene_FiltersOtherScenesDuplicatesAndNestedTargets()
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene sourceScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene otherScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                var root = new GameObject("Root");
                var child = new GameObject("Child");
                var otherRoot = new GameObject("Other Root");
                SceneManager.MoveGameObjectToScene(root, sourceScene);
                SceneManager.MoveGameObjectToScene(child, sourceScene);
                SceneManager.MoveGameObjectToScene(otherRoot, otherScene);
                child.transform.SetParent(root.transform);

                List<GameObject> result = FPRemoveCSUtility.CollectTopLevelRootsInScene(
                    new List<GameObject> { child, otherRoot, root, root },
                    sourceScene);

                Assert.That(result, Is.EqualTo(new[] { root }));
            }
            finally
            {
                RestoreActiveScene(previousActiveScene);
                EditorSceneManager.CloseScene(otherScene, true);
                EditorSceneManager.CloseScene(sourceScene, true);
            }
        }

        [Test]
        public void CopyAndCleanToScene_PreservesSourceAndUnpacksDestinationPrefabCopy()
        {
            string prefabPath = $"Assets/FPRemoveCSUtilityTests_{System.Guid.NewGuid():N}.prefab";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene sourceScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene destinationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                var prefabSource = new GameObject("Visual Prefab");
                prefabSource.AddComponent<FPRemoveCSTestProgrammingComponent>();
                prefabSource.AddComponent<MeshRenderer>();
                prefabSource.AddComponent<AudioSource>();
                var inactiveChild = new GameObject("Inactive Child");
                inactiveChild.transform.SetParent(prefabSource.transform);
                inactiveChild.SetActive(false);
                inactiveChild.AddComponent<FPRemoveCSTestProgrammingComponent>();
                GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabSource, prefabPath);
                Object.DestroyImmediate(prefabSource);

                var sourceInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, sourceScene);
                sourceInstance.transform.SetPositionAndRotation(new Vector3(4f, 2f, -3f), Quaternion.Euler(10f, 25f, 5f));
                SceneManager.SetActiveScene(sourceScene);

                FPRemoveCSSceneCopyResult result = FPRemoveCSUtility.CopyAndCleanToScene(
                    new List<GameObject> { sourceInstance },
                    sourceScene,
                    destinationScene,
                    DefaultOptions());

                Assert.That(result.CopiedRoots.Count, Is.EqualTo(1));
                GameObject copy = result.CopiedRoots[0];
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(sourceScene));
                Assert.That(copy.scene, Is.EqualTo(destinationScene));
                Assert.That(copy.transform.position, Is.EqualTo(sourceInstance.transform.position));
                Assert.That(copy.transform.rotation, Is.EqualTo(sourceInstance.transform.rotation));
                Assert.That(copy.GetComponent<FPRemoveCSTestProgrammingComponent>(), Is.Null);
                Assert.That(copy.GetComponent<MeshRenderer>(), Is.Not.Null);
                Assert.That(copy.GetComponent<AudioSource>(), Is.Not.Null);
                Assert.That(copy.transform.GetChild(0).GetComponent<FPRemoveCSTestProgrammingComponent>(), Is.Null);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(copy), Is.False);

                Assert.That(sourceInstance.GetComponent<FPRemoveCSTestProgrammingComponent>(), Is.Not.Null);
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(sourceInstance), Is.True);
                Assert.That(result.CleanupResult.RemovedComponentCount, Is.EqualTo(2));
            }
            finally
            {
                RestoreActiveScene(previousActiveScene);
                EditorSceneManager.CloseScene(destinationScene, true);
                EditorSceneManager.CloseScene(sourceScene, true);
                AssetDatabase.DeleteAsset(prefabPath);
            }
        }

        [Test]
        public void SanitizeNewSceneName_RemovesExtensionAndInvalidPathCharacters()
        {
            string sanitizedName = FPRemoveCSWindow.SanitizeNewSceneName(
                " Visual/Scene.unity ",
                default);

            Assert.That(sanitizedName, Is.EqualTo("Visual_Scene"));
        }

        private static FPRemoveCSOptions DefaultOptions()
        {
            return new FPRemoveCSOptions
            {
                IncludeChildren = true,
                IncludeInactive = true,
                KeepAudioSources = true,
                KeepUiTextComponents = true
            };
        }

        private static void RestoreActiveScene(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
            }
        }
    }
}
