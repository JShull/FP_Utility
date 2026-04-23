namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public class FPVideoHashEditor: EditorWindow
    {
        [Serializable]
        public class FPVideoManifestItem
        {
            public string id;
            public string version;
            public string fileName;
            public string downloadUrl;
            public string sha256;
            public long contentLength;
        }

        [Serializable]
        public class FPVideoManifestCollection
        {
            public FPVideoManifestItem[] videos;
        }
        #region Parameters
        private UnityEngine.Object videoAsset;

        private string manifestId = string.Empty;
        private string version = "1.0.0";

        private string storageAccountRoot;// = "https://vabluetech1video.blob.core.windows.net";
        private string videosContainerName;// = "videos";
        private string manifestsContainerName;// = "manifests";
        private string manifestFileName;// = "videos_manifest.json";
        private string blobNameOverride = string.Empty;

        private string generatedHash = string.Empty;
        private long contentLength = 0;
        private string fileName = string.Empty;
        private string assetPath = string.Empty;

        private string builtVideoUrl = string.Empty;
        private string builtManifestUrl = string.Empty;

        private string prettyEntryJson = string.Empty;
        private string prettyWrappedJson = string.Empty;

        private Vector2 scrollPosition;
#endregion
        [MenuItem("FuzzPhyte/Utility/Video/Hash Generator")]
        public static void ShowWindow()
        {
            GetWindow<FPVideoHashEditor>("FP Video Hash Generator");
        }
        private void OnEnable()
        {
            storageAccountRoot = EditorPrefs.GetString(FP_UtilityData.AZURE_STORAGE_ROOT_KEY, "https://vabluetech1video.blob.core.windows.net");
            videosContainerName = EditorPrefs.GetString(FP_UtilityData.AZURE_VIDEOS_CONTAINER_KEY, "videos");
            manifestsContainerName = EditorPrefs.GetString(FP_UtilityData.AZURE_MANIFESTS_CONTAINER_KEY, "manifests");
            manifestFileName = EditorPrefs.GetString(FP_UtilityData.AZURE_MANIFEST_FILE_NAME_KEY, "videos_manifest.json");
            
            RefreshBuiltUrls();
        }
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("Video Manifest Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            videoAsset = EditorGUILayout.ObjectField("Video Asset", videoAsset, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                OnVideoAssetChanged();
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Asset Path", assetPath);
                EditorGUILayout.TextField("Resolved File Name", fileName);
                EditorGUILayout.LongField("Content Length", contentLength);
                EditorGUILayout.TextField("SHA256", generatedHash);
            }

            EditorGUILayout.Space();
            GUILayout.Label("Manifest Settings", EditorStyles.boldLabel);

            manifestId = EditorGUILayout.TextField("Manifest ID", manifestId);
            version = EditorGUILayout.TextField("Version", version);

            EditorGUILayout.Space();
            GUILayout.Label("Azure URL Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            storageAccountRoot = EditorGUILayout.TextField("Storage Root", storageAccountRoot);
            videosContainerName = EditorGUILayout.TextField("Videos Container", videosContainerName);
            manifestsContainerName = EditorGUILayout.TextField("Manifests Container", manifestsContainerName);
            manifestFileName = EditorGUILayout.TextField("Manifest File Name", manifestFileName);
            blobNameOverride = EditorGUILayout.TextField("Blob Name Override", blobNameOverride);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(FP_UtilityData.AZURE_STORAGE_ROOT_KEY, storageAccountRoot);
                EditorPrefs.SetString(FP_UtilityData.AZURE_VIDEOS_CONTAINER_KEY, videosContainerName);
                EditorPrefs.SetString(FP_UtilityData.AZURE_MANIFESTS_CONTAINER_KEY, manifestsContainerName);
                EditorPrefs.SetString(FP_UtilityData.AZURE_MANIFEST_FILE_NAME_KEY, manifestFileName);

                // Only persist this too if you actually want it remembered between sessions.
                // EditorPrefs.SetString(FP_UtilityData.AZURE_BLOB_NAME_OVERRIDE_KEY, blobNameOverride);

                RefreshBuiltUrls();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild URLs"))
            {
                RefreshBuiltUrls();
            }

            if (GUILayout.Button("Clear Blob Override"))
            {
                blobNameOverride = string.Empty;
                RefreshBuiltUrls();
            }
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Built Video URL", builtVideoUrl);
                EditorGUILayout.TextField("Built Manifest URL", builtManifestUrl);
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(videoAsset == null))
            {
                if (GUILayout.Button("Generate Manifest JSON", GUILayout.Height(30)))
                {
                    GenerateManifestJson();
                }

                if (GUILayout.Button("Refresh Hash", GUILayout.Height(30)))
                {
                    RefreshFileData();
                    GenerateManifestJson();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(builtVideoUrl)))
            {
                if (GUILayout.Button("Copy Built Video URL"))
                {
                    EditorGUIUtility.systemCopyBuffer = builtVideoUrl;
                    Debug.Log("Copied built video URL to clipboard.");
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(builtManifestUrl)))
            {
                if (GUILayout.Button("Copy Built Manifest URL"))
                {
                    EditorGUIUtility.systemCopyBuffer = builtManifestUrl;
                    Debug.Log("Copied built manifest URL to clipboard.");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (!string.IsNullOrEmpty(prettyEntryJson))
            {
                GUILayout.Label("Single Manifest Entry", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(prettyEntryJson, GUILayout.MinHeight(140));

                if (GUILayout.Button("Copy Entry JSON To Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = prettyEntryJson;
                    Debug.Log("Copied manifest entry JSON to clipboard.");
                }

                EditorGUILayout.Space();
            }

            if (!string.IsNullOrEmpty(prettyWrappedJson))
            {
                GUILayout.Label("Wrapped Manifest JSON", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(prettyWrappedJson, GUILayout.MinHeight(180));

                if (GUILayout.Button("Copy Wrapped Manifest To Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = prettyWrappedJson;
                    Debug.Log("Copied wrapped manifest JSON to clipboard.");
                }
            }

            EditorGUILayout.EndScrollView();
        }
        private void OnVideoAssetChanged()
        {
            assetPath = string.Empty;
            fileName = string.Empty;
            contentLength = 0;
            generatedHash = string.Empty;
            prettyEntryJson = string.Empty;
            prettyWrappedJson = string.Empty;

            if (videoAsset == null)
            {
                manifestId = string.Empty;
                builtVideoUrl = string.Empty;
                RefreshBuiltUrls();
                return;
            }

            assetPath = AssetDatabase.GetAssetPath(videoAsset);

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Debug.LogError("Selected object does not have a valid AssetDatabase path.");
                return;
            }

            fileName = Path.GetFileName(assetPath);

            if (string.IsNullOrWhiteSpace(manifestId))
            {
                manifestId = MakeSafeId(Path.GetFileNameWithoutExtension(fileName));
            }

            RefreshFileData();
            RefreshBuiltUrls();

        }
        private void RefreshFileData()
        {
            if (videoAsset == null)
            {
                return;
            }

            assetPath = AssetDatabase.GetAssetPath(videoAsset);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Debug.LogError("Could not resolve AssetDatabase path.");
                return;
            }

            string fullPath = GetFullPathFromAssetPath(assetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"File not found at resolved path: {fullPath}");
                return;
            }

            FileInfo info = new FileInfo(fullPath);
            contentLength = info.Length;
            fileName = info.Name;
            generatedHash = ComputeSHA256(fullPath);

            RefreshBuiltUrls();
        }
        private void RefreshBuiltUrls()
        {
            string resolvedBlobName = GetResolvedBlobName();

            builtVideoUrl = BuildBlobUrl(storageAccountRoot, videosContainerName, resolvedBlobName);
            builtManifestUrl = BuildBlobUrl(storageAccountRoot, manifestsContainerName, manifestFileName);
        }
        private string GetResolvedBlobName()
        {
            if (!string.IsNullOrWhiteSpace(blobNameOverride))
            {
                return blobNameOverride;
            }

            return fileName;
        }
        private void GenerateManifestJson()
        {
            if (videoAsset == null)
            {
                Debug.LogError("No video asset selected.");
                return;
            }

            if (string.IsNullOrWhiteSpace(manifestId))
            {
                Debug.LogError("Manifest ID is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                Debug.LogError("Version is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                Debug.LogError("File name could not be resolved.");
                return;
            }

            if (string.IsNullOrWhiteSpace(generatedHash))
            {
                Debug.LogError("Hash has not been generated.");
                return;
            }

            if (contentLength <= 0)
            {
                Debug.LogError("Content length is invalid.");
                return;
            }

            if (string.IsNullOrWhiteSpace(builtVideoUrl))
            {
                Debug.LogError("Built video URL is empty. Check the storage root, container name, and blob name.");
                return;
            }

            var entry = new FPVideoManifestItem
            {
                id = manifestId,
                version = version,
                fileName = fileName,
                downloadUrl = builtVideoUrl,
                sha256 = generatedHash,
                contentLength = contentLength
            };

            var wrapped = new FPVideoManifestCollection
            {
                videos = new[] { entry }
            };

            prettyEntryJson = PrettyPrintJson(JsonUtility.ToJson(entry, false));
            prettyWrappedJson = PrettyPrintJson(JsonUtility.ToJson(wrapped, false));
        }
        private static string GetFullPathFromAssetPath(string unityAssetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.GetFullPath(Path.Combine(projectRoot, unityAssetPath));
        }
        private static string ComputeSHA256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(stream);
                StringBuilder sb = new StringBuilder(hashBytes.Length * 2);

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
        private static string BuildBlobUrl(string root, string container, string blobName)
        {
            string cleanRoot = (root ?? string.Empty).Trim().TrimEnd('/');
            string cleanContainer = (container ?? string.Empty).Trim().Trim('/');
            string cleanBlobName = (blobName ?? string.Empty).Trim().TrimStart('/');

            if (string.IsNullOrWhiteSpace(cleanRoot) ||
                string.IsNullOrWhiteSpace(cleanContainer) ||
                string.IsNullOrWhiteSpace(cleanBlobName))
            {
                return string.Empty;
            }

            return $"{cleanRoot}/{cleanContainer}/{cleanBlobName}";
        }
        private static string MakeSafeId(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "video_item";
            }

            string safe = rawName.Trim().ToLowerInvariant();
            safe = safe.Replace(" ", "_");
            safe = safe.Replace("-", "_");

            StringBuilder sb = new StringBuilder(safe.Length);
            for (int i = 0; i < safe.Length; i++)
            {
                char c = safe[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
            }

            return string.IsNullOrWhiteSpace(sb.ToString()) ? "video_item" : sb.ToString();
        }
        private static string PrettyPrintJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();
            bool inQuotes = false;
            int indent = 0;

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];

                switch (ch)
                {
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        int index = i;
                        while (index > 0 && json[--index] == '\\')
                        {
                            escaped = !escaped;
                        }

                        if (!escaped)
                        {
                            inQuotes = !inQuotes;
                        }
                        break;

                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!inQuotes)
                        {
                            sb.AppendLine();
                            indent++;
                            sb.Append(new string(' ', indent * 2));
                        }
                        break;

                    case '}':
                    case ']':
                        if (!inQuotes)
                        {
                            sb.AppendLine();
                            indent--;
                            sb.Append(new string(' ', indent * 2));
                            sb.Append(ch);
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;

                    case ',':
                        sb.Append(ch);
                        if (!inQuotes)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                        }
                        break;

                    case ':':
                        sb.Append(inQuotes ? ":" : ": ");
                        break;

                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
