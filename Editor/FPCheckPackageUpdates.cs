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
        public static FPManifest LoadFromFile(string manifestPath)
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
                if (inDependenciesSection && line.Contains("com.fuzzphyte."))
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
    public static class FPCheckPackageUpdates
    {
        static List<string> _packageUrls;
        static List<Request> _updateRequests;

        static FPCheckPackageUpdates()
        {
            //EditorApplication.update += CheckForPackageUpdates;
        }
        // Add a menu item in the Unity Editor under "Tools/FuzzPhyte/Update FP Packages"
        [MenuItem("FuzzPhyte/Utility/Update Packages")]
        public static void RunPackageUpdateCheck()
        {
            CheckForPackageUpdates();
        }
        private static void CheckForPackageUpdates()
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
            FPManifest manifest = FPManifest.LoadFromFile(manifestPath);
            if (manifest == null)
            {
                UnityEngine.Debug.LogError("Null on Manifest");
                return;
            }
            
            for (int i=0; i<manifest.dependencyUrls.Count;i++)
            {
                var dependency = manifest.dependencyUrls[i];
                UnityEngine.Debug.Log($"Fetching latest for package: {dependency}");
                var request = Client.Add(dependency);
                if(request != null)
                {
                    _updateRequests.Add(request);
                }
            }
            UnityEngine.Debug.Log($"Send off for requests");
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