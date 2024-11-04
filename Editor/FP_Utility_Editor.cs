using UnityEditor;
using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
using System.IO;
namespace FuzzPhyte.Utility.Editor
{
    //Every FP Utility Needs to be able to return the product name 
    //The Asset Sample Path being used for any Scriptable Object / Assets
    public interface IFPProductEditorUtility
    {
        public string ReturnProductName();
        public string ReturnSamplePath();

    }
    public static class FP_Utility_Editor
    {
        public static Color WarningColor = new Color(1f, 0.64f, 0);
        public static Color OkayColor = new Color(0.01f, 0.61f, 0.98f);
        /// <summary>
        /// Return Color for editor window to help with state of sequence
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        public static Color ReturnColorByStatus(SequenceStatus status)
        {
            switch (status)
            {
                case SequenceStatus.None:
                    return Color.white;
                case SequenceStatus.Locked:
                    return Color.red;
                case SequenceStatus.Unlocked:
                    return Color.yellow;
                case SequenceStatus.Active:
                    return Color.green;
                case SequenceStatus.Finished:
                    return Color.cyan;
                default:
                    return Color.white;
            }
        }
        /// <summary>
        /// Draw a line
        /// </summary>
        /// <param name="lineColor">Color of a line</param>
        public static void DrawUILine(Color lineColor)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = lineColor;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        /// <summary>
        /// Draw a line
        /// </summary>
        /// <param name="lineColor">Color of a line</param>
        /// <param name="leftPointShift">Negative value indents left to right</param>
        public static void DrawUILine(Color lineColor, float leftPointShift)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = lineColor;
            Handles.DrawLine(new Vector2(rect.x - leftPointShift, rect.y), new Vector2(rect.width, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        /// <summary>
        /// Draw a line
        /// </summary>
        /// <param name="lineColor">Color of a line</param>
        /// <param name="leftPointShift">Negative value indents left to right</param>
        /// <param name="rightPointShift">Positive value indents right to left</param>
        public static void DrawUILine(Color lineColor, float leftPointShift, float rightPointShift)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = lineColor;
            Handles.DrawLine(new Vector2(rect.x - leftPointShift, rect.y), new Vector2(rect.width - rightPointShift, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        /// <summary>
        /// Pass the rect information we need
        /// </summary>
        /// <param name="box"></param>
        public static void DrawUIBox(Rect box, float heightAdjustment, Color boxColor)
        {
            Vector3[] points = new Vector3[5];
            points[0] = box.min + new Vector2(15, 0);
            points[1] = box.min + new Vector2(box.width, 0);
            points[2] = box.max + new Vector2(0, heightAdjustment);
            points[3] = box.min + new Vector2(15, box.height + heightAdjustment);
            points[4] = box.min + new Vector2(15, 0);
            Handles.color = boxColor;
            Handles.DrawPolyLine(points);
        }
        /// <summary>
        /// Return a GUIStyle
        /// </summary>
        /// <param name="colorFont">Color of Font</param>
        /// <param name="styleFont">Style of Font</param>
        /// <param name="anchorFont">Anchor of Font</param>
        /// <returns></returns>
        [Obsolete("Use FP_Utility_Editor.ReturnStyle instead")]
        public static GUIStyle ReturnStyle(Color colorFont, FontStyle styleFont, TextAnchor anchorFont)
        {
            GUIStyleState normalState = new GUIStyleState()
            {
                textColor = colorFont,
            };
            return new GUIStyle()
            {
                fontStyle = styleFont,
                normal = normalState,
                alignment = anchorFont
            };
        }
        [Obsolete("Use FP_Utility_Editor.ReturnStyleWrap instead")]
        public static GUIStyle ReturnStyleWrap(Color colorFont, FontStyle styleFont, TextAnchor anchorFont, bool useWordWrap)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.wordWrap = useWordWrap;
            return newStyle;
        }
        [Obsolete("Use FP_Utility_Editor.ReturnStyleWrap instead")]
        public static GUIStyle ReturnStyleRichText(Color colorFont, FontStyle styleFont, TextAnchor anchorFont)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.richText = true;
            return newStyle;
        }
        /// <summary>
        /// Create a Folder at a local Asset Directory
        /// </summary>
        /// <param name="localDir">should start with Assets/...</param>
        /// <param name="relativeFolder">the last destination folder</param>
        /// <returns></returns>
        public static (bool, string) CreateAssetDatabaseFolder(string localDir, string relativeFolder)
        {
            //var fullLocalPath = localDir + "/" + relativeFolder;
            var fullLocalPath = Path.Combine(localDir,relativeFolder);
            fullLocalPath.Replace("\\","/");
            if (!AssetDatabase.IsValidFolder(fullLocalPath))
            {
                
                return (false, AssetDatabase.CreateFolder(localDir, relativeFolder));

            }
            else
            {
                return (true, fullLocalPath);
            }
        }

        /// <summary>
        /// Generates an asset path within the editor
        /// Works in tandem with the CreateAssetDatabaseFolder
        /// </summary>
        /// <param name="localDir"></param>
        /// <param name="relativeFolder"></param>
        /// <returns></returns>
        public static (bool, string) CreateAssetPath(string localDir,string relativeFolder)
        {
            var fullLocalPath = Path.Combine(localDir,relativeFolder);
            //var fullLocalPath = localDir + "/" + relativeFolder;
            //remove assets from the path
            if(fullLocalPath.Contains("Assets"))
            {
                fullLocalPath = localDir.Replace("Assets", "");
                fullLocalPath.Replace("\\","/");
            }
            if(File.Exists(Application.dataPath+ fullLocalPath))
            {
                return (true, fullLocalPath);
            }
            else
            {
                Directory.CreateDirectory(Application.dataPath + fullLocalPath);
                return (false, fullLocalPath);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="productName"></param>
        /// <param name="version"></param>
        /// <returns>first string is the local path assuming you're at Assets, the second string is the full path</returns>
        public static (string,string) CreatePackageSampleFolder(string productName,string version)
        {
            //var productSlashName = productName+"/";
            var potentialFolder = Path.Combine(Application.dataPath,"Samples", productName);
            potentialFolder.Replace("\\","/");
            if (!File.Exists(potentialFolder))
            {
                Directory.CreateDirectory(potentialFolder);
            }
            //var sampleLocalFolder = productName + " Samples";
            var versionFolder = Path.Combine(potentialFolder, version);
            versionFolder.Replace("\\","/");
            if (!File.Exists(versionFolder))
            {
                Directory.CreateDirectory(versionFolder);
            }
            AssetDatabase.Refresh();
            var sampleWithinVersion = productName + " Samples";
            var fullDirectory = Path.Combine(versionFolder, sampleWithinVersion);
            fullDirectory.Replace("\\","/");
            Debug.Log($"Full Directory {fullDirectory}");
            if (!File.Exists(fullDirectory))
            {
                Directory.CreateDirectory(fullDirectory);
            }
            var localSamplePath = Path.Combine("Samples", productName);
            var localSampleVersion = localSamplePath+version;
            var assetLocalPath = Path.Combine(localSampleVersion,sampleWithinVersion);
            assetLocalPath.Replace("\\","/");
            fullDirectory.Replace("\\","/");
            return (assetLocalPath, fullDirectory);
        }

        /// <summary>
        /// Create a simple Object asset at a path
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="assetPath">Needs to start in the Assets/ folder</param>
        public static string CreateAssetAt(UnityEngine.Object asset, string assetPath)
        {
            //var dataPath = FP_Utility_Editor.CreateAssetDatabaseFolder(FPControlUtility.SAMPLESPATH, FPControlUtility.CAT0);
            //string assetPath = AssetDatabase.GenerateUniqueAssetPath(dataPath + "/" + "ControlParameter.asset");
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();

                // Focus the asset in the Unity Editor
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = asset;
                // Register the creation in the undo system
                Undo.RegisterCreatedObjectUndo(asset, "Create " + asset.name);
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
            
            // Optionally, log the creation
            // Debug.Log("ExampleAsset created at " + assetPath);
            return $"Asset Created at {assetPath}";
        }
        /// <summary>
        /// Pass a string and the Editor will attempt at creating a new layer 
        /// </summary>
        /// <param name="layerName"></param>
        public static void CreateLayer(string layerName)
        {
            // Open the TagManager asset
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Layers Property
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            // Check if layer is already present
            bool found = false;
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
                if (layerProp.stringValue == layerName)
                {
                    found = true;
                    break;
                }
            }

            // If not found, add it
            if (!found)
            {
                // Find an empty slot
                for (int i = 8; i < layersProp.arraySize; i++)
                {
                    SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(layerProp.stringValue))
                    {
                        // Assign the layer name
                        layerProp.stringValue = layerName;
                        tagManager.ApplyModifiedProperties();
                        break;
                    }
                }
            }
        }
        public static async Task<UnityEditor.PackageManager.PackageInfo[]> SearchPackageAsync(string packageIdOrName,bool offlineMode = false)
        {
            // Ensure the packageIdOrName is not null or empty.
            if (string.IsNullOrEmpty(packageIdOrName))
            {
                throw new ArgumentException("packageIdOrName cannot be null or empty.", nameof(packageIdOrName));
            }

            // Start the search request.
            var request = Client.Search(packageIdOrName, offlineMode);

            // Wait for the request to complete.
            while (!request.IsCompleted)
            {
                await Task.Delay(100); // Wait for 100 milliseconds before checking again.
            }

            // Check for errors.
            if (request.Status == StatusCode.Failure)
            {
                throw new InvalidOperationException($"Search failed: {request.Error.message}");
            }

            // Return the search results.
            return request.Result;
        }
        public static int GetInstanceIDFromGUID(GUID guid)
        {
            // Get the path of the asset from the GUID
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (!string.IsNullOrEmpty(assetPath))
            {
                // Load the asset at the specified path
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                if (obj != null)
                {
                    return obj.GetInstanceID(); // Get the instance ID of the loaded asset
                }
            }

            // If the asset couldn't be found, return -1 or handle the error accordingly
            return -1;
        }        
        public static GUID ReturnGUIDFromInstance(int instanceID, out bool success)
        {
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            //GetHashCode
            var getGlobalID = GlobalObjectId.GetGlobalObjectIdSlow(instanceID).assetGUID;
            if (getGlobalID.ToString() == "GlobalObjectId_V1-0-00000000000000000000000000000000-0-0")
            {
                success = false;
            }
            else
            {
                success = true;
            }
            return getGlobalID;
        }
        /// <summary>
        /// will return null if it doesn't exist
        /// this searches inactive objects as well
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static GameObject FindGameObjectByName(string name)
        {
            GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            // Iterate through all GameObjects to find the one with the matching name
            for(int i=0; i < allGameObjects.Length; i++)
            {
                var obj = allGameObjects[i];
                if (obj.name == name)
                {
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)))
                    {
                        return obj; // Return the first matching GameObject
                    }
                }
            }
            /*
            foreach (GameObject obj in allGameObjects)
            {
                // Check if the object is in the scene (filter out prefab assets)
                if(obj.name == name)
                {
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)))
                    {
                        return obj; // Return the first matching GameObject
                    }
                }
            }
            */
            return null;
        }
        public static string ReturnEditorPath(string packageName, bool local=false)
        {
            if (local)
            {
                return Path.Combine("Assets", packageName, "Editor");   
            }
            var companyNamePackage = "com.fuzzphyte." + packageName;
            return Path.Combine("Packages", companyNamePackage, "Editor");
        }
        public static string ReturnEditorResourceIcons(string editorPath)
        {
            return Path.Combine(editorPath, "Icons");
        }
        public static Texture2D ReturnEditorIcon(string iconPath, bool package=false)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }
        public static Texture2D ReturnGUITex(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
        public static bool IsPackageLoadedViaPackageManager()
        {
            // Find a known script or asset in your package
            MonoScript tempScript = MonoScript.FromScriptableObject(ScriptableObject.CreateInstance<FP_Notification>());
            string path = AssetDatabase.GetAssetPath(tempScript);
            // Destroy the temporary ScriptableObject after use
            ScriptableObject.DestroyImmediate(tempScript,true);
           
            // Check if the path starts with "Packages/", meaning it's loaded via the Package Manager
            if (path.StartsWith("Packages/"))
            {
                return true; // The package is loaded via the Package Manager
            }
            else if (path.StartsWith("Assets/"))
            {
                return false; // The package is loaded as part of the regular Assets folder
            }

            return false;
        }
        public static string GetPackagePathForScript(string scriptClassName)
        {
            var scriptAsset = GetMonoScriptForClass(scriptClassName);

            if (scriptAsset != null)
            {
                // Get the path to the script file
                string scriptPath = AssetDatabase.GetAssetPath(scriptAsset);
                string directoryPath = Path.GetDirectoryName(scriptPath);
                Debug.Log("Script directory: " + directoryPath);
                return directoryPath;
            }

            return null;
        }
        private static MonoScript GetMonoScriptForClass(string className)
        {
            foreach (MonoScript script in Resources.FindObjectsOfTypeAll<MonoScript>())
            {
                if (script.GetClass() != null && script.GetClass().Name == className)
                {
                    return script;
                }
            }

            Debug.LogError($"Script {className} not found.");
            return null;
        }
    }
    /// <summary>
    /// Static class to manage the addition and removal of tags via other editor tools e.g. FP_Recorder
    /// </summary>
    [Serializable]
    public static class FPGenerateTag
    {
        private static int maxTags = 10000;

        //Add Tags
        public static bool CreateTag(string tagName)
        {
            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            // Tags Property
            SerializedProperty tagsProp = tagManager.FindProperty("tags");
            if (tagsProp.arraySize >= maxTags)
            {
                Debug.Log("No more tags can be added to the Tags property. You have " + tagsProp.arraySize + " tags");
                return false;
            }
            // if not found, add it
            if (!PropertyExists(tagsProp, 0, tagsProp.arraySize, tagName))
            {
                int index = tagsProp.arraySize;
                // Insert new array element
                tagsProp.InsertArrayElementAtIndex(index);
                SerializedProperty sp = tagsProp.GetArrayElementAtIndex(index);
                // Set array element to tagName
                sp.stringValue = tagName;
                Debug.Log("Tag: " + tagName + " has been added");
                // Save settings
                tagManager.ApplyModifiedProperties();

                return true;
            }
            else
            {
                //Debug.Log ("Tag: " + tagName + " already exists");
            }
            return false;
        }
        //Remove Tags
        public static bool RemoveTag(string tagName)
        {

            // Open tag manager
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            // Tags Property
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            if (PropertyExists(tagsProp, 0, tagsProp.arraySize, tagName))
            {
                SerializedProperty sp;

                for (int i = 0, j = tagsProp.arraySize; i < j; i++)
                {

                    sp = tagsProp.GetArrayElementAtIndex(i);
                    if (sp.stringValue == tagName)
                    {
                        tagsProp.DeleteArrayElementAtIndex(i);
                        Debug.Log("Tag: " + tagName + " has been removed");
                        // Save settings
                        tagManager.ApplyModifiedProperties();
                        return true;
                    }

                }
            }

            return false;

        }
        //Check if a Tag Exists
        public static bool DoesTagExist(string tag)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(tag))
                {
                    return true; // Tag exists
                }
            }
            return false; // Tag does not exist        
        }
        private static bool PropertyExists(SerializedProperty property, int start, int end, string value)
        {
            for (int i = start; i < end; i++)
            {
                SerializedProperty t = property.GetArrayElementAtIndex(i);
                if (t.stringValue.Equals(value))
                {
                    return true;
                }
            }
            return false;
        }

    }
    
}
