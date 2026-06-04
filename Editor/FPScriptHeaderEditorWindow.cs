// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Applies a shared comment header to selected C# script assets.
    /// </summary>
    public class FPScriptHeaderEditorWindow : EditorWindow
    {
        [Serializable]
        private class HeaderChangeRecord
        {
            public string AssetPath;
            public string FullPath;
            public string BackupPath;
        }

        [Serializable]
        private class HeaderChangeOperation
        {
            public string Name;
            public string Timestamp;
            public List<HeaderChangeRecord> Records = new List<HeaderChangeRecord>();
        }

        [SerializeField]
        private string headerText =
            "// Copyright (c) 2026 John B. Shull\n" +
            "// FuzzPhyte LLC is a company associated with John B. Shull\n" +
            "// This file is part of FP_Utility Package.\n" +
            "//\n" +
            "// Public license: GNU GPLv3-or-later.\n" +
            "// Commercial/proprietary use requires a separate license from John B. Shull.\n" +
            "//\n" +
            "// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.";

        [SerializeField] private List<Object> scriptAssets = new List<Object>();
        [SerializeField] private List<HeaderChangeOperation> undoStack = new List<HeaderChangeOperation>();
        [SerializeField] private List<string> warnings = new List<string>();
        [SerializeField] private List<string> errors = new List<string>();
        [SerializeField] private List<string> infoMessages = new List<string>();
        [SerializeField] private Object pendingScript;
        [SerializeField] private bool replaceExistingHeader = true;
        [SerializeField] private bool skipUnchangedFiles = true;

        private Vector2 parameterScrollPosition;
        private Vector2 headerScrollPosition;
        private Vector2 messageScrollPosition;

        private const float ParameterPanelWidth = 352f;
        private const float BottomDebugHeight = 124f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private const float ActionPanelHeight = 112f;
        private const float DropAreaHeight = 48f;

        [MenuItem("FuzzPhyte/Utility/Editor/Script Header Editor", priority = FP_UtilityData.MENU_UTILITY_EDITOR + 32)]
        public static void ShowWindow()
        {
            FPScriptHeaderEditorWindow window = GetWindow<FPScriptHeaderEditorWindow>("Script Header Editor");
            window.minSize = new Vector2(760f, 520f);
            window.AddSelectedScripts(false);
        }

        private void OnGUI()
        {
            GUILayout.Label("Script Header Editor", EditorStyles.boldLabel);
            DrawWorkspace();
        }

        private void DrawWorkspace()
        {
            Rect previousRect = GUILayoutUtility.GetLastRect();
            float workspaceTop = previousRect.yMax + 4f;
            Rect workspaceRect = new Rect(
                WorkspacePadding,
                workspaceTop,
                Mathf.Max(100f, position.width - (WorkspacePadding * 2f)),
                Mathf.Max(100f, position.height - workspaceTop - WorkspacePadding));

            float topHeight = Mathf.Max(180f, workspaceRect.height - BottomDebugHeight - PanelGap);
            Rect topRect = new Rect(workspaceRect.x, workspaceRect.y, workspaceRect.width, topHeight);
            Rect debugRect = new Rect(workspaceRect.x, topRect.yMax + PanelGap, workspaceRect.width, workspaceRect.height - topHeight - PanelGap);

            float leftWidth = Mathf.Clamp(ParameterPanelWidth, 260f, Mathf.Max(260f, topRect.width - 280f - PanelGap));
            Rect parameterRect = new Rect(topRect.x, topRect.y, leftWidth, topRect.height);
            Rect headerRect = new Rect(parameterRect.xMax + PanelGap, topRect.y, Mathf.Max(100f, topRect.xMax - parameterRect.xMax - PanelGap), topRect.height);

            DrawParameterPanelContainer(parameterRect);
            DrawHeaderPanelContainer(headerRect);
            DrawDebugPanel(debugRect);
        }

        private void DrawParameterPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect actionRect = new Rect(innerRect.x, innerRect.yMax - ActionPanelHeight, innerRect.width, ActionPanelHeight);
            Rect scrollRect = new Rect(innerRect.x, innerRect.y, innerRect.width, Mathf.Max(40f, innerRect.height - ActionPanelHeight - 6f));
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, Mathf.Max(scrollRect.height, 270f + (scriptAssets.Count * 24f)));

            parameterScrollPosition = GUI.BeginScrollView(scrollRect, parameterScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawParameterPanel();
            GUILayout.EndArea();
            GUI.EndScrollView();

            GUILayout.BeginArea(actionRect);
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawActions();
            GUILayout.EndArea();
        }

        private void DrawParameterPanel()
        {
            EditorGUILayout.LabelField("Source Scripts", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                pendingScript = EditorGUILayout.ObjectField(pendingScript, typeof(MonoScript), false);
                if (GUILayout.Button("Add", GUILayout.Width(52f)))
                {
                    AddScriptObject(pendingScript, true);
                    pendingScript = null;
                }
            }

            DrawScriptDropArea();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selection"))
                {
                    AddSelectedScripts(true);
                }

                if (GUILayout.Button("Clean List"))
                {
                    RemoveInvalidScripts(true);
                }
            }

            if (GUILayout.Button("Clear Scripts"))
            {
                RecordWindowUndo("Clear Script Header List");
                scriptAssets.Clear();
                AddInfo("Cleared script list.");
            }

            FPMeshPreviewEditorUtility.DrawSectionDivider();

            replaceExistingHeader = EditorGUILayout.Toggle("Replace Existing Header", replaceExistingHeader);
            skipUnchangedFiles = EditorGUILayout.Toggle("Skip Unchanged Files", skipUnchangedFiles);

            EditorGUILayout.HelpBox("Existing headers are detected as leading line comments or block comments before the first code line.", MessageType.None);

            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawScriptList();
        }

        private void DrawScriptDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop C# scripts here", EditorStyles.helpBox);

            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    AddScriptObjects(DragAndDrop.objectReferences, true);
                }

                evt.Use();
            }
        }

        private void DrawScriptList()
        {
            EditorGUILayout.LabelField($"Scripts ({GetValidScriptCount()})", EditorStyles.boldLabel);

            if (scriptAssets.Count == 0)
            {
                EditorGUILayout.HelpBox("Add MonoScript assets from this package or any other package/project folder.", MessageType.None);
                return;
            }

            for (int i = 0; i < scriptAssets.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    Object next = EditorGUILayout.ObjectField(scriptAssets[i], typeof(MonoScript), false);
                    if (next != scriptAssets[i])
                    {
                        RecordWindowUndo("Edit Script Header List");
                        scriptAssets[i] = next;
                    }

                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        RecordWindowUndo("Remove Script Header Target");
                        scriptAssets.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        private void DrawHeaderPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);

            GUILayout.BeginArea(innerRect);
            EditorGUILayout.LabelField("Header Text", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This text is written at the top of each selected .cs file.", MessageType.None);

            Rect headerRect = GUILayoutUtility.GetRect(1f, Mathf.Max(80f, innerRect.height - 74f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, headerRect.width - 16f), Mathf.Max(headerRect.height, MeasureHeaderTextHeight(headerRect.width - 24f)));
            headerScrollPosition = GUI.BeginScrollView(headerRect, headerScrollPosition, viewRect);

            EditorGUI.BeginChangeCheck();
            string nextHeader = EditorGUI.TextArea(new Rect(0f, 0f, viewRect.width, viewRect.height), headerText, EditorStyles.textArea);
            if (EditorGUI.EndChangeCheck())
            {
                RecordWindowUndo("Edit Script Header Text");
                headerText = nextHeader;
            }

            GUI.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = GetValidScriptCount() > 0 && !string.IsNullOrWhiteSpace(headerText);
                if (GUILayout.Button("Apply Headers", GUILayout.Height(32f)))
                {
                    ApplyHeaders();
                }

                GUI.enabled = undoStack.Count > 0;
                if (GUILayout.Button("Undo Last Apply", GUILayout.Height(32f)))
                {
                    UndoLastApply();
                }

                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Backup Folder"))
                {
                    string backupRoot = GetBackupRoot();
                    Directory.CreateDirectory(backupRoot);
                    EditorUtility.RevealInFinder(backupRoot);
                }

                if (GUILayout.Button("Clear Messages"))
                {
                    ClearMessages();
                }
            }
        }

        private void DrawDebugPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            int messageCount = Mathf.Max(1, errors.Count + warnings.Count + infoMessages.Count);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, Mathf.Max(innerRect.height, 36f + (messageCount * 38f)));

            messageScrollPosition = GUI.BeginScrollView(innerRect, messageScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawMessages();
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawMessages()
        {
            EditorGUILayout.LabelField("Messages", EditorStyles.boldLabel);

            if (errors.Count == 0 && warnings.Count == 0 && infoMessages.Count == 0)
            {
                EditorGUILayout.HelpBox("No script header messages.", MessageType.None);
                return;
            }

            for (int i = 0; i < errors.Count; i++)
            {
                EditorGUILayout.HelpBox(errors[i], MessageType.Error);
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
            }

            for (int i = 0; i < infoMessages.Count; i++)
            {
                EditorGUILayout.HelpBox(infoMessages[i], MessageType.Info);
            }
        }

        private void ApplyHeaders()
        {
            ClearMessages();
            string normalizedHeader = NormalizeHeader(headerText);
            if (string.IsNullOrWhiteSpace(normalizedHeader))
            {
                errors.Add("Header text is empty.");
                return;
            }

            List<ScriptTarget> targets = BuildValidTargets();
            if (targets.Count == 0)
            {
                errors.Add("No valid C# scripts were selected.");
                return;
            }

            string backupFolder = CreateBackupFolder();
            HeaderChangeOperation operation = new HeaderChangeOperation
            {
                Name = "Apply Script Headers",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    ApplyHeaderToTarget(targets[i], normalizedHeader, backupFolder, operation);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (operation.Records.Count > 0)
            {
                RecordWindowUndo("Apply Script Headers");
                undoStack.Add(operation);
                AddInfo($"Updated {operation.Records.Count} script file(s). Backup: {backupFolder}");
                AssetDatabase.Refresh();
            }
            else if (errors.Count == 0)
            {
                AddInfo("No script files needed header changes.");
            }
        }

        private void ApplyHeaderToTarget(ScriptTarget target, string normalizedHeader, string backupFolder, HeaderChangeOperation operation)
        {
            if (!File.Exists(target.FullPath))
            {
                warnings.Add($"Skipped missing file: {target.AssetPath}");
                return;
            }

            string originalText = ReadText(target.FullPath, out Encoding encoding);
            string nextText = BuildHeaderContent(originalText, normalizedHeader, replaceExistingHeader);
            if (skipUnchangedFiles && string.Equals(originalText, nextText, StringComparison.Ordinal))
            {
                return;
            }

            string backupPath = Path.Combine(backupFolder, MakeSafeBackupName(operation.Records.Count, target.AssetPath));
            File.Copy(target.FullPath, backupPath, true);
            File.WriteAllText(target.FullPath, nextText, encoding);

            operation.Records.Add(new HeaderChangeRecord
            {
                AssetPath = target.AssetPath,
                FullPath = target.FullPath,
                BackupPath = backupPath
            });
        }

        private void UndoLastApply()
        {
            ClearMessages();
            if (undoStack.Count == 0)
            {
                warnings.Add("There is no header operation to undo.");
                return;
            }

            HeaderChangeOperation operation = undoStack[undoStack.Count - 1];
            int restoredCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < operation.Records.Count; i++)
                {
                    HeaderChangeRecord record = operation.Records[i];
                    if (!File.Exists(record.BackupPath))
                    {
                        warnings.Add($"Backup is missing for: {record.AssetPath}");
                        continue;
                    }

                    string folder = Path.GetDirectoryName(record.FullPath);
                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }

                    File.Copy(record.BackupPath, record.FullPath, true);
                    restoredCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            RecordWindowUndo("Undo Script Headers");
            undoStack.RemoveAt(undoStack.Count - 1);
            AddInfo($"Restored {restoredCount} script file(s) from {operation.Timestamp}.");
            AssetDatabase.Refresh();
        }

        private List<ScriptTarget> BuildValidTargets()
        {
            List<ScriptTarget> targets = new List<ScriptTarget>();
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < scriptAssets.Count; i++)
            {
                Object script = scriptAssets[i];
                if (!TryGetScriptTarget(script, out ScriptTarget target))
                {
                    if (script != null)
                    {
                        warnings.Add($"Skipped non-C# asset: {script.name}");
                    }

                    continue;
                }

                if (seenPaths.Add(target.AssetPath))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private void AddSelectedScripts(bool recordUndo)
        {
            AddScriptObjects(Selection.objects, recordUndo);
        }

        private void AddScriptObjects(Object[] objects, bool recordUndo)
        {
            if (objects == null || objects.Length == 0)
            {
                return;
            }

            bool changed = false;
            if (recordUndo)
            {
                RecordWindowUndo("Add Script Header Targets");
            }

            for (int i = 0; i < objects.Length; i++)
            {
                changed |= AddScriptObject(objects[i], false);
            }

            if (changed)
            {
                AddInfo("Added selected C# script asset(s).");
            }
        }

        private bool AddScriptObject(Object obj, bool recordUndo)
        {
            if (!TryGetScriptTarget(obj, out ScriptTarget target))
            {
                if (obj != null)
                {
                    warnings.Add($"Only C# MonoScript assets can be added: {obj.name}");
                }

                return false;
            }

            for (int i = 0; i < scriptAssets.Count; i++)
            {
                if (!TryGetScriptTarget(scriptAssets[i], out ScriptTarget existing))
                {
                    continue;
                }

                if (string.Equals(existing.AssetPath, target.AssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (recordUndo)
            {
                RecordWindowUndo("Add Script Header Target");
            }

            scriptAssets.Add(obj);
            return true;
        }

        private void RemoveInvalidScripts(bool recordUndo)
        {
            bool changed = false;
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (recordUndo)
            {
                RecordWindowUndo("Clean Script Header List");
            }

            for (int i = scriptAssets.Count - 1; i >= 0; i--)
            {
                if (!TryGetScriptTarget(scriptAssets[i], out ScriptTarget target) || !seenPaths.Add(target.AssetPath))
                {
                    scriptAssets.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                AddInfo("Removed invalid or duplicate script entries.");
            }
        }

        private int GetValidScriptCount()
        {
            int count = 0;
            HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < scriptAssets.Count; i++)
            {
                if (TryGetScriptTarget(scriptAssets[i], out ScriptTarget target) && seenPaths.Add(target.AssetPath))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetScriptTarget(Object obj, out ScriptTarget target)
        {
            target = default(ScriptTarget);
            if (obj == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string fullPath = GetFullProjectPath(assetPath);
            target = new ScriptTarget(assetPath, fullPath);
            return true;
        }

        private static string BuildHeaderContent(string originalText, string normalizedHeader, bool replaceHeader)
        {
            string normalizedOriginal = NormalizeLineEndings(originalText);
            if (normalizedOriginal.StartsWith("\uFEFF", StringComparison.Ordinal))
            {
                normalizedOriginal = normalizedOriginal.Substring(1);
            }

            string body = replaceHeader
                ? normalizedOriginal.Substring(FindBodyStart(normalizedOriginal))
                : normalizedOriginal.TrimStart('\n');

            body = body.TrimStart('\n');
            string nextText = string.IsNullOrEmpty(body)
                ? normalizedHeader + "\n"
                : normalizedHeader + "\n\n" + body;

            return nextText.Replace("\n", DetectLineEnding(originalText));
        }

        private static int FindBodyStart(string text)
        {
            int index = 0;

            while (index < text.Length)
            {
                int lineStart = index;
                int lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd < 0)
                {
                    lineEnd = text.Length;
                }

                string line = text.Substring(lineStart, lineEnd - lineStart);
                string trimmedLine = line.TrimStart();
                int nextLineStart = lineEnd < text.Length ? lineEnd + 1 : lineEnd;

                if (string.IsNullOrWhiteSpace(line) || trimmedLine.StartsWith("//", StringComparison.Ordinal))
                {
                    index = nextLineStart;
                    continue;
                }

                if (trimmedLine.StartsWith("/*", StringComparison.Ordinal))
                {
                    int blockEnd = text.IndexOf("*/", lineStart, StringComparison.Ordinal);
                    if (blockEnd < 0)
                    {
                        return lineStart;
                    }

                    index = blockEnd + 2;
                    if (index < text.Length && text[index] == '\n')
                    {
                        index++;
                    }

                    continue;
                }

                break;
            }

            while (index < text.Length && text[index] == '\n')
            {
                index++;
            }

            return index;
        }

        private static string NormalizeHeader(string text)
        {
            return NormalizeLineEndings(text).Trim();
        }

        private static string NormalizeLineEndings(string text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string DetectLineEnding(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains("\r\n") ? "\r\n" : "\n";
        }

        private static string ReadText(string path, out Encoding encoding)
        {
            byte[] bytes = File.ReadAllBytes(path);
            encoding = DetectEncoding(bytes);
            string text = encoding.GetString(bytes);
            return text.StartsWith("\uFEFF", StringComparison.Ordinal) ? text.Substring(1) : text;
        }

        private static Encoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new UTF8Encoding(true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode;
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }

            return new UTF8Encoding(false);
        }

        private static string GetFullProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static string GetBackupRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Library", "FP_Utility", "HeaderEditorBackups");
        }

        private static string CreateBackupFolder()
        {
            string backupFolder = Path.Combine(GetBackupRoot(), DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backupFolder);
            return backupFolder;
        }

        private static string MakeSafeBackupName(int index, string assetPath)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string safePath = assetPath.Replace('/', '_').Replace('\\', '_');
            for (int i = 0; i < invalidChars.Length; i++)
            {
                safePath = safePath.Replace(invalidChars[i], '_');
            }

            return $"{index:0000}_{safePath}.bak";
        }

        private float MeasureHeaderTextHeight(float width)
        {
            string safeText = string.IsNullOrEmpty(headerText) ? " " : headerText;
            GUIContent content = new GUIContent(safeText);
            return Mathf.Max(180f, EditorStyles.textArea.CalcHeight(content, Mathf.Max(50f, width)) + 16f);
        }

        private void RecordWindowUndo(string actionName)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(actionName);
            Undo.RecordObject(this, actionName);
            EditorUtility.SetDirty(this);
        }

        private void AddInfo(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                infoMessages.Add(message);
            }
        }

        private void ClearMessages()
        {
            errors.Clear();
            warnings.Clear();
            infoMessages.Clear();
        }

        private struct ScriptTarget
        {
            public readonly string AssetPath;
            public readonly string FullPath;

            public ScriptTarget(string assetPath, string fullPath)
            {
                AssetPath = assetPath;
                FullPath = fullPath;
            }
        }
    }
}
