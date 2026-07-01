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
        private const string StartupMessageIconRelativePath = "Editor/Gizmos/FP_BanditWorks.png";
        private const string SessionCheckKey = "FuzzPhyte.Utility.Editor.FPUtilitySessionCheck.HasRun";
        private const string ShowPackageMessagesEditorPrefsKey = "FuzzPhyte.Utility.Editor.FPUtilitySessionCheck.ShowPackageMessages";
        private const string PackageStartupMessageTitle = "FP Utility";
        private const string PackageStartupMessage = "FP Utility is checking for the latest Git package update for this Unity project.\n\nThanks for using this package!";

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

            Texture2D startupIcon = LoadStartupMessageIcon();
            if (startupIcon != null)
            {
                FPUtilityStartupMessageWindow.Show(
                    PackageStartupMessageTitle,
                    PackageStartupMessage,
                    startupIcon,
                    CompletePackageMessage);
                return;
            }

            bool keepShowing = EditorUtility.DisplayDialog(
                PackageStartupMessageTitle,
                PackageStartupMessage,
                "OK",
                "Don't Show Again");
            CompletePackageMessage(keepShowing);
        }

        private static void CompletePackageMessage(bool keepShowing)
        {
            if (!keepShowing)
            {
                ShowPackageMessages = false;
            }

            EditorApplication.delayCall += SelectPackageReadmeAsset;
        }

        private static Texture2D LoadStartupMessageIcon()
        {
            if (TryGetUtilityPackageRootPath(out string packageRootPath))
            {
                Texture2D icon = LoadStartupMessageIconAtPackageRoot(packageRootPath);
                if (icon != null)
                {
                    return icon;
                }
            }

            return LoadStartupMessageIconAtPackageRoot(UtilityPackagePath.TrimEnd('/'));
        }

        private static Texture2D LoadStartupMessageIconAtPackageRoot(string packageRootPath)
        {
            if (string.IsNullOrWhiteSpace(packageRootPath))
            {
                return null;
            }

            string iconPath = $"{packageRootPath}/{StartupMessageIconRelativePath}";
            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
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

        private sealed class FPUtilityStartupMessageWindow : EditorWindow
        {
            private const float IconSize = 84f;
            private const float WindowWidth = 440f;
            private const float WindowHeight = 220f;

            private string messageTitle;
            private string messageBody;
            private Texture2D icon;
            private Action<bool> onComplete;
            private bool completed;

            public static void Show(
                string title,
                string message,
                Texture2D messageIcon,
                Action<bool> completion)
            {
                var window = CreateInstance<FPUtilityStartupMessageWindow>();
                window.titleContent = new GUIContent(title, messageIcon);
                window.messageTitle = title;
                window.messageBody = message;
                window.icon = messageIcon;
                window.onComplete = completion;
                window.minSize = new Vector2(WindowWidth, WindowHeight);
                window.maxSize = new Vector2(WindowWidth, WindowHeight);
                window.position = GetCenteredPosition(WindowWidth, WindowHeight);
                window.ShowModalUtility();
            }

            private void OnGUI()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(14f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(16f);
                        DrawIcon();
                        GUILayout.Space(14f);
                        DrawMessage();
                        GUILayout.Space(16f);
                    }

                    GUILayout.FlexibleSpace();
                    DrawDivider();
                    DrawButtons();
                    GUILayout.Space(12f);
                }
            }

            private void OnDestroy()
            {
                if (!completed)
                {
                    Complete(true);
                }
            }

            private void DrawIcon()
            {
                Rect iconRect = GUILayoutUtility.GetRect(
                    IconSize,
                    IconSize,
                    GUILayout.Width(IconSize),
                    GUILayout.Height(IconSize));

                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                }
            }

            private void DrawMessage()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(2f);

                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 16,
                        wordWrap = true
                    };
                    EditorGUILayout.LabelField(messageTitle, titleStyle);
                    GUILayout.Space(4f);

                    var messageStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                    {
                        wordWrap = true
                    };
                    EditorGUILayout.LabelField(messageBody, messageStyle);
                }
            }

            private static void DrawDivider()
            {
                Rect rect = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
                rect.x += 16f;
                rect.width = Mathf.Max(1f, rect.width - 32f);
                EditorGUI.DrawRect(rect, FP_Utility_Editor.WarningColor);
                GUILayout.Space(10f);
            }

            private void DrawButtons()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Don't Show Again", GUILayout.Width(132f), GUILayout.Height(24f)))
                    {
                        Complete(false);
                    }

                    if (GUILayout.Button("OK", GUILayout.Width(86f), GUILayout.Height(24f)))
                    {
                        Complete(true);
                    }

                    GUILayout.Space(16f);
                }
            }

            private void Complete(bool keepShowing)
            {
                if (completed)
                {
                    return;
                }

                completed = true;
                onComplete?.Invoke(keepShowing);
                Close();
            }

            private static Rect GetCenteredPosition(float width, float height)
            {
                Rect mainWindowPosition = EditorGUIUtility.GetMainWindowPosition();
                return new Rect(
                    mainWindowPosition.x + ((mainWindowPosition.width - width) * 0.5f),
                    mainWindowPosition.y + ((mainWindowPosition.height - height) * 0.5f),
                    width,
                    height);
            }
        }
    }
}
