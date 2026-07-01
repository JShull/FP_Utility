// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Runs FP Utility editor startup checks once per Unity editor session.
    /// </summary>
    public static class FPUtilitySessionCheck
    {
        private const string UtilityPackageName = "com.fuzzphyte.utility";
        private const string UtilityPackagePath = "Packages/com.fuzzphyte.utility/";
        private const string UtilityReadmeAssetName = "Readme";
        private const string SessionCheckKey = "FuzzPhyte.Utility.Editor.FPUtilitySessionCheck.HasRun";
        private const string ShowPackageMessagesEditorPrefsKey = "FuzzPhyte.Utility.Editor.FPUtilitySessionCheck.ShowPackageMessages";
        private const string PackageStartupMessageTitle = "FP Utility";
        private const string PackageStartupMessage = "FP Utility is checking for the latest Git package update for this Unity project.\n\nThe package Readme asset will be selected in the Project window when this message closes.";

        public static bool ShowPackageMessages
        {
            get => EditorPrefs.GetBool(ShowPackageMessagesEditorPrefsKey, true);
            set => EditorPrefs.SetBool(ShowPackageMessagesEditorPrefsKey, value);
        }

        [InitializeOnLoadMethod]
        private static void QueueSessionCheck()
        {
            EditorApplication.delayCall += RunSessionCheckOnce;
        }

        private static void RunSessionCheckOnce()
        {
            if (SessionState.GetBool(SessionCheckKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunSessionCheckOnce;
                return;
            }

            if (!IsUtilityLoadedFromPackageManager())
            {
                SessionState.SetBool(SessionCheckKey, true);
                return;
            }

            SessionState.SetBool(SessionCheckKey, true);
            ShowPackageMessageIfEnabled();
            FPCheckPackageUpdates.RunUtilityPackageUpdateCheck();
        }

        [MenuItem("FuzzPhyte/Utility/Package Messages/Show Startup Messages", priority = FP_UtilityData.MENU_UTILITY_PACKAGES + 10)]
        public static void TogglePackageMessages()
        {
            ShowPackageMessages = !ShowPackageMessages;
        }

        [MenuItem("FuzzPhyte/Utility/Package Messages/Show Startup Messages", true)]
        private static bool ValidateTogglePackageMessages()
        {
            Menu.SetChecked("FuzzPhyte/Utility/Package Messages/Show Startup Messages", ShowPackageMessages);
            return true;
        }

        private static bool IsUtilityLoadedFromPackageManager()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FPUtilitySessionCheck).Assembly);
            if (packageInfo != null && packageInfo.name == UtilityPackageName)
            {
                return true;
            }

            if (TryGetUtilityPackageRootPath(out string packageRootPath))
            {
                return packageRootPath.StartsWith(UtilityPackagePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool TryGetUtilityPackageRootPath(out string packageRootPath)
        {
            packageRootPath = null;
            const string editorScriptSuffix = "/Editor/" + nameof(FPUtilitySessionCheck) + ".cs";
            if (!TryGetSessionCheckScriptPath(out string scriptPath) ||
                !scriptPath.EndsWith(editorScriptSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            packageRootPath = scriptPath.Substring(0, scriptPath.Length - editorScriptSuffix.Length).TrimEnd('/');
            return !string.IsNullOrEmpty(packageRootPath);
        }

        private static bool TryGetSessionCheckScriptPath(out string scriptPath)
        {
            scriptPath = null;
            string[] guids = AssetDatabase.FindAssets($"{nameof(FPUtilitySessionCheck)} t:MonoScript");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace("\\", "/");
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (script != null && script.GetClass() == typeof(FPUtilitySessionCheck))
                {
                    scriptPath = assetPath;
                    return true;
                }
            }

            return false;
        }

        private static void ShowPackageMessageIfEnabled()
        {
            if (!ShowPackageMessages)
            {
                return;
            }

            bool keepShowing = EditorUtility.DisplayDialog(
                PackageStartupMessageTitle,
                PackageStartupMessage,
                "OK",
                "Don't Show Again");

            if (!keepShowing)
            {
                ShowPackageMessages = false;
            }

            EditorApplication.delayCall += SelectPackageReadmeAsset;
        }

        private static void SelectPackageReadmeAsset()
        {
            FPReadmeAsset readme = LoadPackageReadmeAsset();
            if (readme == null)
            {
                Debug.LogWarning($"FP Utility could not find the package {UtilityReadmeAssetName} FP Readme Asset.");
                return;
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = readme;
            EditorGUIUtility.PingObject(readme);
        }

        private static FPReadmeAsset LoadPackageReadmeAsset()
        {
            if (TryGetUtilityPackageRootPath(out string packageRootPath))
            {
                FPReadmeAsset readme = LoadReadmeAtPackageRoot(packageRootPath);
                if (readme != null)
                {
                    return readme;
                }
            }

            return LoadReadmeAtPackageRoot(UtilityPackagePath.TrimEnd('/'));
        }

        private static FPReadmeAsset LoadReadmeAtPackageRoot(string packageRootPath)
        {
            if (string.IsNullOrWhiteSpace(packageRootPath) || !AssetDatabase.IsValidFolder(packageRootPath))
            {
                return null;
            }

            string directReadmePath = $"{packageRootPath}/{UtilityReadmeAssetName}.asset";
            FPReadmeAsset directReadme = AssetDatabase.LoadAssetAtPath<FPReadmeAsset>(directReadmePath);
            if (directReadme != null)
            {
                return directReadme;
            }

            string[] readmeGuids = AssetDatabase.FindAssets(UtilityReadmeAssetName, new[] { packageRootPath });
            foreach (string guid in readmeGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                FPReadmeAsset readme = AssetDatabase.LoadAssetAtPath<FPReadmeAsset>(assetPath);
                if (readme != null && readme.name == UtilityReadmeAssetName)
                {
                    return readme;
                }
            }

            return null;
        }
    }
}
