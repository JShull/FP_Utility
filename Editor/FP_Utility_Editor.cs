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
        public static GUIStyle ReturnStyleWrap(Color colorFont, FontStyle styleFont, TextAnchor anchorFont, bool useWordWrap)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.wordWrap = useWordWrap;
            return newStyle;
        }
        public static GUIStyle ReturnStyleRichText(Color colorFont, FontStyle styleFont, TextAnchor anchorFont)
        {
            var newStyle = ReturnStyle(colorFont, styleFont, anchorFont);
            newStyle.richText = true;
            return newStyle;
        }
        /// <summary>
        /// Create a Folder at a local Asset Directory
        /// </summary>
        /// <param name="localDir">shoulud start with Assets/...</param>
        /// <param name="relativeFolder">the last destination folder</param>
        /// <returns></returns>
        public static (bool, string) CreateAssetDatabaseFolder(string localDir, string relativeFolder)
        {
            var fullLocalPath = localDir + "/" + relativeFolder;
            if (!AssetDatabase.IsValidFolder(fullLocalPath))
            {
                return (false, AssetDatabase.CreateFolder(localDir, relativeFolder));

            }
            else
            {
                return (true, fullLocalPath);
            }
        }

        public static (bool, string) CreateAssetPath(string localDir,string relativeFolder)
        {
            var fullLocalPath = localDir + "/" + relativeFolder;
            //remove assets from the path
            if(fullLocalPath.Contains("Assets"))
            {
                fullLocalPath = localDir.Replace("Assets", "");
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
    }
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
