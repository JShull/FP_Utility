namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;

    public static class FP_Hierarchy
    {
        #region Move to Top Hierarchy
        [MenuItem("GameObject/FuzzPhyte/Hierarchy Top",false,0)]
        private static void MoveToTop()
        {
            // Get the currently selected GameObject
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject != null)
            {
                // Get the parent transform of the selected GameObject
                Transform parentTransform = selectedObject.transform.parent;

                // If the selected GameObject has a parent, move it to the top of its siblings
                if (parentTransform != null)
                {
                    Undo.SetTransformParent(selectedObject.transform, parentTransform, "Move to Top of Hierarchy");
                    selectedObject.transform.SetSiblingIndex(0);
                }
                else
                {
                    // If the selected GameObject has no parent, move it to the top of the root hierarchy
                    selectedObject.transform.SetAsFirstSibling();
                }

                // Mark the scene as dirty so that the change is saved
                EditorUtility.SetDirty(selectedObject);
                Selection.activeGameObject = selectedObject;
                EditorGUIUtility.PingObject(selectedObject); // Ensure it's visible in the Hierarchy
            }
        }
        // Validate the menu item to make sure it is only shown when a GameObject is selected
        [MenuItem("GameObject/FuzzPhyte/Hierarchy Top", true)]
        private static bool ValidateMoveToTop()
        {
            return Selection.activeGameObject != null;
        }
        #endregion
        #region Collapse Nested
        [MenuItem("GameObject/FuzzPhyte/Collapse All Items", false, 1)]
        private static void CollapseAllHierarchyItems()
        {
            // Save the currently selected GameObject
            GameObject originallySelectedObject = Selection.activeGameObject;

            // Get the current active scene
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // Get all root GameObjects in the scene
            GameObject[] rootObjects = scene.GetRootGameObjects();

            // Collapse all root objects and their children
            foreach (GameObject rootObject in rootObjects)
            {
                CollapseHierarchyRecursive(rootObject);
            }

            // Repaint the Hierarchy window to show changes
            EditorApplication.RepaintHierarchyWindow();

            // Reselect the originally selected GameObject
            if (originallySelectedObject != null)
            {
                Selection.activeGameObject = originallySelectedObject;
                EditorGUIUtility.PingObject(originallySelectedObject); // Ensure it's visible in the Hierarchy
            }
        }

        // Recursive method to collapse all child objects
        private static void CollapseHierarchyRecursive(GameObject obj)
        {
            // Use the Unity Editor method to collapse the hierarchy
            SetExpanded(obj, false);

            // Recursively collapse children
            foreach (Transform child in obj.transform)
            {
                CollapseHierarchyRecursive(child.gameObject);
            }
        }

        // This method sets the expansion state of a GameObject in the Hierarchy
        private static void SetExpanded(GameObject go, bool expand)
        {
            // Use the instance ID of the GameObject
            int instanceID = go.GetInstanceID();

            // Access the internal SceneHierarchyWindow and set the expanded state
            var hierarchyWindow = GetHierarchyWindow();

            if (hierarchyWindow != null)
            {
                var sceneHierarchyType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                var setExpandedMethod = sceneHierarchyType.GetMethod("SetExpanded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                setExpandedMethod.Invoke(hierarchyWindow, new object[] { instanceID, expand });
            }
        }

        private static EditorWindow GetHierarchyWindow()
        {
            // Get the Hierarchy window, if not focused, return the first found window of this type
            var sceneHierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            return EditorWindow.GetWindow(sceneHierarchyWindowType);
        }
        #endregion
    }
}

