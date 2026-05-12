namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.SceneManagement;
    public class FPSceneReferenceReplaceEditorWindow:EditorWindow
    {
        private GameObject oldGameObject;
        private GameObject newGameObject;

        private MonoScript componentScript;

        private bool replaceGameObjectReferences = true;
        private bool replaceSpecificComponentReferences = true;
        private bool includeInactiveObjects = true;
        private bool includePlayableDirectorBindings = true;

        private Vector2 scroll;

        [MenuItem("FuzzPhyte/Utility/Editor/Scene Reference Replacer")]
        public static void Open()
        {
            var window = GetWindow<FPSceneReferenceReplaceEditorWindow>();
            window.titleContent = new GUIContent("Scene Ref Replacer");
            window.Show();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Scene Reference Replacer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Replaces references in the active scene only. Select an old GameObject, a new GameObject, and optionally a specific component script such as TVModuleManager.cs.",
                MessageType.Info
            );

            oldGameObject = (GameObject)EditorGUILayout.ObjectField(
                "Old GameObject",
                oldGameObject,
                typeof(GameObject),
                true
            );

            newGameObject = (GameObject)EditorGUILayout.ObjectField(
                "New GameObject",
                newGameObject,
                typeof(GameObject),
                true
            );

            componentScript = (MonoScript)EditorGUILayout.ObjectField(
                "Specific Component Script",
                componentScript,
                typeof(MonoScript),
                false
            );

            EditorGUILayout.Space();

            replaceGameObjectReferences = EditorGUILayout.ToggleLeft(
                "Replace GameObject references",
                replaceGameObjectReferences
            );

            replaceSpecificComponentReferences = EditorGUILayout.ToggleLeft(
                "Replace references to matching component type",
                replaceSpecificComponentReferences
            );

            includeInactiveObjects = EditorGUILayout.ToggleLeft(
                "Include inactive objects",
                includeInactiveObjects
            );

            includePlayableDirectorBindings = EditorGUILayout.ToggleLeft(
                "Include PlayableDirector / Timeline bindings",
                includePlayableDirectorBindings
            );

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!CanRun()))
            {
                if (GUILayout.Button("Replace References In Active Scene", GUILayout.Height(32)))
                {
                    ReplaceReferencesInActiveScene();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private bool CanRun()
        {
            if (oldGameObject == null || newGameObject == null)
            {
                return false;
            }

            if (oldGameObject == newGameObject)
            {
                return false;
            }

            if (replaceSpecificComponentReferences && componentScript == null)
            {
                return false;
            }

            return true;
        }

        private void ReplaceReferencesInActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();

            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning("No valid active scene found.");
                return;
            }

            Type componentType = null;

            if (replaceSpecificComponentReferences)
            {
                componentType = componentScript.GetClass();

                if (componentType == null)
                {
                    Debug.LogWarning("Selected script does not resolve to a valid class.");
                    return;
                }

                if (!typeof(Component).IsAssignableFrom(componentType))
                {
                    Debug.LogWarning("Selected script is not a Component type.");
                    return;
                }
            }

            var oldComponent = componentType == null
                ? null
                : oldGameObject.GetComponent(componentType);

            var newComponent = componentType == null
                ? null
                : newGameObject.GetComponent(componentType);

            if (replaceSpecificComponentReferences)
            {
                if (oldComponent == null)
                {
                    Debug.LogWarning($"Old GameObject does not have component: {componentType.Name}");
                    return;
                }

                if (newComponent == null)
                {
                    Debug.LogWarning($"New GameObject does not have component: {componentType.Name}");
                    return;
                }
            }

            var replacementMap = new Dictionary<UnityEngine.Object, UnityEngine.Object>();

            if (replaceGameObjectReferences)
            {
                replacementMap[oldGameObject] = newGameObject;
            }

            if (replaceSpecificComponentReferences && oldComponent != null && newComponent != null)
            {
                replacementMap[oldComponent] = newComponent;
            }

            var allSceneObjects = GetAllObjectsInScene(activeScene, includeInactiveObjects);

            int changedObjectCount = 0;
            int changedReferenceCount = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Scene References");

            foreach (var sceneObject in allSceneObjects)
            {
                if (sceneObject == null)
                {
                    continue;
                }

                var result = ReplaceObjectReferences(sceneObject, replacementMap);

                if (result.changed)
                {
                    changedObjectCount++;
                    changedReferenceCount += result.referenceCount;
                }
            }

            if (includePlayableDirectorBindings)
            {
                foreach (var director in FindObjectsInScene<PlayableDirector>(activeScene, includeInactiveObjects))
                {
                    var result = ReplacePlayableDirectorBindings(director, replacementMap);

                    if (result.changed)
                    {
                        changedObjectCount++;
                        changedReferenceCount += result.referenceCount;
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (changedReferenceCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log(
                $"Reference replacement complete in scene '{activeScene.name}'. " +
                $"Changed {changedReferenceCount} reference(s) across {changedObjectCount} object(s)."
            );
        }

        private static List<UnityEngine.Object> GetAllObjectsInScene(Scene scene, bool includeInactive)
        {
            var results = new List<UnityEngine.Object>();

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                {
                    continue;
                }

                results.Add(root);

                var transforms = root.GetComponentsInChildren<Transform>(includeInactive);

                foreach (var transform in transforms)
                {
                    if (transform == null)
                    {
                        continue;
                    }

                    var gameObject = transform.gameObject;

                    if (gameObject == null)
                    {
                        continue;
                    }

                    if (!results.Contains(gameObject))
                    {
                        results.Add(gameObject);
                    }

                    var components = gameObject.GetComponents<Component>();

                    foreach (var component in components)
                    {
                        if (component == null)
                        {
                            continue;
                        }

                        results.Add(component);
                    }
                }
            }

            return results;
        }

        private static IEnumerable<T> FindObjectsInScene<T>(Scene scene, bool includeInactive)
            where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var components = root.GetComponentsInChildren<T>(includeInactive);

                foreach (var component in components)
                {
                    if (component != null)
                    {
                        yield return component;
                    }
                }
            }
        }

        private static (bool changed, int referenceCount) ReplaceObjectReferences(
            UnityEngine.Object targetObject,
            Dictionary<UnityEngine.Object, UnityEngine.Object> replacementMap
        )
        {
            if (targetObject == null)
            {
                return (false, 0);
            }

            SerializedObject serializedObject;

            try
            {
                serializedObject = new SerializedObject(targetObject);
            }
            catch
            {
                return (false, 0);
            }

            bool changed = false;
            int referenceCount = 0;

            var property = serializedObject.GetIterator();

            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                var currentReference = property.objectReferenceValue;

                if (currentReference == null)
                {
                    continue;
                }

                if (!replacementMap.TryGetValue(currentReference, out var replacement))
                {
                    continue;
                }

                Undo.RecordObject(targetObject, "Replace Object Reference");

                property.objectReferenceValue = replacement;
                changed = true;
                referenceCount++;

                Debug.Log(
                    $"Replaced reference on {targetObject.name} property '{property.propertyPath}': " +
                    $"{currentReference.name} -> {replacement.name}",
                    targetObject
                );
            }

            if (changed)
            {
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetObject);
            }

            return (changed, referenceCount);
        }

        private static (bool changed, int referenceCount) ReplacePlayableDirectorBindings(
            PlayableDirector director,
            Dictionary<UnityEngine.Object, UnityEngine.Object> replacementMap
        )
        {
            if (director == null || director.playableAsset == null)
            {
                return (false, 0);
            }

            bool changed = false;
            int referenceCount = 0;

            foreach (var output in director.playableAsset.outputs)
            {
                var currentBinding = director.GetGenericBinding(output.sourceObject);

                if (currentBinding == null)
                {
                    continue;
                }

                if (!replacementMap.TryGetValue(currentBinding, out var replacement))
                {
                    continue;
                }

                Undo.RecordObject(director, "Replace Timeline Binding");

                director.SetGenericBinding(output.sourceObject, replacement);

                changed = true;
                referenceCount++;

                Debug.Log(
                    $"Replaced Timeline binding on {director.name}: " +
                    $"{currentBinding.name} -> {replacement.name}",
                    director
                );
            }

            if (changed)
            {
                EditorUtility.SetDirty(director);
            }

            return (changed, referenceCount);
        }
    }
}
