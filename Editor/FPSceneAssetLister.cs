namespace FuzzPhyte.Utility.Editor
{
    using UnityEngine;
    using UnityEditor;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEngine.SceneManagement;

    public class FPSceneAssetLister : EditorWindow
    {
        private Vector2 scrollPos;
        private List<SceneAssetInfo> sceneAssets = new List<SceneAssetInfo>();
        private string destinationFolder = "Assets/_FPUtility";
        private Dictionary<string, bool> typeToggles = new Dictionary<string, bool>();

        private string jsonFileName = "";
        private static readonly string JsonFolderPath = "Assets/_FPUtility";

        [MenuItem("FuzzPhyte/Utility/Scene Asset Tool",priority =150)]
        public static void ShowWindow()
        {
            GetWindow<FPSceneAssetLister>("Scene Asset Tool");
        }

        private void OnGUI()
        {
            GUILayout.Label("Scene Asset Collector", EditorStyles.boldLabel);
            GUILayout.Space(5);

            destinationFolder = EditorGUILayout.TextField("Destination Folder", destinationFolder);

            if (GUILayout.Button("Scan Scene for Assets"))
            {
                ScanSceneAssets();
            }

            if (sceneAssets.Count == 0)
            {
                EditorGUILayout.HelpBox("No assets scanned yet. Click the button above.", MessageType.Info);
                return;
            }
            GUILayout.Space(10);

            // === Header Toolbar ===
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                foreach (var asset in sceneAssets) asset.Selected = true;
            }
            if (GUILayout.Button("Unselect All", GUILayout.Width(100)))
            {
                foreach (var asset in sceneAssets) asset.Selected = false;
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(5);

            // === Multi-Select by Type ===
            GUILayout.Label("Select By Type:", EditorStyles.boldLabel);

            float viewWidth = EditorGUIUtility.currentViewWidth - 40; // Account for padding
            float toggleWidth = 150f; // Adjust this as needed
            int itemsPerRow = Mathf.FloorToInt(viewWidth / toggleWidth);
            if (itemsPerRow < 1) itemsPerRow = 1;

            int count = 0;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            foreach (var type in typeToggles.Keys.ToList())
            {
                typeToggles[type] = EditorGUILayout.ToggleLeft(type, typeToggles[type], GUILayout.Width(toggleWidth));
                count++;
                if (count % itemsPerRow == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Select By Checked Types"))
            {
                var selectedTypes = typeToggles.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet();
                foreach (var asset in sceneAssets)
                {
                    if (asset.IsPackageAsset) continue; // Ignore package assets

                    if (selectedTypes.Contains(asset.TypeName))
                    {
                        asset.Selected = true; // Add to the selection
                    }
                }
            }

            GUILayout.Space(10);
            // === LIST OF ASSETS ===

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (var assetInfo in sceneAssets)
            {
                if (assetInfo.Asset == null) continue;

                EditorGUILayout.BeginHorizontal();
                //assetInfo.Selected = EditorGUILayout.Toggle(assetInfo.Selected, GUILayout.Width(20));
                EditorGUI.BeginDisabledGroup(assetInfo.IsPackageAsset);
                assetInfo.Selected = EditorGUILayout.Toggle(assetInfo.Selected, GUILayout.Width(20));
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.ObjectField(assetInfo.Asset, typeof(Object), false);
                GUILayout.Label(assetInfo.TypeName, GUILayout.Width(100));
                if (GUILayout.Button("Object", GUILayout.Width(70)))
                {
                    Selection.objects = assetInfo.ReferencedBy.ToArray();
                    if (assetInfo.ReferencedBy.Count > 0)
                        EditorGUIUtility.PingObject(assetInfo.ReferencedBy[0]);
                }
                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(assetInfo.Asset);
                    Selection.activeObject = assetInfo.Asset;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            // === JSON File Name Field ===
            jsonFileName = EditorGUILayout.TextField("JSON File Name (no extension)", jsonFileName);
            GUILayout.Space(5);


            // === Action Buttons ===
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Move Selected Assets"))
            {
                int selectedCount = sceneAssets.Count(a => a.Selected);
                if (selectedCount == 0)
                {
                    EditorUtility.DisplayDialog("No Assets Selected", "Please select at least one asset to move.", "OK");
                }
                else if (EditorUtility.DisplayDialog("Move Selected Assets",
                    $"Are you sure you want to move {selectedCount} asset(s) to '{destinationFolder}'?",
                    "Yes, Move Assets", "Cancel"))
                {
                    MoveSelectedAssets(selectedCount);
                }
            }
            if (GUILayout.Button("Save to JSON"))
            {
                DumpToJson();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ScanSceneAssets()
        {
            sceneAssets.Clear();
            typeToggles.Clear();


            var dependencies = new HashSet<Object>();
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (var obj in rootObjects)
            {
                var objs = EditorUtility.CollectDependencies(new Object[] { obj });
                foreach (var dep in objs)
                {
                    if (dep == null) continue;

                    //skip transform
                    if (dep is Transform) continue;

                    string path = AssetDatabase.GetAssetPath(dep);
                    if (!string.IsNullOrEmpty(path) && !path.StartsWith("Assets/Scenes"))
                    {
                        dependencies.Add(dep); // Collect unique dependencies
                    }
                }
            }

            // Deduplicate by asset path and track GameObjects that reference them
            var uniqueAssets = new Dictionary<string, SceneAssetInfo>();
            foreach (var dep in dependencies)
            {
                var assetRef = dep;//local reference
                string path = AssetDatabase.GetAssetPath(assetRef);
                string typeName = assetRef.GetType().Name;
                
                //check for Sprites/Texture2D
                if (typeName == "Texture2D")
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType == TextureImporterType.Sprite)
                    {
                        typeName = "Sprite";
                    }
                }

                // Handle ScriptableObjects like FP_Data
                if (typeof(ScriptableObject).IsAssignableFrom(assetRef.GetType()))
                {
                    typeName = "ScriptableObject";
                }

                // Handle Mesh (or any sub-asset) as part of FBX or other parent asset
                var mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                if ((assetRef is Mesh || assetRef is AnimationClip) && mainAsset != null && mainAsset!=assetRef)
                {
                    assetRef = mainAsset;
                    path = AssetDatabase.GetAssetPath(assetRef);
                    if (assetRef != null)
                    {
                        typeName = assetRef.GetType().Name;
                    }
                    else
                    {
                        Debug.LogWarning($"Main asset at path {path} could not be resolved");
                        continue;// skip this asset to avoid errors
                    }
                        
                }
                if (!uniqueAssets.ContainsKey(path))
                {
                    bool isPackage = path.StartsWith("Packages/");
                    bool isFontDependency = path.Contains("/Fonts/") && (typeName == "Texture2D" || typeName == "Material");
                    bool isUnityBuiltin = string.IsNullOrEmpty(path) 
                        || path.StartsWith("Resources/unity_builtin_extra")
                        || path.StartsWith("Library/") 
                        || path.StartsWith("GUI/");
                    var info = new SceneAssetInfo
                    {
                        Asset = assetRef,
                        TypeName = typeName,
                        Selected = false,
                        ReferencedBy = new List<GameObject>(),
                        IsPackageAsset = isPackage||isFontDependency|| isUnityBuiltin
                    };
                    uniqueAssets[path] = info;

                    if (!typeToggles.ContainsKey(info.TypeName))
                        typeToggles[info.TypeName] = false;
                }
            }
            // Track which GameObjects use each asset via component references
            foreach (var obj in rootObjects)
            {
                var allSceneObjects = obj.GetComponentsInChildren<Transform>(true);
                foreach (var t in allSceneObjects)
                {
                    var go = t.gameObject;
                    var components = go.GetComponents<Component>();

                    foreach (var c in components)
                    {
                        if (c == null) continue;

                        var so = new SerializedObject(c);
                        var property = so.GetIterator();

                        while (property.NextVisible(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                            {
                                string path = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                                if (!string.IsNullOrEmpty(path) && uniqueAssets.ContainsKey(path))
                                {
                                    if (!uniqueAssets[path].ReferencedBy.Contains(go))
                                    {
                                        uniqueAssets[path].ReferencedBy.Add(go);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Track scene GameObjects that are prefab instances of prefab assets
            foreach (var obj in rootObjects)
            {
                var allSceneObjects = obj.GetComponentsInChildren<Transform>(true);
                foreach (var t in allSceneObjects)
                {
                    var go = t.gameObject;
                    var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(go);

                    if (prefabSource != null)
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(prefabSource);
                        if (!string.IsNullOrEmpty(prefabPath) && uniqueAssets.ContainsKey(prefabPath))
                        {
                            var info = uniqueAssets[prefabPath];
                            if (!info.ReferencedBy.Contains(go))
                            {
                                info.ReferencedBy.Add(go);
                            }
                        }
                    }
                }
            }
            // sceneAssets = uniqueAssets.Values.OrderBy(a => AssetDatabase.GetAssetPath(a.Asset)).ToList();
            sceneAssets = uniqueAssets.Values
                .OrderBy(a => a.TypeName)
                .ThenBy(a => AssetDatabase.GetAssetPath(a.Asset))
                .ToList();


            jsonFileName = SceneManager.GetActiveScene().name + "_Assets";
        }

        private void MoveSelectedAssets(int numFiles)
        {
            if (!AssetDatabase.IsValidFolder(destinationFolder))
            {
                Debug.LogError($"Destination folder does not exist: {destinationFolder}");
                return;
            }

            Dictionary<string, string> typeToSubfolder = GetTypeToSubfolderMap();

            foreach (var assetInfo in sceneAssets)
            {
                if (!assetInfo.Selected || assetInfo.Asset == null) continue;

                string typeName = assetInfo.TypeName;
                string subfolderName = typeToSubfolder.ContainsKey(typeName) ? typeToSubfolder[typeName] : "Other";

                string subfolderPath = Path.Combine(destinationFolder, subfolderName).Replace("\\", "/");
                // Ensure the full subfolder path exists
                EnsureFolderExists(subfolderPath);
                
                string assetPath = AssetDatabase.GetAssetPath(assetInfo.Asset);
                string fileName = Path.GetFileName(assetPath);
                string targetPath = Path.Combine(subfolderPath, fileName).Replace("\\", "/");

                var error = AssetDatabase.MoveAsset(assetPath, targetPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Error moving asset: {error}");
                }
                else
                {
                    Debug.Log($"Moved {assetPath} to {targetPath}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Move completed, should have moved {numFiles} file(s)!");
        }

        private void DumpToJson()
        {
            Dictionary<string, string> typeToSubfolder = GetTypeToSubfolderMap();

            var groupedAssets = new Dictionary<string, List<string>>();

            foreach (var assetInfo in sceneAssets)
            {
                if (assetInfo.Asset == null) continue;

                string typeName = assetInfo.TypeName;
                string category = typeToSubfolder.ContainsKey(typeName) ? typeToSubfolder[typeName] : "Other";

                if (!groupedAssets.ContainsKey(category))
                    groupedAssets[category] = new List<string>();

                groupedAssets[category].Add(AssetDatabase.GetAssetPath(assetInfo.Asset));
            }

            if (!AssetDatabase.IsValidFolder(JsonFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "_FPUtility");
                Debug.Log($"Created folder: {JsonFolderPath}");
            }

            string safeFileName = jsonFileName.Trim().Replace(" ", "_");
            string path = Path.Combine(JsonFolderPath, safeFileName + ".json").Replace("\\", "/");

            string json = JsonUtility.ToJson(new FPWrapper(groupedAssets), true);
            File.WriteAllText(path, json);
            Debug.Log($"Data dumped = {json}");
            AssetDatabase.Refresh();
            Debug.Log($"Dumped asset info to {path}");
            EditorUtility.RevealInFinder(path);
        }
        private void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }

        //If TextureImporter.textureType == TextureImporterType.Sprite It's a Sprite.
        private Dictionary<string, string> GetTypeToSubfolderMap()
        {
            return new Dictionary<string, string>
            {
                { "MonoScript", "Scripts" },
                { "Material", "Materials" },
                { "GameObject", "Prefabs" },
                { "Font", "Fonts" },
                { "TMP_FontAsset", "Fonts" },
                { "Mesh", "3DAssets" },
                { "Texture2D", "Textures" },
                { "Sprite", "Sprites" },
                { "Shader","Shaders" },
                { "AudioClip", "Audio" },
                { "ScriptableObject","ScriptableObjects" }
            };
        }
        [System.Serializable]
        private class FPWrapper
        {
            public List<CategoryGroup> categories = new List<CategoryGroup>();
            //public Dictionary<string, List<string>> assets;

            public FPWrapper(Dictionary<string, List<string>> data)
            {
                foreach (var kvp in data)
                {
                    categories.Add(new CategoryGroup
                    {
                        category = kvp.Key,
                        assets = kvp.Value
                    });
                }
            }
        }
        private class SceneAssetInfo
        {
            public Object Asset;
            public List<GameObject> ReferencedBy = new List<GameObject>();
            public string TypeName;
            public bool Selected;
            public bool IsPackageAsset;
        }
        [System.Serializable]
        public class CategoryGroup
        {
            public string category;
            public List<string> assets;
        }
    }
}
