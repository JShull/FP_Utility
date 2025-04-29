namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    using System.Collections.Generic;
    using UnityEngine.SceneManagement;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Linq;
    using System;

    [InitializeOnLoad]
    public static class FP_HHeader
    {
        private static Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        private static Dictionary<string, string> previousNames = new Dictionary<string, string>();
        [Tooltip("Cache state before we play so we can come back after play ends")]
        private static Dictionary<string,bool> editRuntimeFoldoutStates = new Dictionary<string,bool>();
        private static Color expandedColor = new Color(0.412f, 0.678f, 0, 1);
        private static Color collapsedColor = new Color(0.728f, 0.678f, 0, 1);
        private static Color headerColor = new Color(0.0f, 0.0f, 0.0f, 1);
        private static int adjHeaderWidth = 40;
        private static string lastChangedObjectName;
        private static int loopLookCount = 0;
        private static Texture2D hhCloseIcon;
        private static Texture2D hhOpenIcon;
        private static Texture2D hhSelectAllIcon;
        private static Texture2D hhSelectAllIconActive;
        private static bool dragSelectionActive;
        private static string selectedObjectName;
        public static bool IsEnabled => EditorPrefs.GetBool(FP_UtilityData.FP_HHeader_ENABLED_KEY+ "_" + SceneManager.GetActiveScene().name, true);

        static FP_HHeader()
        {
            Debug.LogWarning($"FP_HHeader: Editor Setup Initialized");
            //am I in the package or in the editor
            var loadedPackageManager = FP_Utility_Editor.IsPackageLoadedViaPackageManager();
            //Debug.LogWarning($"FP_HHeader: via Unity Package Manager: {loadedPackageManager}");
            var packageName = loadedPackageManager ? "utility" : "FP_Utility";
            var packageRef = FP_Utility_Editor.ReturnEditorPath(packageName, !loadedPackageManager);
            var iconRefEditor = FP_Utility_Editor.ReturnEditorResourceIcons(packageRef);
            Debug.LogWarning($"FP_HHeader: iconRefEditor = {iconRefEditor}");
            Debug.LogWarning($"FP_HHeader: packageRef = {packageRef}");
            //ICON LOAD
            var closePath = Path.Combine(iconRefEditor, "HH_Close.png");
            var openPath = Path.Combine(iconRefEditor, "HH_Open.png");
            var selectAllIcon = Path.Combine(iconRefEditor, "HH_SelectAll.png");
            var selectAllIconActive = Path.Combine(iconRefEditor, "HH_SelectAllActive.png");
           
            hhCloseIcon = FP_Utility_Editor.ReturnEditorIcon(closePath, loadedPackageManager);
            hhOpenIcon = FP_Utility_Editor.ReturnEditorIcon(openPath, loadedPackageManager);
            
            hhSelectAllIcon = FP_Utility_Editor.ReturnEditorIcon(selectAllIcon, loadedPackageManager);
            hhSelectAllIconActive = FP_Utility_Editor.ReturnEditorIcon(selectAllIconActive, loadedPackageManager);

            LoadFoldoutStatesFromPrefs(); // Load saved foldout states on editor initialization
            LoadHeaderStyleFromFile();
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
            EditorApplication.update += OnEditorUpdate; // Monitor changes in the editor
            Selection.selectionChanged += OnSelectionChanged; // Hook into the selection changed event
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged; //restores the settings I saved right when we come back from play mode
        }
        
        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            if (!IsEnabled) return;
            Scene activeScene = SceneManager.GetActiveScene();
            bool dirtyState = false;
           
            string lastScenePath = EditorPrefs.GetString(FP_UtilityData.LAST_SCENEPATH_VAR, "");
            if (activeScene.path != lastScenePath)
            {
                EditorPrefs.SetString(FP_UtilityData.LAST_SCENEPATH_VAR, activeScene.path);
                OnSceneOpened(activeScene);
                //this should load our dictionaries
               
                return;
            }
            //i don't think foldoutStates is getting updated after we have a sync/find issue we probably need to read from the file each time or have an editor pref variable flag

            var foldoutKeys = new List<string>(foldoutStates.Keys);
            Debug.LogWarning($"FP_HHeader: Keys Length: {foldoutKeys.Count}");
            for (int i = 0; i < foldoutKeys.Count; i++)
            {
                var key = foldoutKeys[i];
                
                GameObject obj = FP_Utility_Editor.FindGameObjectByNameInactive(key);
                
                if (obj!=null)
                {
                    Debug.LogWarning($"FP_HHeader: Key {key}, index {i}, obj = {obj.name}");
                    //Debug.LogWarning($"GameObject Found by Key which is the name: {obj.name}");
                    var nameCheckResults = PreviousNameCheck(key, obj, loopLookCount);
                    loopLookCount = nameCheckResults.Item2;
                    if (!nameCheckResults.Item1)
                    {
                        Debug.LogWarning($"FP_HHeader: A Previous Name Look Failed with key: {key} this wasn't in the cache, lets add that in for a value: {obj.name}, loop count {loopLookCount}");
                        previousNames.Add(key, obj.name);
                        dirtyState = true;
                    }
                }
                else
                {
                    // we didn't find the object anymore, so we need to confirm that it wasn't in the previousName
                    //Debug.LogError($"We didn't find the gameobject, was looking for {key} which an object should be in the Hierarchy, maybe the name changed?");
                    Debug.LogWarning($"FP_HHeader: Key {key}, index {i}, obj = null");
                    var nameCheckResults = PreviousNameCheck(key, null,loopLookCount);
                    loopLookCount = nameCheckResults.Item2;
                    if (!nameCheckResults.Item1)
                    {
                        //Debug.LogWarning($"A Name Changed  Check, but no previous key, and it failed the null which means this was 100% a name change and we caught it!");
                        foldoutStates.Remove(key);
                        if (previousNames.ContainsKey(key))
                        {
                            Debug.LogWarning($"FP_HHeader: Removing a key, {key} because this object failed to come back.");
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
        private static (bool,int) PreviousNameCheck(string key, GameObject obj, int loopNum=0)
        {
            if (previousNames.ContainsKey(key))
            {
                var prevName = previousNames[key];
                Debug.LogWarning($"FP_HHeader: key = {key}, value = {prevName}");
                if (obj == null)
                {
                    //the assumption is to use the current name of the last item changed
                    //Debug.LogWarning($"We had our Obj return null, lets use the name of the last object changed |{lastChangedObjectName}|");
                    if (lastChangedObjectName == "") 
                    {
                        Debug.LogWarning($"Blank Name?");
                    }
                    else
                    {
                        Debug.LogWarning($"looking for last changed object name= {lastChangedObjectName}");
                    }
                    obj = FP_Utility_Editor.FindGameObjectByNameInactive(lastChangedObjectName);
                    if (obj != null)
                    {
                        //this is the last item we changed
                        //Debug.LogWarning($"OBJ not null");
                        UpdatePreviousData(obj, prevName, key);
                    }
                    else
                    {
                        //return false;
                        loopNum++;
                        //Debug.LogWarning($"Loop {loopNum}: {EditorApplication.timeSinceStartup}");
                        if (loopNum > 100)
                        {
                            //Debug.LogError($"Item was probably destroyed");
                            return (false, 0);
                        }
                    }
                }
                else
                {
                    UpdatePreviousData(obj, prevName, key);
                }
                
                return (true,loopNum);
            }
            return (false,0);
        }
        private static void UpdatePreviousData(GameObject obj, string prevName, string key)
        {
            if (obj.name != prevName)
            {
                // Name has changed, check if it still meets the criteria
                Debug.LogWarning($"FP_HHeader: *************************:NAME CHANGED:*************************");

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
                    if (foldoutStates.ContainsKey(prevName))
                    {
                        var previousState = foldoutStates[prevName];
                        foldoutStates.Remove(prevName);
                        previousNames.Remove(prevName);
                        if (!foldoutStates.ContainsKey(obj.name))
                        {
                            foldoutStates.Add(obj.name, previousState);
                            //JOHN
                            previousNames.Add(obj.name, key);
                        }
                        else
                        {
                            ShowSubsequentObjects(obj);
                        }
                        lastChangedObjectName = obj.name;
                        Debug.LogWarning($"FP_HHeader: Updating last changed object:{prevName}, now = {lastChangedObjectName}");
                    }
                    else
                    {
                        Debug.LogWarning($"FP_HHeader: Foldout state does not contain the previous name: {prevName}");
                        ShowSubsequentObjects(obj);
                        foldoutStates.Remove(key);
                        previousNames.Remove(key);
                    }
                    
                }
                EditorApplication.RepaintHierarchyWindow();
                SaveFoldoutStatesToPrefs();
            }
        }
        private static void OnSceneOpened(Scene scene)
        {
            // Clear and reset foldout states when a new scene is opened
            //Debug.LogWarning($"FP_HHeader: Scene Changed... Resetting Foldout States");
            if (!IsEnabled) return;
            foldoutStates.Clear();
            previousNames.Clear();
            //these resets my data
            LoadFoldoutStatesFromPrefs();
            //update visuals
            var foldoutKeys = new List<string>(foldoutStates.Keys);
            for(int i= 0; i < foldoutKeys.Count; i++)
            {
                var ID = foldoutKeys[i];
                var foldOutState = foldoutStates[ID];
                var obj = FP_Utility_Editor.FindGameObjectByNameInactive(ID);
                if (obj != null)
                {
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
                }
                
                //Debug.LogWarning($"Mouse Down Change Foldout State");
                EditorApplication.RepaintHierarchyWindow();
            }
            //Debug.LogWarning($"FP_HHeader: Editor opened a new scene: {scene.name}! Refreshed FuzzPhyte Header Data Complete!");
            // Force a repaint of the Hierarchy window to ensure OnHierarchyWindowItemOnGUI runs
            LoadHeaderStyleFromFile();
            EditorApplication.RepaintHierarchyWindow();
        }
        private static void OnSelectionChanged()
        {
            if (!IsEnabled) return;
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
                                Debug.LogError($"FP_HHeader: Selection changed?");
                                break;  // Stop after expanding the first relevant root object
                            }
                        }
                    }
                }
            }
                
        }
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!IsEnabled) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                //about to enter play mode
                editRuntimeFoldoutStates = new Dictionary<string, bool>(foldoutStates);
                
                //expand everything
                ExpandAllHeaders();
                EditorApplication.RepaintHierarchyWindow();
            }
            else if(state==PlayModeStateChange.EnteredEditMode)
            {
                foldoutStates = new Dictionary<string, bool>(editRuntimeFoldoutStates);
                RestoreHiddenPrefabs();
                EditorApplication.RepaintHierarchyWindow();
            }
            /*
            //OLD
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                LoadFoldoutStatesFromPrefs(); // Restore foldout states after exiting Play Mode
                EditorApplication.RepaintHierarchyWindow();
            }
            */
        }
        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            if (!IsEnabled) return;
            // Get the GameObject associated with this hierarchy item
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            //get position in the inspector
            if (obj == null)
            {
                return;
            }
            DrawHierarchyVisuals(obj, selectionRect);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
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
                    
                    if (!foldoutStates.ContainsKey(ID))
                    {
                        foldoutStates.Add(ID, true);
                        dirtyState = true;
                        lastChangedObjectName= obj.name;
                    }
                    bool isExpanded = foldoutStates[ID];
                    var foldoutRect = ReturnFoldOutRect(selectionRect, new Vector2(4, 15));
                    ///
                    /// DRAWING STUFF BEGINS
                    /// 
                    /*
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
                    
                    

                    // Draw the custom foldout arrow
                    DrawCustomFoldout(foldoutRect, isExpanded); // Change color as needed
                    */
                    ///
                    /// END OF DRAWING
                    ///

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
                        //Debug.LogWarning($"Mouse Down Change Foldout State");
                        EditorApplication.RepaintHierarchyWindow();
                        Event.current.Use();
                        dirtyState = true;
                    }

                    // Draw the custom select all icon
                    Rect selectAllRect = ReturnFoldOutRect(selectionRect, new Vector2(20, 15));
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
                        //initialMousePosition = Event.current.mousePosition;
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
                        //Debug.LogWarning($"Some sort of change, removing the foldout state for {obj.name}!");
                        EditorApplication.RepaintHierarchyWindow();
                        dirtyState = true;
                    }
                }
                if (dirtyState)
                {
                    //Debug.LogWarning($"Dirty state!");
                    SaveFoldoutStatesToPrefs();
                }
            }
        }
        private static void DrawHierarchyVisuals(GameObject obj, Rect selectionRect)
        {
            if (obj.name != obj.name.ToUpper() || obj.activeInHierarchy || obj.transform.childCount != 0)
            {
                return; // Only apply visuals to headers
            }
            // Adjust the label position to avoid overlapping the foldout arrow

            string ID = obj.name;


            // Set up custom style for bold text
            GUIStyle style = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white } // You can change the color here
            };
            Rect labelRect = new Rect(selectionRect.x + adjHeaderWidth, selectionRect.y, selectionRect.width - adjHeaderWidth, selectionRect.height);
            // Optionally change the background color
            Rect backgroundRect = selectionRect;
            backgroundRect.xMin = 50;
            EditorGUI.DrawRect(backgroundRect, headerColor); // Semi-transparent black
            // Draw Object Name
            EditorGUI.LabelField(labelRect, obj.name, style);

            // Ensure the custom foldout arrow is drawn last and is clearly visible
            Rect foldoutRect = ReturnFoldOutRect(selectionRect,new Vector2(4,15));
            if (foldoutStates.ContainsKey(ID)) 
            {
                bool isExpanded = foldoutStates[ID];
                DrawCustomFoldout(foldoutRect, isExpanded); // Change color as needed
            }
            else
            {
                DrawCustomFoldout(foldoutRect, true);
            }

        }
        private static Rect ReturnFoldOutRect(Rect selectionRect, Vector2 xyDim)
        {
            return new Rect(selectionRect.x + xyDim.x, selectionRect.y, xyDim.y, selectionRect.height);
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

            string foldoutKey = headerObj.name;
            Dictionary<string, List<string>> hiddenObjectsByFoldout = LoadHiddenObjectsFromPrefs();

            if (!hiddenObjectsByFoldout.ContainsKey(foldoutKey)) 
            {
                hiddenObjectsByFoldout[foldoutKey] = new List<string>();
            }

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

                // if it's a prefab instance, track it under the foldout key
                if(PrefabUtility.GetPrefabInstanceStatus(sibling)== PrefabInstanceStatus.Connected)
                {
                    //Debug.LogWarning($"Found a prefab? {sibling.name}");
                    hiddenObjectsByFoldout[foldoutKey].Add(sibling.name);
                }
                // Hide the sibling object
                sibling.hideFlags |= HideFlags.HideInHierarchy; // Hide in Hierarchy
            }
            //Save updated hidden prefab data
            
            SaveHiddenObjectsToPrefs(hiddenObjectsByFoldout);
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
        //helper method to expand all without saving
        private static void ExpandAllHeaders()
        {
            var foldoutKeys = new List<string>(foldoutStates.Keys); // Create a separate list of keys
            foreach (var key in foldoutKeys)
            {
                foldoutStates[key] = true;
                GameObject obj = FP_Utility_Editor.FindGameObjectByNameInactive(key);
                if(obj != null)
                {
                    ShowSubsequentObjects(obj);
                }
            }
            EditorApplication.RepaintHierarchyWindow();
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
            //Debug.LogWarning($"FP_HHeader: Foldout states saved to EditorPrefs: {FP_UtilityData.FP_FOLDOUTSTATES_KEY}_{activeScene.name}");
        }
        private static void RestoreHiddenPrefabs()
        {
            Dictionary<string, List<string>> hiddenObjectsByFoldout = LoadHiddenObjectsFromPrefs();

            foreach (var foldoutKey in hiddenObjectsByFoldout.Keys)
            {
                foreach (string prefabName in hiddenObjectsByFoldout[foldoutKey])
                {
                    GameObject prefabInstance = FP_Utility_Editor.FindGameObjectByNameInactive(prefabName);
                    if (prefabInstance != null)
                    {
                        prefabInstance.hideFlags |= HideFlags.HideInHierarchy; // Re-hide the object
                    }
                }
            }

            // Clear stored data after restoring
            EditorPrefs.DeleteKey(FP_UtilityData.FP_HIDDENOBJECTS_KEY + "_"+SceneManager.GetActiveScene().name);
        }
        private static void SaveHiddenObjectsToPrefs(Dictionary<string, List<string>> hiddenObjects)
        {
            string key = FP_UtilityData.FP_HIDDENOBJECTS_KEY + "_"+ SceneManager.GetActiveScene().name;

            string json = JsonUtility.ToJson(new FPSerializableDictionary<string, string>(hiddenObjects), true); // Pretty print for debugging
            //Debug.Log($"Saving Hidden Objects JSON: {json}"); // Debug Output
            EditorPrefs.SetString(key, json);
        }
        private static Dictionary<string, List<string>> LoadHiddenObjectsFromPrefs()
        {
            string key = FP_UtilityData.FP_HIDDENOBJECTS_KEY + "_"+ SceneManager.GetActiveScene().name;

            if (EditorPrefs.HasKey(key))
            {
                string json = EditorPrefs.GetString(key);
                //Debug.Log($"Loading Hidden Objects JSON: {json}"); // Debug Output

                var deserializedData = JsonUtility.FromJson<FPSerializableDictionary<string, string>>(json);

                if (deserializedData == null || deserializedData.keys.list == null || deserializedData.values.list == null)
                {
                    Debug.LogError("Failed to deserialize Hidden Objects from JSON.");
                    return new Dictionary<string, List<string>>(); // Return empty dictionary
                }

                return deserializedData.ToDictionary();
            }

            return new Dictionary<string, List<string>>();
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
            Debug.LogWarning($"Keys Count: {keys.Count} | Values Count: {values.Count} | Last Values Count: {lastOtherValues.Count}");
            if(lastOtherValues.Count != keys.Count)
            {
                Debug.LogWarning($"Last Other Values Count does not match the keys count, this is a problem: Reset everything");
                for (int i = 0; i < keys.Count; i++)
                {

                    foldoutStates.Add(keys[i], values[i]);
                    //previousNames.Add(keys[i], keys[i]);
                    //previousNames[keys[i]] = lastOtherValues[i];
                }
            }
            else
            {
                for (int i = 0; i < keys.Count; i++)
                {

                    foldoutStates.Add(keys[i], values[i]);
                    if (previousNames.ContainsKey(keys[i]))
                    {
                        previousNames[keys[i]] = lastOtherValues[i];
                    }
                    else
                    {
                        previousNames.Add(keys[i], lastOtherValues[i]);
                    }
                    //previousNames[keys[i]] = lastOtherValues[i];
                }
            }
            
            //Debug.LogWarning("Foldout states loaded from EditorPrefs.");
        }
        private static void LoadHeaderStyleFromFile()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string savedHeaderFileLocation = EditorPrefs.GetString(FP_UtilityData.FP_HEADERSTYLE_VALUE + "_" + activeScene.name);
            //attempt to load this asset into the scriptable object
            try
            {
                var theHeaderStyle = (FP_HHeaderData)AssetDatabase.LoadAssetAtPath(savedHeaderFileLocation, typeof(FP_HHeaderData));
                //only load the style
                expandedColor = theHeaderStyle.ExpandedColor;
                headerColor=theHeaderStyle.HeaderColor;
                collapsedColor = theHeaderStyle.CollapsedColor;
                hhCloseIcon = theHeaderStyle.CloseIcon;
                hhOpenIcon = theHeaderStyle.OpenIcon;
                hhSelectAllIcon = theHeaderStyle.SelectAllIcon;
                hhSelectAllIconActive = theHeaderStyle.SelectAllIconActive;
            }
            catch(Exception ex)
            {
                Debug.LogWarning($"FP_HHeader: Header Data Style file probably wasn't created yet - you can ignore this! {savedHeaderFileLocation} Log: {ex.Message}");
            }
        }
        #endregion
        #region Menu Functions
        [MenuItem("FuzzPhyte/Utility/Header/Enable FP_HHeader",false,20)]
        private static void ToggleHeaderMenuMain() => ToggleHeaderSystem();
        [MenuItem("FuzzPhyte/Utility/Header/Enable FP_HHeader", true)]
        private static bool ValidateHeaderMenuMain()
        {
            ValidateHeaderMenu("FuzzPhyte/Utility/Header/Enable FP_HHeader");
            return true;
        }


        [MenuItem("GameObject/FuzzPhyte/Header/Enable FP_HHeader", false, 20)]
        private static void ToggleHeaderMenuHierarchy() => ToggleHeaderSystem();
        [MenuItem("GameObject/FuzzPhyte/Header/Enable FP_HHeader", true)]
        private static bool ValidateHeaderMenuHierarchy()
        {
            ValidateHeaderMenu("GameObject/FuzzPhyte/Header/Enable FP_HHeader");
            return true;
        }
        
        
        [MenuItem("Assets/FuzzPhyte/Header/Enable FP_HHeader", false, 20)]
        private static void ToggleHeaderMenuAssets() => ToggleHeaderSystem();

        [MenuItem("Assets/FuzzPhyte/Header/Enable FP_HHeader", true)]
        private static bool ValidateHeaderMenuAssets()
        {
            ValidateHeaderMenu("Assets/FuzzPhyte/Header/Enable FP_HHeader");
            return true;
        }
        private static void ToggleHeaderSystem()
        {
            bool newValue = !IsEnabled;
            EditorPrefs.SetBool(FP_UtilityData.FP_HHeader_ENABLED_KEY + "_" + SceneManager.GetActiveScene().name, newValue);
            Debug.LogWarning($"FP_HHeader is now {(newValue ? "Enabled" : "Disabled")}");

            if (!newValue)
            {
                UnhideAllInHierarchy(); // custom function you already have
            }
            else
            {
                ResetSceneData();
            }
            EditorApplication.RepaintHierarchyWindow();
        }
        private static void ValidateHeaderMenu(string path)
        {
            Menu.SetChecked(path, IsEnabled);
        }
        
        [MenuItem("GameObject/FuzzPhyte/Header/Expand Z Sections", false, 51)]
        private static void UnhideAllInHierarchy()
        {
            // Iterate over all GameObjects in the scene

            foreach (GameObject obj in GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // 'true' includes inactive objects
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
        [MenuItem("GameObject/FuzzPhyte/Header/Collapse Z Sections", false, 52)]
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
        [MenuItem("GameObject/FuzzPhyte/Header/Reset Z Sections Data", false, 53)]
        
        private static void ResetSceneData()
        {
            foldoutStates.Clear();
            previousNames.Clear();
            //blast editor prefs
            ClearFoldoutStatesFromPrefs();
            //these resets my data
            SaveFoldoutStatesToPrefs();
            Debug.LogWarning($"FP_HHeader: Editor forced data reset, refreshing FuzzPhyte Header!");
            // Force a repaint of the Hierarchy window to ensure OnHierarchyWindowItemOnGUI runs
            EditorApplication.RepaintHierarchyWindow();
        }
        [MenuItem("Assets/FuzzPhyte/Header/Create Headers", false, 50)]
        private static void CreateHeadersFromData()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("You can only run this in Edit Mode.");
                return;
            }

            if (Selection.activeObject is FP_HHeaderData headerData)
            {
                Undo.RegisterCompleteObjectUndo(headerData, "Create Header GameObjects");

                for (int i = 0; i < headerData.Headers.Count; i++)
                {
                    string rawName = headerData.Headers[i];
                    if (string.IsNullOrWhiteSpace(rawName))
                        continue;
                    rawName=rawName.ToUpper();
                    // Normalize name: //obj.name == obj.name.ToUpper()
                    string safeName = Regex.Replace(rawName.ToUpperInvariant(), @"[^A-Z0-9 _]", "").Trim();

                    // Check for duplicate name
                    GameObject existing = GameObject.Find(safeName);
                    if (existing != null)
                    {
                        Debug.LogWarning($"A GameObject named '{safeName}' already exists. Skipping.");
                        continue;
                    }

                    GameObject go = new GameObject(safeName);
                    Undo.RegisterCreatedObjectUndo(go, "Create Header Object");

                    go.SetActive(false);

                    // Insert at top of hierarchy in order
                    go.transform.SetSiblingIndex(i);
                }
                //visuals
                hhCloseIcon = headerData.CloseIcon;
                hhOpenIcon = headerData.OpenIcon;
                hhSelectAllIcon = headerData.SelectAllIcon;
                hhSelectAllIconActive = headerData.SelectAllIconActive;
                collapsedColor = headerData.CollapsedColor;
                expandedColor = headerData.ExpandedColor;
                headerColor = headerData.HeaderColor;
                EditorApplication.RepaintHierarchyWindow();
                Debug.Log($"Created {headerData.Headers.Count} header GameObjects from: {headerData.name}");
                CreateHeaderDataFile();
            }
            else
            {
                Debug.LogWarning("Please select a valid FP_HHeaderData asset.");
            }
        }
        [MenuItem("Assets/FuzzPhyte/Header/Save Headers", false, 51)]
        private static void CreateHeaderDataFile()
        {
            var asset = ScriptableObject.CreateInstance<FP_HHeaderData>();
            asset.Headers = new List<string>(foldoutStates.Keys.ToList());
            asset.ExpandedColor = expandedColor;
            asset.HeaderColor = headerColor;
            asset.CollapsedColor = collapsedColor;
            asset.CloseIcon = hhCloseIcon;
            asset.OpenIcon = hhOpenIcon;
            asset.SelectAllIcon = hhSelectAllIcon;
            asset.SelectAllIconActive = hhSelectAllIconActive;
            asset.UniqueID = System.DateTime.Now.ToLongTimeString()+"FPHeader_42"; 
            var path = Path.Combine("Assets", "FP_Utility\\Editor\\FP_HHeader");
            Debug.Log($"{Application.dataPath}");
            var fileName = System.DateTime.Now.ToString("MMddyyyy_hhmmss") + "_FPHHeader.asset";
            Debug.Log($"Local Path= {path}");
            var items = FP_Utility_Editor.CreateAssetPath("FP_Utility\\Editor", "FP_HHeader");
            var itemsCreatedPath = "";
            if (items.Item1)
            {
                itemsCreatedPath = FP_Utility_Editor.CreateAssetAt(asset, Path.Combine(path, fileName));
            }
            else
            {
                Debug.LogWarning($"Didn't find the directory, created it for you {items.Item2}");
                itemsCreatedPath = FP_Utility_Editor.CreateAssetAt(asset, Path.Combine(path, fileName));
            }
            Debug.LogWarning($"FP_HHeader: Asset Created at {itemsCreatedPath}");
            
            Scene activeScene = SceneManager.GetActiveScene();
            EditorPrefs.SetString(FP_UtilityData.FP_HEADERSTYLE_VALUE + "_" + activeScene.name, itemsCreatedPath);
        }
        private static void ClearFoldoutStatesFromPrefs()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            // Generate keys for this scene
            string keysKey = FP_UtilityData.FP_FOLDOUTSTATES_KEY + "_" + activeScene.name;
            string valuesKey = FP_UtilityData.FP_FOLDOUTSTATES_VALUE + "_" + activeScene.name;
            string previousKey = FP_UtilityData.FP_PREVIOUSFOLDOUT_VALUE + "_" + activeScene.name;
            string hiddenKey = FP_UtilityData.FP_HIDDENOBJECTS_KEY + "_" + activeScene.name;
            // Remove them from EditorPrefs
            EditorPrefs.DeleteKey(keysKey);
            EditorPrefs.DeleteKey(valuesKey);
            EditorPrefs.DeleteKey(previousKey);
            EditorPrefs.DeleteKey(hiddenKey);

            Debug.LogWarning($"FP_HHeader: Cleared all Editor Prefs tied to the scene: {activeScene.name}");
        }

        
        #endregion
    }
}
