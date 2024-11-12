namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    using System.Diagnostics;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor.PackageManager;
    using UnityEditor.PackageManager.Requests;

    #region Manifest Related
    // Wrapper class for deserialization with JsonUtility
    [System.Serializable]
    public class FPManifest
    {
        public Dictionary<string, string> dependencies = new Dictionary<string, string>();

        // Custom JSON parsing for JsonUtility, since it doesn't support dictionaries directly
        public static FPManifest FromJson(string json)
        {
            json = "{ \"dependencies\": " + json + "}"; // Wrapping for JsonUtility
            FPManifestWrapper wrapper = JsonUtility.FromJson<FPManifestWrapper>(json);
            return new FPManifest { dependencies = wrapper.dependencies.ToDictionary(entry => entry.Key, entry => entry.Value) };
        }

        
    }
    // Helper to convert dependencies to dictionary format
    [System.Serializable]
    public class FPManifestWrapper
    {
        public List<FPManifestEntry> dependencies;
    }

    [System.Serializable]
    public class FPManifestEntry
    {
        public string Key;
        public string Value;
    }
    #endregion
    public static class FPCheckPackageUpdates
    {
        static List<string> _packageUrls;
        static List<Request> _updateRequests;

        static FPCheckPackageUpdates()
        {
            EditorApplication.update += CheckForPackageUpdates;
        }
        // Add a menu item in the Unity Editor under "Tools/FuzzPhyte/Update FP Packages"
        [MenuItem("FuzzPhyte/Utility/Update Packages")]
        public static void RunPackageUpdateCheck()
        {
            CheckForPackageUpdates();
        }
        private static void CheckForPackageUpdates()
        {
            EditorApplication.update -= CheckForPackageUpdates;

            // Get all FP_ package URLs from the manifest
            _packageUrls = GetFPPackagesFromManifest();
            _updateRequests = new List<Request>();
            UnityEngine.Debug.LogWarning($"Running request on {_packageUrls.Count} packages!");
            foreach (string packageUrl in _packageUrls)
            {
                UnityEngine.Debug.Log($"Fetching latest for package: {packageUrl}");
                var request = Client.Add(packageUrl); // This re-fetches the latest from Git
                _updateRequests.Add(request);
            }

            // Start listening for update completion
            EditorApplication.update += MonitorUpdateRequests;
        }

        private static void MonitorUpdateRequests()
        {
            bool allComplete = true;

            foreach (var request in _updateRequests)
            {
                if (!request.IsCompleted)
                {
                    allComplete = false;
                    continue;
                }

                if (request.Status == StatusCode.Success)
                {
                    UnityEngine.Debug.Log($"Package {request.ToString() } updated successfully.");
                }
                else if (request.Status >= StatusCode.Failure)
                {
                    UnityEngine.Debug.LogError($"Failed to update package: {request.Error.message}");
                }
            }

            // If all requests are complete, stop monitoring
            if (allComplete)
            {
                EditorApplication.update -= MonitorUpdateRequests;
            }
        }

        private static List<string> GetFPPackagesFromManifest()
        {
            //string path = @"C:\Folder1\Folder2\Folder3\Folder4";
            //string newPath = Path.GetFullPath(Path.Combine(path, @"..\..\"));
            var projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;

            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            UnityEngine.Debug.LogWarning($"Checking for a manifest at: {manifestPath}");
            List<string> packagePaths = new List<string>();

            if (!File.Exists(manifestPath))
            {
                UnityEngine.Debug.LogError("Could not find manifest.json file.");
                return packagePaths;
            }

            // Read JSON content and parse
            string jsonContent = File.ReadAllText(manifestPath);
            UnityEngine.Debug.Log($"Manifest content: {jsonContent}");
            FPManifest manifest = JsonUtility.FromJson<FPManifest>(jsonContent);

            // Filter and build package paths
            foreach (var dependency in manifest.dependencies)
            {
                if (dependency.Key.StartsWith("com.fuzzphyte."))
                {
                    string packagePath = dependency.Value;
                    packagePaths.Add(packagePath);
                }
            }

            return packagePaths;
        }

        private static void CheckForGitUpdates(string packagePath)
        {
            /*
            if (!Directory.Exists(packagePath))
            {
                UnityEngine.Debug.LogWarning($"Package path not found: {packagePath}");
                return;
            }
            */

            RunGitCommand(packagePath, "fetch");
            string output = RunGitCommand(packagePath, "status -uno");

            if (output.Contains("Your branch is behind"))
            {
                UnityEngine.Debug.Log($"Package {packagePath} has updates available.");
            }
        }

        private static string RunGitCommand(string workingDirectory, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }

}
