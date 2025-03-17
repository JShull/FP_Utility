namespace FuzzPhyte.Utility.Editor
{
    using System.Collections;
    using System.IO;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;
    /// <summary>
    /// Class used for activating refresh ability for compilation - "reloading scripts"
    /// </summary>
    public class FPAutoReloadToggleWindow:EditorWindow
    {
        protected const string AutoRefreshKey = "FuzzPhyte.Utility.Editor.AutoScriptReload";
        private Texture enabledIcon;
        private Texture disabledIcon;
        private bool loadedTextures;

        [MenuItem("FuzzPhyte/Utility/Auto Script Reload Toggle", priority = 2)]
        public static void ShowWindow()
        {
            GetWindow<FPAutoReloadToggleWindow>("FP Reload");
        }
        
        private void LoadTextures()
        {
            var loadedPackageManager = FP_Utility_Editor.IsPackageLoadedViaPackageManager();
            //Debug.LogWarning($"FP_HHeader: via Unity Package Manager: {loadedPackageManager}");
            var packageName = loadedPackageManager ? "utility" : "FP_Utility";
            var packageRef = FP_Utility_Editor.ReturnEditorPath(packageName, !loadedPackageManager);
            var iconRefEditor = FP_Utility_Editor.ReturnEditorResourceIcons(packageRef);
            Debug.LogWarning($"iconRefEditor = {iconRefEditor}");
            Debug.LogWarning($"packageRef = {packageRef}");
            //ICON LOAD
            var enabledIconPath = Path.Combine(iconRefEditor, "HH_SelectAllActive.png");
            var disabledIconPath = Path.Combine(iconRefEditor, "HH_SelectAll.png");

            disabledIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(enabledIconPath);
            enabledIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(disabledIconPath);
        }

        private void OnGUI()
        {
            if (!loadedTextures)
            {
                LoadTextures();
                loadedTextures = true;
            }
            GUILayout.Label("FP Utility Script Reload", EditorStyles.boldLabel);

            bool autoReloadEnabled = AutoReload;

            GUILayout.Label($"Current Status: {(autoReloadEnabled ? "Enabled" : "Disabled")}", EditorStyles.helpBox);
            Color lineColor = autoReloadEnabled ? FP_Utility_Editor.OkayColor : FP_Utility_Editor.WarningColor;
            FP_Utility_Editor.DrawUILine(lineColor);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            Texture icon = autoReloadEnabled
                ? enabledIcon
                : disabledIcon;
            Texture refreshIcon = EditorGUIUtility.IconContent("Refresh").image;
            string toggleText = autoReloadEnabled ? "Disable Auto Reload" : "Enable Auto Reload";
            string textDisplay = autoReloadEnabled ? "Enabled" : "Disabled";
            //get width of our current window
            var windowWidth = position.width;
            bool smallWindow = windowWidth < 200;
            //divide our windowWidth by 2 to get the max width, I then want the button to be like 25% of that maxwidth
            var buttonWidth = windowWidth / 4;
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                border = new RectOffset(0, 0, 0, 0),
                fixedHeight = 40f,
                fixedWidth = buttonWidth,
                fontSize = smallWindow?6:10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = FP_Utility_Editor.TextMenuColor },
                hover = { textColor = autoReloadEnabled?FP_Utility_Editor.WarningColor: FP_Utility_Editor.TextHoverColor },
                active = { textColor = FP_Utility_Editor.TextActiveColor }
            };
            
            var buttonReloadStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                border = new RectOffset(0, 0, 0, 0),
                fixedHeight = 40f,
                fixedWidth = buttonWidth,
                fontSize = smallWindow?6:10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = FP_Utility_Editor.TextMenuColor },
                hover = { textColor = FP_Utility_Editor.TextHoverColor },
                active = { textColor = FP_Utility_Editor.TextActiveColor }
            };
            if (GUILayout.Button(new GUIContent(textDisplay, icon, toggleText), buttonStyle))
            {
                AutoReload = !AutoReload;
            }
            
            //Debug.Log("RELOAD");
            if (GUILayout.Button(new GUIContent("Reload", refreshIcon, "Refresh?"), buttonReloadStyle))
            {
                ManualRefresh();
            }
            EditorGUILayout.EndHorizontal();
        }

        private bool AutoReload
        {
            get => EditorPrefs.GetBool("kAutoRefresh", true);
            set
            {
                EditorPrefs.SetBool(AutoRefreshKey, value);
                EditorPrefs.SetBool("kAutoRefresh", value);
                EditorPrefs.SetInt("ScriptCompilationDuringPlay", value ? 1 : 0);
                // Explicitly set Unity's Asset Pipeline Auto Refresh
                EditorPrefs.SetInt("kAutoRefreshMode", value ? 1 : 0);

                if (!value)
                {
                    EditorApplication.LockReloadAssemblies();
                    Debug.Log("FP Auto script reload: disabled.");
                }
                else
                {
                    EditorApplication.UnlockReloadAssemblies();
                    AssetDatabase.Refresh();
                    Debug.Log("FP Auto script reload: enabled and assets refreshed.");
                }
            }
        }
        /*
        private void SetAutoRefresh(bool enable)
        {
            EditorPrefs.SetBool(AutoRefreshKey, enable);
            EditorPrefs.SetBool("kAutoRefresh", enable);
           
            if (!enable)
            {
                EditorApplication.LockReloadAssemblies();
                Debug.Log("FP Auto script reload: disabled.");
            }
            else
            {
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();
                Debug.Log("FP Auto script reload: enabled and assets refreshed.");
            }
        }
        */
        private void ManualRefresh()
        {
            bool wasLocked = !EditorPrefs.GetBool("kAutoRefresh");

            if (wasLocked)
            {
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();

                EditorApplication.delayCall += () =>
                {
                    EditorApplication.LockReloadAssemblies();
                    Debug.Log("Manual refresh complete. Assemblies re-locked.");
                };

                Debug.Log("Manual script refresh and assembly reload triggered temporarily.");
            }
            else
            {
                AssetDatabase.Refresh();
                Debug.Log("Manual script refresh triggered.");
            }
        }
        /*
        private void RelockAssemblies()
        {
            EditorApplication.LockReloadAssemblies();
            Debug.Log("Assemblies re-locked after manual refresh.");
            EditorApplication.update -= RelockAfterCompile;
        }

        private void RelockAfterCompile()
        {
            // Wait until scripts finish compiling
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                RelockAssemblies();
            }
        }
        */

    }
}
