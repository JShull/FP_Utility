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
        private static int adjHeaderWidth = 40;
        private static string lastChangedObjectName;
        private static string scriptAssetPath;
        private static Texture2D hhCloseIcon;
        private static Texture2D hhOpenIcon;
        private static Texture2D hhSelectAllIcon;
        private static Texture2D hhSelectAllIconActive;
        private static bool dragSelectionActive;
        private static string selectedObjectName;
        private static Vector2 initialMousePosition;
        static FP_HHeader()
        {
            Debug.LogWarning($"Editor Setup Initialized FP_HHeader");
            scriptAssetPath = FP_Utility_Editor.GetPackagePathForScript("FP_HHeader");
            hhCloseIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(scriptAssetPath + "/Icons/HH_Close.png");
            hhOpenIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(scriptAssetPath + "/Icons/HH_Open.png");
            hhSelectAllIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(scriptAssetPath + "/Icons/HH_SelectAll.png");
            hhSelectAllIconActive = AssetDatabase.LoadAssetAtPath<Texture2D>(scriptAssetPath + "/Icons/HH_SelectAllActive.png");
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
                Debug.LogWarning($"Loading via Scene Change");
                return;
            }
            
            var foldoutKeys = new List<string>(foldoutStates.Keys);

            for (int i = 0; i < foldoutKeys.Count; i++)
            {
                var key = foldoutKeys[i];
                //GUID gUID = new(key);
                //var instanceID = FP_Utility_Editor.GetInstanceIDFromGUID(gUID);
                GameObject obj = FP_Utility_Editor.FindGameObjectByName(key);
                //Debug.LogWarning($"Looking for gameobject named: {key}");
                if (obj!=null)
                {
                    
                    //Debug.LogWarning($"GameObject Found by Key which is the name: {obj.name}");
                    if (!PreviousNameCheck(key,obj))
                    {
                        Debug.LogWarning($"A Previous Name Look Failed: this wasn't in the cache, lets add it");
                        previousNames.Add(key, obj.name);
                        dirtyState = true;
                    }
                }
                else
                {
                    // we didn't find the object anymore, so we need to confirm that it wasn't in the previousName
                    Debug.LogError($"We didn't find the gameobject, was looking for {key} which an object should be in the Hierarchy, maybe the name changed?");
                    if(!PreviousNameCheck(key, null))
                    {
                        Debug.LogWarning($"A Name Changed  Check, but no previous key, and it failed the null which means this was 100% a name change and we caught it!");
                        foldoutStates.Remove(key);
                        if (previousNames.ContainsKey(key))
                        {
                            previousNames.Remove(key);
                        }
                        dirtyState = true;
                    }
                }
            }
          
            if (dirtyState)
            {
                SaveFoldoutStatesToPrefs();
                EditorApplication.RepaintHierarchyWindow();
            }
        }

        private static bool PreviousNameCheck(string key, GameObject obj)
        {
            if (previousNames.ContainsKey(key))
            {
                var prevName = previousNames[key];
                if (obj == null)
                {
                    //the assumption is to use the current name of the last item changed
                    Debug.LogWarning($"We had our Obj return null, lets use the name of the last object changed {lastChangedObjectName}");
                    obj = FP_Utility_Editor.FindGameObjectByName(lastChangedObjectName);
                    if (obj != null)
                    {
                        //this is the last item we changed
                        UpdatePreviousData(obj, prevName, key);
                    }
                    else
                    {
                        Debug.LogWarning($"Loop... {EditorApplication.timeSinceStartup}");
                    }
                }
                else
                {
                    UpdatePreviousData(obj, prevName, key);
                }
                
                return true;
            }
            return false;
        }
        private static void UpdatePreviousData(GameObject obj, string prevName, string key)
        {
            if (obj.name != prevName)
            {
                // Name has changed, check if it still meets the criteria
                Debug.LogWarning($"NAME CHANGED*************************");

                if (obj.name != obj.name.ToUpper() || obj.activeInHierarchy)
                {
                    // No longer meets criteria, unhide all children and remove tracking
                    ShowSubsequentObjects(obj);
                    foldoutStates.Remove(key);
                    previousNames.Remove(key);
                }
                else
                {
                    // still meets the criteria just reset the data
                    var previousState = foldoutStates[prevName];
                    
                    foldoutStates.Remove(prevName);
                    previousNames.Remove(prevName);
                    if (!foldoutStates.ContainsKey(obj.name))
                    {
                        foldoutStates.Add(obj.name, previousState);

                    }
                    else
                    {
                        ShowSubsequentObjects(obj);
                    }
                    lastChangedObjectName = obj.name;
                    Debug.LogWarning($"Updating last changed object = {lastChangedObjectName}");
                }
                EditorApplication.RepaintHierarchyWindow();
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
                        //bool confirmGUID = true;
                        //var GUIDKey = FP_Utility_Editor.ReturnGUIDFromInstance(sceneRootObject.GetInstanceID(),out confirmGUID).ToString();
                        // Check if the scene root object is in foldoutStates and is currently collapsed
                        var key = sceneRootObject.name;
                        if (foldoutStates.ContainsKey(key))
                        {
                            if (!foldoutStates[key])
                            {
                                // Unfold the section to reveal this object
                                foldoutStates[key] = true;
                                ShowSubsequentObjects(sceneRootObject);
                                EditorApplication.RepaintHierarchyWindow();
                                // Highlight the original selected item
                                EditorGUIUtility.PingObject(selectedObj);
                                SaveFoldoutStatesToPrefs();
                                Debug.LogError($"Selection changed?");
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
            //get position in the inspector
            
            bool dirtyState=false;
            if (obj != null)
            {
                // get stored GUID
                //bool confirmGUID = true;
                var ID = obj.name;
                //get guid from gameobject?
                // Check if the GameObject name is in all caps and if it is not active (disabled)
                if (obj.name == obj.name.ToUpper() && !obj.activeInHierarchy && obj.transform.childCount==0)
                {
                    // Track foldout state using the GameObject's instance ID
                    //Debug.LogWarning($"Inspecting {obj.name}, Current Foldout size: {foldoutStates.Count}");
                    /*
                    for(int i=0;i < foldoutStates.Count; i++)
                    {
                        var aKey = foldoutStates.Keys.ToList();
                        //Debug.LogWarning($"LOOP: Foldout Debug: ID {aKey[i]} | Status: {foldoutStates[aKey[i]]}");
                    }
                    */
                    if (!foldoutStates.ContainsKey(ID))
                    {
                        foldoutStates.Add(ID, true);
                        Debug.LogWarning($"Foldout Debug: ID {ID} | Name: {obj.name} |Status: {foldoutStates[ID]}");
                        dirtyState = true;
                        lastChangedObjectName= obj.name;
                        Debug.LogWarning($"Updating last changed object = {lastChangedObjectName}");
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
                        Event.current.Use();
                        dirtyState = true;
                    }

                    // Draw the custom select all icon
                    Rect selectAllRect = new Rect(selectionRect.x + 20, selectionRect.y, 15, selectionRect.height);
                    // Draw the custom select all icon
                    if(obj.name == selectedObjectName)
                    {
                        DrawSelectAllIcon(selectAllRect, dragSelectionActive);
                    }
                    else
                    {
                        DrawSelectAllIcon(selectAllRect, false);
                    }
                    //DrawSelectAllIcon(selectAllRect,dragSelectionActive);
                    if (Event.current.type == EventType.MouseDown && selectAllRect.Contains(Event.current.mousePosition))
                    {
                        SelectAllSubsequentObjects(obj);
                        EditorApplication.RepaintHierarchyWindow();
                        //Event.current.Use();
                        dragSelectionActive = true;
                        initialMousePosition = Event.current.mousePosition;
                        selectedObjectName = obj.name;
                        //Event.current.Use();
                    }

                    // Handle MouseUp event to end dragging
                    if (Event.current.type == EventType.MouseUp && dragSelectionActive)
                    {
                        dragSelectionActive = false; // Stop dragging when mouse is released
                        Event.current.Use(); // Consume the event
                        //Debug.LogWarning($"Finished Dragg");
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
                if(hhOpenIcon != null)
                {
                    GUI.DrawTexture(rect, hhOpenIcon,ScaleMode.ScaleToFit,true);
                    return;
                }
                points[0] = new Vector3(rect.x, rect.y + rect.height * 0.3f);
                points[1] = new Vector3(rect.x + rect.width * 0.8f, rect.y + rect.height * 0.3f);
                points[2] = new Vector3(rect.x + rect.width * 0.4f, rect.y + rect.height * 0.7f);
            }
            else
            {
                if (hhCloseIcon != null)
                {
                    GUI.DrawTexture(rect, hhCloseIcon,ScaleMode.ScaleToFit,true);
                    return;
                }
                points[0] = new Vector3(rect.x + rect.width * 0.3f, rect.y);
                points[1] = new Vector3(rect.x + rect.width * 0.7f, rect.y + rect.height * 0.5f);
                points[2] = new Vector3(rect.x + rect.width * 0.3f, rect.y + rect.height);
            }

            Handles.DrawAAConvexPolygon(points);
        }
        private static void DrawSelectAllIcon(Rect rect,bool activeSelection=false)
        {

            if (hhSelectAllIcon != null && !activeSelection)
            {
                GUI.DrawTexture(rect, hhSelectAllIcon, ScaleMode.ScaleToFit, true);
                return;
            }
            if(hhSelectAllIconActive != null && activeSelection)
            {
                GUI.DrawTexture(rect, hhSelectAllIconActive, ScaleMode.ScaleToFit, true);
                return;
            }
            // Set up the colors for the radio button
            Color outerColor = Color.white; // Outer circle color (unselected state)
            Color innerColor = Color.clear; // Inner circle (selected state)

            // Draw the outer circle (radio button)
            Handles.color = outerColor;
            Handles.DrawSolidDisc(rect.center, Vector3.forward, rect.width / 2f); // Draw the outer circle
            Handles.color = innerColor;
            Handles.DrawSolidDisc(rect.center, Vector3.forward, rect.width / 4f);
            // You can replace this with a custom icon if desired (e.g., a small icon texture)
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
        private static void SelectAllSubsequentObjects(GameObject headerObj)
        {
            List<GameObject> subsequentObjects = new List<GameObject>();
            Transform parentTransform = headerObj.transform.parent;
            int siblingIndex = headerObj.transform.GetSiblingIndex();
            int childCount = parentTransform != null ? parentTransform.childCount : headerObj.scene.rootCount;
            //subsequentObjects.Add(headerObj);
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

                subsequentObjects.Add(sibling);
            }
            if (subsequentObjects.Count > 0)
            {
                // Select the header along with all subsequent objects
                subsequentObjects.Insert(0, headerObj); // Optionally include the header itself

                // Assign all subsequent objects to Selection.objects
                Selection.objects = subsequentObjects.ToArray();
                //EditorApplication.RepaintHierarchyWindow();
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
                //var InstanceID = obj.GetInstanceID();
                //bool confirmGUID = true;
                var ID = obj.name;
               

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
                //var guidKey = new GUID(key);
                //var instID = FP_Utility_Editor.GetInstanceIDFromGUID(guidKey);
                GameObject obj = GameObject.Find(key);
                //GameObject obj = EditorUtility.InstanceIDToObject(instID) as GameObject;
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
