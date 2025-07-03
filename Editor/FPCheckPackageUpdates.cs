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
    using System.Text.RegularExpressions;

    #region Manifest Related
    public class FPManifest
    {
        public List<string> dependencyUrls = new List<string>();

        // Load and parse manifest.json
        public static FPManifest LoadFromFile(string manifestPath, string packageNameConvention)
        {
            var manifest = new FPManifest();

            if (!File.Exists(manifestPath))
            {
                UnityEngine.Debug.LogError("Could not find manifest.json file.");
                return null;
            }

            // Read all lines from the file
            string[] lines = File.ReadAllLines(manifestPath);
            bool inDependenciesSection = false;

            // Regular expression to capture URLs
            Regex urlPattern = new Regex(@"https?://[^\s""]+");

            foreach (string line in lines)
            {
                // Start processing once we reach "dependencies"
                if (line.Trim().StartsWith("\"dependencies\":"))
                {
                    inDependenciesSection = true;
                    continue;
                }

                // Stop processing if we exit the dependencies section
                if (inDependenciesSection && line.Trim().StartsWith("}"))
                {
                    break;
                }

                // Process lines only within the dependencies section
                // assuming package convention is something like com.<core>.PACKAGETOUPDATE = "com.fuzzphyte."
                if (inDependenciesSection && line.Contains(packageNameConvention))
                {
                    // Extract the URL using the regex pattern
                    Match match = urlPattern.Match(line);
                    if (match.Success)
                    {
                        manifest.dependencyUrls.Add(match.Value);
                    }
                }
            }

            return manifest;
        }
    }
    #endregion
    //quick way to check for FuzzPhyte package updates
    public static class FPCheckPackageUpdates
    {
        static List<Request> _updateRequests;
        
        // Add a menu item in the Unity Editor under "Tools/FuzzPhyte/Update FP Packages"
        [MenuItem("FuzzPhyte/Utility/Update Packages")]
        public static void RunPackageUpdateCheck()
        {
            CheckForPackageUpdates("com.fuzzphyte.");
        }
        private static void CheckForPackageUpdates(string packageNameConvention)
        {
            //EditorApplication.update -= CheckForPackageUpdates;

            // Get all FP_ package URLs from the manifest
            var projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;

            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                UnityEngine.Debug.LogError("Could not find manifest.json file.");
                return;
            }
            FPManifest manifest = FPManifest.LoadFromFile(manifestPath, packageNameConvention);
            if (manifest == null)
            {
                UnityEngine.Debug.LogError("Null on Manifest");
                return;
            }
            _updateRequests = new List<Request>();
            for (int i=0; i<manifest.dependencyUrls.Count;i++)
            {
                var dependency = manifest.dependencyUrls[i];
                //UnityEngine.Debug.Log($"Fetching latest for package: {dependency}");
                var request = Client.Add(dependency);
                if(request != null)
                {
                    _updateRequests.Add(request);
                }
            }
            UnityEngine.Debug.Log($"Send off {manifest.dependencyUrls.Count} requests");
            // Start listening for update completion
            MonitorUpdateRequests();
        }
        
        private static void MonitorUpdateRequests()
        {
            //bool allComplete = true;
            
            foreach (var request in _updateRequests)
            {
                if (!request.IsCompleted)
                {
                    //allComplete = false;
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
            /*
            // If all requests are complete, stop monitoring
            if (allComplete)
            {
                //EditorApplication.update -= MonitorUpdateRequests;
            }
            */
        }
    }

}
