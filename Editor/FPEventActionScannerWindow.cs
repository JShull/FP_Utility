namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    using System.IO;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Linq;
    using System.Diagnostics;
    /// <summary>
    /// This is an editor window that is internal use to help on the package work itself.
    /// It won't really work at the moment as a package, but useful to have as a tool building the packages
    /// </summary>
    public class FPEventActionScannerWindow:EditorWindow
    {
        private Vector2 scrollPos;
        private List<(string filePath, int lineNumber, string matchLine)> results = new();
        
        //private string allTextResults = string.Empty;
        private static readonly Regex eventPattern = new(@"\b(event|delegate|Action<?.*?>?)\b.*;", RegexOptions.Compiled);

        private Dictionary<string, bool> packageFilters = new();
        private HashSet<string> hitPackages = new();
        private enum EditorTarget { Default, VisualStudio, VSCode, JetBrainsRider }
        private EditorTarget selectedEditor = EditorTarget.Default;

        [MenuItem("FuzzPhyte/Utility/Editor/Action-Event Scanner",priority = FP_UtilityData.ORDER_MENU,secondaryPriority = FP_UtilityData.ORDER_SUBMENU_LVL6)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPEventActionScannerWindow>("FP Action-Event Scanner");
            window.minSize = new Vector2(600, 400);
            //window.PreparePackageFilters();
            //window.ScanProject();

        }
        private void OnEnable()
        {
            PreparePackageFilters();
            ScanProject();
        }
        private void OnGUI()
        {
            GUILayout.Label("Filter by Package", EditorStyles.boldLabel);

           

            GUILayout.Space(5);
            selectedEditor = (EditorTarget)EditorGUILayout.EnumPopup("Open File With", selectedEditor);

            int columns = 4;
            int count = 0;
            float columnWidth = position.width / columns - 10f;

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            var keysData = packageFilters.Keys.ToList();
            for(int i=0;i<keysData.Count; i++)
            {
                var key = keysData[i];
                if (count > 0 && count % columns == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                var style = new GUIStyle();
                style.normal.textColor = hitPackages.Contains(key) ? Color.green : GUI.skin.label.normal.textColor;
                packageFilters[key] = EditorGUILayout.ToggleLeft(key, packageFilters[key], style, GUILayout.Width(columnWidth));
                count++;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All")) SetAllPackageFilters(true);
            if (GUILayout.Button("Deselect All")) SetAllPackageFilters(false);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);
            if (GUILayout.Button("Rescan Project")) ScanProject();
            if (GUILayout.Button("Export Results to File")) ExportResults();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Results:", EditorStyles.label);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            //var grouped = results.GroupBy(r => r.filePath);
            var grouped = results
                .GroupBy(r => GetTopLevelFolder(r.filePath))
                .OrderBy(g => g.Key)
                .SelectMany(g => g.OrderBy(r => r.filePath).GroupBy(r => r.filePath));
            string currentPackage = null;
            foreach (var group in grouped)
            {
                string package = GetTopLevelFolder(group.Key);
                if (package != currentPackage)
                {
                    GUILayout.Space(5);
                    EditorGUILayout.LabelField($"Package: {package}", EditorStyles.boldLabel);
                    currentPackage = package;
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(group.Key, EditorStyles.linkLabel))
                {
                    string fullPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - 6), group.Key);
                    LaunchEditor(fullPath);
                }
                EditorGUILayout.EndHorizontal();

                foreach (var match in group)
                {
                    EditorGUILayout.LabelField($"  Line {match.lineNumber + 1}: {match.matchLine}");
                }
                GUILayout.Space(10);
            }

            EditorGUILayout.EndScrollView();
        }
        private void LaunchEditor(string fullPath)
        {
            switch (selectedEditor)
            {
                case EditorTarget.VisualStudio:
                    Process.Start(new ProcessStartInfo("devenv.exe", fullPath) { UseShellExecute = true });
                    break;
                case EditorTarget.VSCode:
                    Process.Start(new ProcessStartInfo("code", fullPath) { UseShellExecute = true });
                    break;
                case EditorTarget.JetBrainsRider:
                    Process.Start(new ProcessStartInfo("rider64.exe", fullPath) { UseShellExecute = true });
                    break;
                default:
                    Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
                    break;
            }
        }
        private void SetAllPackageFilters(bool state)
        {
            var keys = packageFilters.Keys.ToList();
            foreach (var key in keys)
            {
                packageFilters[key] = state;
            }
        }
        private void PreparePackageFilters()
        {
            packageFilters.Clear();
            
            string[] directories = Directory.GetDirectories(Application.dataPath);

            foreach (string dir in directories)
            {
                string folderName = Path.GetFileName(dir);
                if (folderName.StartsWith("FP_"))
                {
                    packageFilters[folderName] = true;
                }
            }
        }
        private void ScanProject()
        {
            results.Clear();
            //allTextResults = string.Empty;
            hitPackages.Clear();

            string[] allCSFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            foreach (string filePath in allCSFiles)
            {
                string relativePath = filePath.Replace("\\", "/");
                string folderName = GetTopLevelFolder(relativePath);

                if (!packageFilters.TryGetValue(folderName, out bool isEnabled) || !isEnabled)
                    continue;

                var lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (eventPattern.IsMatch(lines[i]))
                    {
                        string assetRelativePath = "Assets" + filePath.Replace(Application.dataPath, "").Replace("\\", "/");
                        results.Add((assetRelativePath, i, lines[i].Trim()));
                        hitPackages.Add(folderName);
                    }
                }
            }

            UnityEngine.Debug.Log($"FP_EventScanWindow: Found {results.Count} event/delegate/action uses.");
        }

        private string GetTopLevelFolder(string path)
        {
            string relative = path.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            string[] parts = relative.Split('/');
            return parts.Length > 1 ? parts[1] : "";
        }

        private void ExportResults()
        {
            string exportPath = EditorUtility.SaveFilePanel("Export Event Results", Application.dataPath, "FP_EventResults.md", "md");
            if (string.IsNullOrEmpty(exportPath)) return;

            using StreamWriter sw = new(exportPath);
            sw.Write("# FuzzPhyte Event Results");
            sw.WriteLine("\n");
            sw.Write(FormatResultsMarkdownNew());

            UnityEngine.Debug.Log($"FP_EventScanWindow: Results exported to {exportPath}");
            AssetDatabase.Refresh();
        }
        
        private string FormatResultsMarkdown()
        {
            var grouped = results
                .GroupBy(r => GetTopLevelFolder(r.filePath))
                .OrderBy(g => g.Key);

            var lines = new List<string>();

            foreach (var packageGroup in grouped)
            {
                lines.Add($"## Package: {packageGroup.Key}");

                var scriptGroups = packageGroup
                    .OrderBy(r => r.filePath)
                    .GroupBy(r => r.filePath);

                foreach (var scriptGroup in scriptGroups)
                {
                    lines.Add($"* {scriptGroup.Key}");
                    foreach (var match in scriptGroup)
                    {
                        lines.Add($"  * Line {match.lineNumber + 1}: {match.matchLine}");
                    }
                    lines.Add(""); // spacing
                }
            }

            return string.Join("\n", lines);
        }
        private string FormatResultsMarkdownNew()
        {
            string rootPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6); // trims /Assets

            var grouped = results
                .GroupBy(r => GetTopLevelFolder(r.filePath))
                .OrderBy(g => g.Key);

            var lines = new List<string>();
            string mkdFilePrefix = "file:///";
            foreach (var packageGroup in grouped)
            {
                lines.Add($"## Package: {packageGroup.Key}");
                lines.Add(""); // spacing

                var fileGroups = packageGroup
                    .GroupBy(r => r.filePath)
                    .OrderBy(g => g.Key);

                foreach (var fileGroup in fileGroups)
                {
                    string fullPath = Path.Combine(rootPath, fileGroup.Key).Replace("\\", "/");
                    string fileName = Path.GetFileName(fileGroup.Key);

                    lines.Add($"* [{fileName}]({mkdFilePrefix}{fullPath})");
                    foreach (var match in fileGroup)
                    {
                        lines.Add($"  * Line {match.lineNumber + 1}: {match.matchLine}");
                    }
                    lines.Add(""); // spacing
                }
            }

            return string.Join("\n", lines);
        }

    }
}
