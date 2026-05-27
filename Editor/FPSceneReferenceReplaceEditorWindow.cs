namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.SceneManagement;
    using UnityEngine.Timeline;
    public class FPSceneReferenceReplaceEditorWindow:EditorWindow
    {
        private GameObject oldGameObject;
        private GameObject newGameObject;

        private MonoScript componentScript;

        private bool replaceGameObjectReferences = true;
        private bool replaceSpecificComponentReferences = true;
        private bool includeInactiveObjects = true;
        private bool includePlayableDirectorBindings = true;
        private bool logPlayableDirectorBindings = true;

        private Vector2 scroll;

        [MenuItem("FuzzPhyte/Utility/Scene/Scene Reference Replacer", priority = FP_UtilityData.MENU_UTILITY_SCENE + 12)]
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
            if (includePlayableDirectorBindings)
            {
                logPlayableDirectorBindings = EditorGUILayout.ToggleLeft(
                "Log PlayableDirector / Timeline bindings before replacement",
                logPlayableDirectorBindings
                );
            }
            else
            {
                logPlayableDirectorBindings = false;
            }
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
                    DebugPlayableDirectorBindings(director);
                    var result = ReplacePlayableDirectorBindings(director, oldGameObject, newGameObject,replacementMap);

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
            GameObject oldGameObject,
            GameObject newGameObject,
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
                var sourceObject = output.sourceObject;

                if (sourceObject == null)
                {
                    continue;
                }

                var currentBinding = director.GetGenericBinding(sourceObject);

                if (currentBinding == null)
                {
                    continue;
                }

                UnityEngine.Object replacement = null;

                // Normal binding replacement:
                // old GameObject -> new GameObject
                // old component -> new component
                if (replacementMap.TryGetValue(currentBinding, out var mappedReplacement))
                {
                    replacement = mappedReplacement;
                }

                // Signal Track-specific fallback.
                // Signal tracks may appear as GameObject bindings or SignalReceiver bindings.
                if (replacement == null && sourceObject is SignalTrack)
                {
                    replacement = GetSignalTrackReplacement(
                        currentBinding,
                        oldGameObject,
                        newGameObject
                    );
                }

                if (replacement == null || replacement == currentBinding)
                {
                    continue;
                }

                Undo.RecordObject(director, "Replace Timeline Binding");

                director.SetGenericBinding(sourceObject, replacement);

                changed = true;
                referenceCount++;

                Debug.Log(
                    $"Replaced Timeline binding on '{director.name}' / '{sourceObject.name}': " +
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
        private static UnityEngine.Object GetSignalTrackReplacement(
            UnityEngine.Object currentBinding,
            GameObject oldGameObject,
            GameObject newGameObject
)
        {
            if (currentBinding == null || oldGameObject == null || newGameObject == null)
            {
                return null;
            }

            // Case 1:
            // The Signal Track is bound directly to the old GameObject.
            if (currentBinding == oldGameObject)
            {
                return newGameObject;
            }

            // Case 2:
            // The Signal Track is bound to a SignalReceiver component on the old GameObject.
            if (currentBinding is SignalReceiver oldSignalReceiver)
            {
                if (oldSignalReceiver.gameObject != oldGameObject)
                {
                    return null;
                }

                var newSignalReceiver = newGameObject.GetComponent<SignalReceiver>();

                if (newSignalReceiver != null)
                {
                    return newSignalReceiver;
                }

                // Optional: auto-add SignalReceiver if the old object had one.
                newSignalReceiver = Undo.AddComponent<SignalReceiver>(newGameObject);
                EditorUtility.SetDirty(newGameObject);

                return newSignalReceiver;
            }

            // Case 3:
            // Some Timeline bindings are stored as components.
            // If the bound component belongs to the old object, try to find
            // a matching component type on the new object.
            if (currentBinding is Component oldComponent)
            {
                if (oldComponent.gameObject != oldGameObject)
                {
                    return null;
                }

                var newComponent = newGameObject.GetComponent(oldComponent.GetType());

                if (newComponent != null)
                {
                    return newComponent;
                }
            }

            return null;
        }
        private static void DebugPlayableDirectorBindings(PlayableDirector director)
        {
            if (director == null || director.playableAsset == null)
            {
                return;
            }

            foreach (var output in director.playableAsset.outputs)
            {
                var sourceObject = output.sourceObject;
                var binding = director.GetGenericBinding(sourceObject);

                Debug.Log(
                    $"Director: {director.name} | " +
                    $"Track: {sourceObject?.name ?? "NULL"} | " +
                    $"Track Type: {sourceObject?.GetType().Name ?? "NULL"} | " +
                    $"Binding: {(binding == null ? "NULL" : binding.name)} | " +
                    $"Binding Type: {(binding == null ? "NULL" : binding.GetType().Name)}",
                    director
                );
            }
        }
    }
}
