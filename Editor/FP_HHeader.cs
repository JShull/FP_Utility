namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    using System.Collections.Generic;
    using UnityEngine.SceneManagement;
    using System.Linq;

    [InitializeOnLoad]
    public static class FP_HHeader
    {
        private static Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        private static Dictionary<string, string> previousNames = new Dictionary<string, string>();
        private static Color expandedColor = new Color(0.412f, 0.678f, 0, 1);
        private static Color collapsedColor = new Color(0.728f, 0.678f, 0, 1);
        private static Color headerColor = new Color(0.0f, 0.0f, 0.0f, 1);
        private static int adjHeaderWidth = 20;
        
        static FP_HHeader()
        {
            Debug.LogWarning($"Editor Setup Initialized FP_HHeader");
            LoadFoldoutStatesFromPrefs(); // Load saved foldout states on editor initialization
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
            EditorApplication.update += OnEditorUpdate; // Monitor changes in the editor
            Selection.selectionChanged += OnSelectionChanged; // Hook into the selection changed event
        }
        
        private static void OnEditorUpdate()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            bool dirtyState = false;
            //Debug.LogWarning($"On Editor Update!");
            //LoadFoldoutStatesFromPrefs();
            string lastScenePath = EditorPrefs.GetString(FP_UtilityData.LAST_SCENEPATH_VAR, "");
            if (activeScene.path != lastScenePath)
            {
                EditorPrefs.SetString(FP_UtilityData.LAST_SCENEPATH_VAR, activeScene.path);
                OnSceneOpened(activeScene);
                //this should load our dictionaries
            }
            var foldoutKeys = new List<string>(foldoutStates.Keys);

            for (int i = 0; i < foldoutKeys.Count; i++)
            {
                var key = foldoutKeys[i];
                GUID gUID = new(key);
                var instanceID = FP_Utility_Editor.GetInstanceIDFromGUID(gUID);
                if (instanceID != -1)
                {
                    GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                    if (obj != null)
                    {
                        //pull previousName
                        var prevName = previousNames[key];
                        if (obj.name != prevName)
                        {
                            // Name has changed, check if it still meets the criteria
                            if (obj.name != obj.name.ToUpper() || obj.activeInHierarchy)
                            {
                                // No longer meets criteria, unhide all children and remove tracking
                                ShowSubsequentObjects(obj);
                                foldoutStates.Remove(key);
                                previousNames.Remove(key);
                                dirtyState = true;
                                EditorApplication.RepaintHierarchyWindow();
                            }
                            else
                            {
                                // Update the stored name
                                previousNames[key] = obj.name;
                                dirtyState = true;
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Something didn't work right!");
                }
            }
          
            if (dirtyState)
            {
                SaveFoldoutStatesToPrefs();
            }
        }
        private static void OnSceneOpened(Scene scene)
        {
            // Clear and reset foldout states when a new scene is opened
            foldoutStates.Clear();
            previousNames.Clear();
            //these resets my data
            LoadFoldoutStatesFromPrefs();
            Debug.LogWarning($"Editor opened a new scene: {scene.name}! Refreshing FuzzPhyte Header!");
            // Force a repaint of the Hierarchy window to ensure OnHierarchyWindowItemOnGUI runs
            EditorApplication.RepaintHierarchyWindow();
        }
        private static void OnSelectionChanged()
        {
            // Get the currently selected object in the scene
            if (EditorWindow.focusedWindow != null && EditorWindow.focusedWindow.titleContent.text == "Scene")
            {
                GameObject selectedObj = Selection.activeGameObject;

                if (selectedObj != null)
                {
                    // Traverse up to find the top-most parent (the root object in its hierarchy)
                    Transform current = selectedObj.transform;
                    while (current.parent != null)
                    {
                        current = current.parent;  // Move up to the parent
                    }
                    GameObject rootObject = current.gameObject;  // This is the root object in the hierarchy

                    // Now that we have the root object, get the scene it belongs to
                    var scene = rootObject.scene;

                    // Get all root objects in the scene
                    GameObject[] sceneRootObjects = scene.GetRootGameObjects();

                    // Find the root object's index in the scene's root objects array
                    int rootIndex = System.Array.IndexOf(sceneRootObjects, rootObject);

                    // Traverse upwards through the scene root objects to check foldoutStates
                    for (int i = rootIndex - 1; i >= 0; i--)
                    {
                        GameObject sceneRootObject = sceneRootObjects[i];
                        bool confirmGUID = true;
                        var GUIDKey = FP_Utility_Editor.ReturnGUIDFromInstance(sceneRootObject.GetInstanceID(),out confirmGUID).ToString();
                        // Check if the scene root object is in foldoutStates and is currently collapsed
                        if (foldoutStates.ContainsKey(GUIDKey))
                        {
                            if (!foldoutStates[GUIDKey])
                            {
                                // Unfold the section to reveal this object
                                foldoutStates[GUIDKey] = true;
                                ShowSubsequentObjects(sceneRootObject);
                                EditorApplication.RepaintHierarchyWindow();
                                // Highlight the original selected item
                                EditorGUIUtility.PingObject(selectedObj);
                                SaveFoldoutStatesToPrefs();
                                break;  // Stop after expanding the first relevant root object
                            }
                        }
                    }
                }
            }
                
        }
        // Helper method to check if a transform is a child of another transform
        private static bool IsChildOf(Transform parent, Transform child)
        {
            Transform current = child;

            while (current != null)
            {
                if (current == parent)
                {
                    return true;
                }
                current = current.parent;
            }

            return false;
        }
       
        
        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            // Get the GameObject associated with this hierarchy item
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
  
            
            bool dirtyState=false;
            if (obj != null)
            {
                // get stored GUID
                bool confirmGUID = true;
                var ID = FP_Utility_Editor.ReturnGUIDFromInstance(instanceID, out confirmGUID).ToString();
                //get guid from gameobject?
                
                // compare guids

                
               
                // Track the name initially
                if (!previousNames.ContainsKey(ID))
                {
                    previousNames.Add(ID, obj.name);
                    dirtyState = true;
                }
                // Check if the GameObject name is in all caps and if it is not active (disabled)
                if (obj.name == obj.name.ToUpper() && !obj.activeInHierarchy && obj.transform.childCount==0)
                {
                    // Track foldout state using the GameObject's instance ID
                    Debug.LogWarning($"Foldout size: {foldoutStates.Count}");
                    for(int i=0;i < foldoutStates.Count; i++)
                    {
                        var aKey = foldoutStates.Keys.ToList();
                        Debug.LogWarning($"Foldout Debug: ID {aKey[i]} | Name: {obj.name} |Status: {foldoutStates[aKey[i]]}");
                    }
                    if (!foldoutStates.ContainsKey(ID))
                    {
                        foldoutStates.Add(ID, true);
                        //foldoutStates[instanceID] = true; // Default to expanded
                        Debug.LogWarning($"Foldout Debug: ID {ID} | Name: {obj.name} |Status: {foldoutStates[ID]}");
                        dirtyState = true;
                    }
                    // Adjust the label position to avoid overlapping the foldout arrow
                    Rect labelRect = new Rect(selectionRect.x + adjHeaderWidth, selectionRect.y, selectionRect.width - adjHeaderWidth, selectionRect.height);

                    // Set up custom style for bold text
                    GUIStyle style = new GUIStyle
                    {
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.white } // You can change the color here
                    };

                    // Optionally change the background color
                    Rect backgroundRect = selectionRect;
                    backgroundRect.xMin = 50;
                    EditorGUI.DrawRect(backgroundRect, headerColor); // Semi-transparent black

                    // Draw the GameObject name with the custom style in the adjusted position
                    EditorGUI.LabelField(labelRect, obj.name, style);

                    // Ensure the custom foldout arrow is drawn last and is clearly visible
                    Rect foldoutRect = new Rect(selectionRect.x + 4, selectionRect.y, 15, selectionRect.height);
                    bool isExpanded = foldoutStates[ID];

                    // Draw the custom foldout arrow
                    DrawCustomFoldout(foldoutRect, isExpanded); // Change color as needed

                    // Toggle the foldout state on click
                    if (Event.current.type == EventType.MouseDown && foldoutRect.Contains(Event.current.mousePosition))
                    {
                        foldoutStates[ID] = !isExpanded;
                        Event.current.Use();

                        if (foldoutStates[ID])
                        {
                            // If expanding, make sure to show previously hidden objects
                            ShowSubsequentObjects(obj);
                        }
                        else
                        {
                            // If collapsing, hide subsequent objects
                            HideSubsequentObjects(obj);
                        }
                        Debug.LogWarning($"Mouse Down Change Foldout State");
                        EditorApplication.RepaintHierarchyWindow();
                        dirtyState = true;
                    }
                    
                }
                else
                {
                    // The GameObject does not meet the criteria, ensure its children are visible
                    if (foldoutStates.ContainsKey(ID))
                    {
                        ShowSubsequentObjects(obj);
                        foldoutStates.Remove(ID);
                        previousNames.Remove(ID);
                        Debug.LogWarning($"Some sort of change, removing the foldout state for {obj.name}!");
                        EditorApplication.RepaintHierarchyWindow();
                        dirtyState = true;
                    }
                }
                if (dirtyState)
                {
                    Debug.LogWarning($"Dirty state!");
                    SaveFoldoutStatesToPrefs();
                }
            }
        }

        private static void DrawCustomFoldout(Rect rect, bool isExpanded)
        {
            // Set the arrow color based on the foldout state
            Handles.color = isExpanded ? expandedColor : collapsedColor;

            // Draw the triangle manually
            Vector3[] points = new Vector3[3];
            if (isExpanded)
            {
                points[0] = new Vector3(rect.x, rect.y + rect.height * 0.3f);
                points[1] = new Vector3(rect.x + rect.width * 0.8f, rect.y + rect.height * 0.3f);
                points[2] = new Vector3(rect.x + rect.width * 0.4f, rect.y + rect.height * 0.7f);
            }
            else
            {
                points[0] = new Vector3(rect.x + rect.width * 0.3f, rect.y);
                points[1] = new Vector3(rect.x + rect.width * 0.7f, rect.y + rect.height * 0.5f);
                points[2] = new Vector3(rect.x + rect.width * 0.3f, rect.y + rect.height);
            }

            Handles.DrawAAConvexPolygon(points);
        }

        private static void HideSubsequentObjects(GameObject headerObj)
        {
            Transform parentTransform = headerObj.transform.parent;
            int siblingIndex = headerObj.transform.GetSiblingIndex();
            int childCount = parentTransform != null ? parentTransform.childCount : headerObj.scene.rootCount;

            for (int i = siblingIndex + 1; i < childCount; i++)
            {
                GameObject sibling;
                if (parentTransform != null)
                {
                    sibling = parentTransform.GetChild(i).gameObject;
                }
                else
                {
                    sibling = headerObj.scene.GetRootGameObjects()[i];
                }

                // Check if the sibling is another header
                if (sibling.name == sibling.name.ToUpper() && !sibling.activeInHierarchy)
                {
                    break; // Stop hiding when the next header is encountered
                }

                // Hide the sibling object
                sibling.hideFlags |= HideFlags.HideInHierarchy; // Hide in Hierarchy
            }
        }
        
        private static void ShowSubsequentObjects(GameObject headerObj)
        {
            Transform parentTransform = headerObj.transform.parent;
            int siblingIndex = headerObj.transform.GetSiblingIndex();
            int childCount = parentTransform != null ? parentTransform.childCount : headerObj.scene.rootCount;

            for (int i = siblingIndex + 1; i < childCount; i++)
            {
                GameObject sibling;
                if (parentTransform != null)
                {
                    sibling = parentTransform.GetChild(i).gameObject;
                }
                else
                {
                    sibling = headerObj.scene.GetRootGameObjects()[i];
                }

                // Check if the sibling is another header
                if (sibling.name == sibling.name.ToUpper() && !sibling.activeInHierarchy)
                {
                    break; // Stop showing when the next header is encountered
                }

                // Show the sibling object
                sibling.hideFlags &= ~HideFlags.HideInHierarchy; // Remove the HideInHierarchy flag
            }
        }
        #region Save and Load Foldout States
        private static void SaveFoldoutStatesToPrefs()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            List<string> keys = new List<string>(foldoutStates.Keys);
            List<bool> values = new List<bool>(foldoutStates.Values);
            List<string> otherValues = new List<string>(previousNames.Values);
            // Convert keys and values to a JSON string
            string keysJson = JsonUtility.ToJson(new FPSerializableList<string>(keys));
            string valuesJson = JsonUtility.ToJson(new FPSerializableList<bool>(values));
            string otherJson = JsonUtility.ToJson(new FPSerializableList<string>(otherValues));
            // Save the JSON strings in EditorPrefs
            EditorPrefs.SetString(FP_UtilityData.FP_FOLDOUTSTATES_KEY + "_"+ activeScene.name, keysJson);
            EditorPrefs.SetString(FP_UtilityData.FP_FOLDOUTSTATES_VALUE + "_" + activeScene.name, valuesJson);
            EditorPrefs.SetString(FP_UtilityData.FP_PREVIOUSFOLDOUT_VALUE + "_" + activeScene.name, otherJson);
            Debug.LogWarning("Foldout states saved to EditorPrefs.");
        }
        private static void LoadFoldoutStatesFromPrefs()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            // Get the saved JSON strings from EditorPrefs
            string keysJson = EditorPrefs.GetString(FP_UtilityData.FP_FOLDOUTSTATES_KEY + "_" + activeScene.name, "{}");
            string valuesJson = EditorPrefs.GetString(FP_UtilityData.FP_FOLDOUTSTATES_VALUE + "_" + activeScene.name, "{}");
            string lastValues = EditorPrefs.GetString(FP_UtilityData.FP_PREVIOUSFOLDOUT_VALUE + "_" + activeScene.name, "{}");

            // Convert the JSON strings back into lists
            List<string> keys = JsonUtility.FromJson<FPSerializableList<string>>(keysJson).list;
            List<bool> values = JsonUtility.FromJson<FPSerializableList<bool>>(valuesJson).list;
            List<string> lastOtherValues = JsonUtility.FromJson<FPSerializableList<string>>(lastValues).list;
            // Clear the current foldoutStates and reconstruct the dictionary
            foldoutStates.Clear();
            previousNames.Clear();
            for (int i = 0; i < keys.Count; i++)
            {
                foldoutStates[keys[i]] = values[i];
                previousNames[keys[i]] = lastOtherValues[i];
            }
            Debug.LogWarning("Foldout states loaded from EditorPrefs.");
        }
        #endregion
        #region Menu Functions
        
        [MenuItem("GameObject/FuzzPhyte/Expand Z Hierarchy", false, 51)]
        private static void UnhideAllInHierarchy()
        {
            // Iterate over all GameObjects in the scene
            foreach (GameObject obj in Object.FindObjectsOfType<GameObject>(true)) // 'true' includes inactive objects
            {
                // Check if the object is hidden in the hierarchy
                if (obj.hideFlags.HasFlag(HideFlags.HideInHierarchy))
                {
                    // Remove the HideInHierarchy flag to make it visible again
                    obj.hideFlags &= ~HideFlags.HideInHierarchy;
                }

                // Get the instance ID of the object
                var InstanceID = obj.GetInstanceID();
                bool confirmGUID = true;
                var ID = FP_Utility_Editor.ReturnGUIDFromInstance(InstanceID,out confirmGUID).ToString();
               

                // Reset the foldout state to "open" (expanded)
                if (foldoutStates.ContainsKey(ID))
                {
                    foldoutStates[ID] = true; // Ensure the arrow is in the open state
                }
            }
            // Refresh the hierarchy to ensure the changes are visible
            EditorApplication.RepaintHierarchyWindow();
            SaveFoldoutStatesToPrefs();
        }
        // Method to collapse all custom sections
        [MenuItem("GameObject/FuzzPhyte/Collapse Z Sections", false, 52)]
        private static void CollapseZCustomSections()
        {
            // Collect all keys to modify in a separate list
            List<string> keysToCollapse = new List<string>(foldoutStates.Keys);

            // Iterate over the collected keys
            foreach (string key in keysToCollapse)
            {
                foldoutStates[key] = false; // Set the foldout state to collapsed
                var guidKey = new GUID(key);
                var instID = FP_Utility_Editor.GetInstanceIDFromGUID(guidKey);
                GameObject obj = EditorUtility.InstanceIDToObject(instID) as GameObject;
                // Collapse the associated objects
                if (obj != null)
                {
                    HideSubsequentObjects(obj);
                }
            }
            // Repaint the hierarchy to make sure all objects are updated
            EditorApplication.RepaintHierarchyWindow();
            SaveFoldoutStatesToPrefs();
        }
        [MenuItem("GameObject/FuzzPhyte/Reset Scene Hierarchy Data", false, 53)]
        private static void ResetSceneData()
        {
            foldoutStates.Clear();
            previousNames.Clear();
            //these resets my data
            SaveFoldoutStatesToPrefs();
            Debug.LogWarning($"Editor forced data reset! Refreshing FuzzPhyte Header!");
            // Force a repaint of the Hierarchy window to ensure OnHierarchyWindowItemOnGUI runs
            EditorApplication.RepaintHierarchyWindow();
        }
        #endregion
    }
}
