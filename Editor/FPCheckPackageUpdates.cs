// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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

        public static bool TryGetDependencyReference(string manifestPath, string packageName, out string dependencyReference)
        {
            dependencyReference = string.Empty;

            if (!File.Exists(manifestPath))
            {
                UnityEngine.Debug.LogError("Could not find manifest.json file.");
                return false;
            }

            string[] lines = File.ReadAllLines(manifestPath);
            bool inDependenciesSection = false;
            Regex dependencyPattern = new Regex($"^\\s*\"{Regex.Escape(packageName)}\"\\s*:\\s*\"([^\"]+)\"");

            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("\"dependencies\":"))
                {
                    inDependenciesSection = true;
                    continue;
                }

                if (inDependenciesSection && line.Trim().StartsWith("}"))
                {
                    break;
                }

                if (!inDependenciesSection)
                {
                    continue;
                }

                Match match = dependencyPattern.Match(line);
                if (match.Success)
                {
                    dependencyReference = match.Groups[1].Value;
                    return true;
                }
            }

            return false;
        }
    }
    #endregion
    //quick way to check for FuzzPhyte package updates
    public static class FPCheckPackageUpdates
    {
        private const string UtilityPackageName = "com.fuzzphyte.utility";
        static List<Request> _updateRequests;
        
        // Add a menu item in the Unity Editor under "Tools/FuzzPhyte/Update FP Packages"
        [MenuItem("FuzzPhyte/Utility/Update Packages", priority = FP_UtilityData.MENU_UTILITY_PACKAGES)]
        public static void RunPackageUpdateCheck()
        {
            CheckForPackageUpdates("com.fuzzphyte.");
        }

        public static void RunUtilityPackageUpdateCheck()
        {
            CheckForPackageUpdate(UtilityPackageName);
        }

        private static string GetManifestPath()
        {
            var projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;
            return Path.Combine(projectRoot, "Packages", "manifest.json");
        }

        private static void CheckForPackageUpdate(string packageName)
        {
            string manifestPath = GetManifestPath();
            if (!FPManifest.TryGetDependencyReference(manifestPath, packageName, out string dependencyReference))
            {
                UnityEngine.Debug.LogWarning($"Could not find {packageName} in manifest.json dependencies.");
                return;
            }

            StartPackageUpdateRequests(new List<string> { dependencyReference });
        }

        private static void CheckForPackageUpdates(string packageNameConvention)
        {
            //EditorApplication.update -= CheckForPackageUpdates;

            // Get all FP_ package URLs from the manifest
            string manifestPath = GetManifestPath();
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
            StartPackageUpdateRequests(manifest.dependencyUrls);
        }

        private static void StartPackageUpdateRequests(List<string> dependencyReferences)
        {
            _updateRequests = new List<Request>();
            for (int i=0; i<dependencyReferences.Count;i++)
            {
                var dependency = dependencyReferences[i];
                //UnityEngine.Debug.Log($"Fetching latest for package: {dependency}");
                var request = Client.Add(dependency);
                if(request != null)
                {
                    _updateRequests.Add(request);
                }
            }

            UnityEngine.Debug.Log($"Send off {dependencyReferences.Count} requests");
            // Start listening for update completion
            EditorApplication.update -= MonitorUpdateRequests;
            EditorApplication.update += MonitorUpdateRequests;
        }
        
        private static void MonitorUpdateRequests()
        {
            if (_updateRequests == null || _updateRequests.Count == 0)
            {
                EditorApplication.update -= MonitorUpdateRequests;
                return;
            }

            bool allComplete = true;
            foreach (var request in _updateRequests)
            {
                if (!request.IsCompleted)
                {
                    allComplete = false;
                    continue;
                }
            }

            if (!allComplete)
            {
                return;
            }

            foreach (var request in _updateRequests)
            {
                if (request.Status == StatusCode.Success)
                {
                    UnityEngine.Debug.Log($"Package {request.ToString() } updated successfully.");
                }
                else if (request.Status >= StatusCode.Failure)
                {
                    UnityEngine.Debug.LogError($"Failed to update package: {request.Error.message}");
                }
            }

            EditorApplication.update -= MonitorUpdateRequests;
            _updateRequests = null;
        }
    }

}
