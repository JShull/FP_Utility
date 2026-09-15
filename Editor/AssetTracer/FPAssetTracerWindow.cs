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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    /// <summary>Reverse dependency search with explicit, reviewed override cleanup.</summary>
    public sealed class FPAssetTracerWindow : EditorWindow
    {
        [SerializeField] private Object target;
        [SerializeField] private bool includePackages;
        [SerializeField] private bool includeIndirect = true;
        [SerializeField] private string filter = "";
        private readonly Dictionary<string, List<string>> users = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> nextHop = new Dictionary<string, string>();
        private readonly List<string> results = new List<string>();
        private readonly List<string> errors = new List<string>();
        private string[] candidates;
        private string searchedPath;
        private int cursor;
        private int page;
        private bool scanning;
        private Vector2 scroll;
        private string status = "Choose an asset, then search.";
        private string details = "";
        private const int PageSize = 40;
        private readonly List<FPAssetTracerCleanup.Review> reviews = new List<FPAssetTracerCleanup.Review>();
        private bool complete;
        private string auditSummary = "";
        private string cleanupStatus = "";
        private Action confirmedAction;
        private string confirmationText;
        private bool refreshPending;
        private bool refreshAndAudit;
        private double refreshAfter;
        private string awaitingSave;

        [MenuItem("FuzzPhyte/Utility/Editor/Debug/Asset Tracer")]
        public static void Open()
        {
            var window = GetWindow<FPAssetTracerWindow>("Asset Tracer");
            window.minSize = new Vector2(640, 480);
            if (IsAsset(Selection.activeObject)) window.SetTarget(Selection.activeObject);
            window.Show();
        }

        [MenuItem("Assets/FuzzPhyte/Utility/Editor/Debug/Asset Tracer", false, 2000)]
        private static void OpenSelected() => Open();

        [MenuItem("Assets/FuzzPhyte/Utility/Editor/Debug/Asset Tracer", true)]
        private static bool ValidateSelection() => IsAsset(Selection.activeObject);

        private static bool IsAsset(Object value) => value != null &&
            !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value)) &&
            !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(value));

        private void OnEnable()
        {
            EditorApplication.projectChanged += ProjectChanged;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += SceneSaved;
        }
        private void OnDisable()
        {
            Stop();
            EditorApplication.projectChanged -= ProjectChanged;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= SceneSaved;
            EditorApplication.update -= RefreshWhenReady;
            confirmedAction = null;
        }

        private void ProjectChanged()
        {
            if (scanning) Stop();
            complete = false;
            reviews.Clear();
            auditSummary = "";
            confirmedAction = null;
            if (refreshAndAudit) QueueRefresh();
            status = "Project assets changed. Search again to refresh results.";
            Repaint();
        }

        private void SetTarget(Object value)
        {
            Stop();
            target = value;
            confirmedAction = null;
            awaitingSave = null;
            refreshPending = refreshAndAudit = false;
            EditorApplication.update -= RefreshWhenReady;
            complete = false;
            reviews.Clear();
            auditSummary = "";
            results.Clear();
            nextHop.Clear();
            users.Clear();
            details = "";
            page = 0;
            status = "Choose search options, then search.";
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Find assets that use this asset", EditorStyles.boldLabel);
                var value = EditorGUILayout.ObjectField("Target asset", target, typeof(Object), false);
                if (value != target) SetTarget(value);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!IsAsset(Selection.activeObject)))
                        if (GUILayout.Button("Use Selection")) SetTarget(Selection.activeObject);
                    using (new EditorGUI.DisabledScope(scanning))
                    {
                        bool packages = EditorGUILayout.ToggleLeft("Search Packages too", includePackages);
                        if (packages != includePackages) { includePackages = packages; SetTarget(target); }
                    }
                }
                EditorGUILayout.HelpBox("Searches saved asset dependencies. Direct = references this asset file; indirect = uses it through another asset. Sub-assets share a file, so use Details to check the exact object. Runtime string/address lookups and unsaved scene changes are not included.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!IsAsset(target) || scanning))
                        if (GUILayout.Button("Search References", GUILayout.Height(26))) StartSearch();
                    using (new EditorGUI.DisabledScope(!scanning))
                        if (GUILayout.Button("Cancel", GUILayout.Width(70)))
                        {
                            Stop();
                            status = "Cancelled. Results are incomplete; search again.";
                        }
                }
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
                if (scanning)
                {
                    var rect = GUILayoutUtility.GetRect(1, 18, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(rect, candidates.Length == 0 ? 0 : (float)cursor / candidates.Length,
                        $"{cursor:N0} / {candidates.Length:N0} assets");
                }
            }

            EditorGUI.BeginChangeCheck();
            includeIndirect = EditorGUILayout.ToggleLeft("Show indirect references", includeIndirect);
            filter = EditorGUILayout.TextField("Filter paths", filter);
            if (EditorGUI.EndChangeCheck()) page = 0;
            var visible = results.Where(p => (includeIndirect || nextHop[p] == searchedPath) &&
                p.IndexOf(filter ?? "", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            int pages = Math.Max(1, (visible.Count + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pages - 1);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("Previous")) page--;
                GUILayout.Label($"{visible.Count:N0} results — page {page + 1}/{pages}");
                using (new EditorGUI.DisabledScope(page + 1 == pages)) if (GUILayout.Button("Next")) page++;
                if (GUILayout.Button("Copy Paths")) EditorGUIUtility.systemCopyBuffer = string.Join("\n", visible);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (confirmedAction != null)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Confirm cleanup", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(confirmationText, EditorStyles.wordWrappedLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Cancel Cleanup"))
                        { confirmedAction = null; cleanupStatus = "Cleanup cancelled. No changes made."; }
                        if (GUILayout.Button("Confirm and Run Cleanup"))
                        {
                            var action = confirmedAction;
                            confirmedAction = null;
                            if (action != null) EditorApplication.delayCall += () => { if (this != null) action(); };
                        }
                    }
                }
            }
            using (new EditorGUI.DisabledScope(!complete || scanning))
                if (GUILayout.Button("Review Real Uses / Missing-Prefab Overrides"))
                    EditorApplication.delayCall += AuditReferences;
            if (!string.IsNullOrEmpty(auditSummary)) EditorGUILayout.HelpBox(auditSummary, MessageType.Info);
            if (!string.IsNullOrEmpty(cleanupStatus)) EditorGUILayout.HelpBox(cleanupStatus, MessageType.Info);
            foreach (var path in visible.Skip(page * PageSize).Take(PageSize))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(nextHop[path] == searchedPath ? "Direct" : "Indirect", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(path, EditorStyles.wordWrappedLabel, GUILayout.Height(34));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select / Ping"))
                        {
                            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                            EditorGUIUtility.PingObject(Selection.activeObject);
                        }
                        if (GUILayout.Button("Details")) details = Describe(path);
                    }
                    var review = reviews.FirstOrDefault(r => r.Path == path);
                    if (review != null)
                    {
                        EditorGUILayout.LabelField(review.OnlyOrphans ? "Missing-prefab overrides only" : "Other / unresolved use", EditorStyles.boldLabel);
                        if (GUILayout.Button("Review Cleanup Details")) details = FPAssetTracerCleanup.Describe(review);
                        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                        {
                            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!FPAssetTracerCleanup.IsLoaded(path))
                                {
                                    if (GUILayout.Button("Open Scene for Unity Override Cleanup"))
                                        EditorApplication.delayCall += () => RunCleanupAction(() =>
                                        {
                                            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive);
                                            return "Scene opened additively. Review it before running Unity override cleanup.";
                                        });
                                }
                                else if (GUILayout.Button("Unity: Remove Unused Overrides"))
                                {
                                    var selected = target;
                                    ConfirmCleanup(
                                        path + "\nUnity will remove ALL unused overrides on prefab roots whose overrides reference the selected asset. Valid overrides are retained. Review and save the scene explicitly; Undo is available.", () =>
                                    {
                                        RunCleanupAction(() =>
                                        {
                                            string message = FPAssetTracerCleanup.RemoveUnusedInLoadedScene(path, selected);
                                            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path).isDirty) awaitingSave = path;
                                            else QueueRefresh();
                                            return message;
                                        });
                                    });
                                }
                            }
                            using (new EditorGUI.DisabledScope(review.Orphans.Count == 0 || review.Note != null || FPAssetTracerCleanup.IsLoaded(path)))
                                if (GUILayout.Button($"Remove {review.Orphans.Count} Missing-Prefab Reference Override(s)…"))
                                    ConfirmCleanup(FPAssetTracerCleanup.Describe(review) + "\n\nOnly the listed references to this selected asset will be removed. Confirm these prefab sources were intentionally deleted. A file backup is created under Library/FP_Utility/AssetTracerCleanup; this file operation has no Undo.",
                                        () => RunCleanupAction(() => FPAssetTracerCleanup.RemoveOrphans(review), true));
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(details))
            {
                EditorGUILayout.LabelField("Reference details", EditorStyles.boldLabel);
                DrawReferenceDetails();
                if (GUILayout.Button("Copy Details")) EditorGUIUtility.systemCopyBuffer = details;
            }
            if (errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", errors.Take(8)), MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private void DrawReferenceDetails()
        {
            var normal = new GUIStyle(EditorStyles.label) { wordWrap = true };
            var owner = new GUIStyle(normal);
            owner.normal.textColor = FP_Utility_Editor.TextActiveColor;
            owner.focused.textColor = FP_Utility_Editor.TextActiveColor;
            using (new EditorGUILayout.VerticalScope(EditorStyles.textArea, GUILayout.MinHeight(130)))
            {
                foreach (string line in details.Split('\n'))
                {
                    bool isOwner = line.StartsWith("GameObject/owner:", StringComparison.Ordinal) ||
                        line.StartsWith("Prefab instance (", StringComparison.Ordinal) ||
                        line.StartsWith("Override target:", StringComparison.Ordinal) ||
                        line.StartsWith("Saved GameObject name overrides", StringComparison.Ordinal) ||
                        (line.Contains(" → ") && line.Contains(" [GUID:"));
                    var style = isOwner ? owner : normal;
                    var content = new GUIContent(line.Length == 0 ? " " : line);
                    Rect rect = GUILayoutUtility.GetRect(content, style, GUILayout.ExpandWidth(true));
                    EditorGUI.SelectableLabel(rect, line, style);
                }
            }
        }

        private void StartSearch()
        {
            Stop();
            complete = false; reviews.Clear(); auditSummary = "";
            results.Clear(); users.Clear(); nextHop.Clear(); errors.Clear(); details = ""; page = 0;
            searchedPath = AssetDatabase.GetAssetPath(target);
            candidates = AssetDatabase.GetAllAssetPaths().Where(p =>
                (p.StartsWith("Assets/", StringComparison.Ordinal) ||
                 (includePackages && p.StartsWith("Packages/", StringComparison.Ordinal))) &&
                !AssetDatabase.IsValidFolder(p)).ToArray();
            cursor = 0;
            scanning = true;
            status = "Reading direct dependencies; the Editor remains available between scan steps.";
            EditorApplication.update += Step;
        }

        private void Stop()
        {
            scanning = false;
            EditorApplication.update -= Step;
        }

        private void Step()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var timer = Stopwatch.StartNew();
            while (cursor < candidates.Length && timer.ElapsedMilliseconds < 20)
            {
                string path = candidates[cursor++];
                try
                {
                    foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    {
                        if (dependency == path) continue;
                        if (!users.TryGetValue(dependency, out var list))
                            users.Add(dependency, list = new List<string>());
                        list.Add(path);
                    }
                }
                catch (Exception ex) { errors.Add(path + ": " + ex.Message); }
            }
            if (cursor == candidates.Length)
            {
                Stop();
                var visited = new HashSet<string> { searchedPath };
                var queue = new Queue<string>();
                queue.Enqueue(searchedPath);
                while (queue.Count > 0)
                {
                    string dependency = queue.Dequeue();
                    if (!users.TryGetValue(dependency, out var parents)) continue;
                    foreach (string parent in parents)
                    {
                        if (!visited.Add(parent)) continue;
                        nextHop[parent] = dependency;
                        results.Add(parent);
                        queue.Enqueue(parent);
                    }
                }
                results.Sort(StringComparer.OrdinalIgnoreCase);
                complete = true;
                int direct = results.Count(p => nextHop[p] == searchedPath);
                status = $"Finished: {direct} direct, {results.Count - direct} indirect references. {errors.Count} assets could not be scanned.";
                if (refreshAndAudit)
                {
                    refreshAndAudit = false;
                    AuditReferences();
                    cleanupStatus += "\nSaved-reference results refreshed. " + status;
                }
            }
            Repaint();
        }

        private void ConfirmCleanup(string message, Action action)
        {
            confirmationText = message;
            confirmedAction = action;
            scroll = Vector2.zero;
        }

        private void RunCleanupAction(Func<string> action, bool refreshSavedResults = false)
        {
            if (this == null) return;
            try
            {
                cleanupStatus = action();
                if (refreshSavedResults) QueueRefresh();
                if (!string.IsNullOrEmpty(awaitingSave)) cleanupStatus += "\nWaiting for you to save " + awaitingSave + "; results will refresh automatically afterward.";
            }
            catch (Exception ex) { cleanupStatus = "Cleanup stopped: " + ex.Message; }
            complete = false;
            Repaint();
        }

        private void SceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.path != awaitingSave) return;
            awaitingSave = null;
            QueueRefresh();
        }

        private void QueueRefresh()
        {
            refreshPending = refreshAndAudit = true;
            refreshAfter = EditorApplication.timeSinceStartup + 0.75;
            EditorApplication.update -= RefreshWhenReady;
            EditorApplication.update += RefreshWhenReady;
        }

        private void RefreshWhenReady()
        {
            if (!refreshPending || EditorApplication.timeSinceStartup < refreshAfter || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= RefreshWhenReady;
            refreshPending = false;
            if (!IsAsset(target)) { refreshAndAudit = false; return; }
            StartSearch();
        }

        private void AuditReferences()
        {
            if (this == null || !complete || scanning) return;
            reviews.Clear();
            var direct = results.Where(p => nextHop[p] == searchedPath).ToArray();
            string guid = AssetDatabase.AssetPathToGUID(searchedPath);
            try
            {
                for (int i = 0; i < direct.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Review asset uses", direct[i], (float)i / direct.Length))
                    { reviews.Clear(); auditSummary = "Review cancelled; no conclusion about usage."; return; }
                    reviews.Add(FPAssetTracerCleanup.Inspect(direct[i], guid));
                }
                int unresolved = reviews.Count(r => !r.OnlyOrphans);
                var sources = reviews.SelectMany(r => r.MissingPrefabs).Distinct().ToArray();
                auditSummary = $"Reviewed {reviews.Count} direct asset files: {reviews.Count - unresolved} contain only missing-prefab overrides; {unresolved} have other/unresolved uses. Missing prefab sources: {sources.Length}.\n" +
                    ((unresolved == 0 && errors.Count == 0 && reviews.Count > 0)
                        ? "All scanned direct dependencies are accounted for by orphan override records. No other saved direct use was found in this scan scope. Indirect lighting/scene dependencies do not by themselves prove a rendered use."
                        : "Real usage has NOT been ruled out. Review other references and scan errors before removing assets.") +
                    "\nThis is not a runtime-use or safe-deletion guarantee: string lookups, unsaved scenes and excluded packages are not verified. Clean overrides, save if needed, then rescan; lighting data is never modified.";
            }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        private string Describe(string path)
        {
            var lines = new List<string> { "Dependency chain (one shortest route):" };
            string current = path;
            while (current != searchedPath)
            {
                lines.Add(current);
                current = nextHop[current];
            }
            lines.Add(searchedPath);
            lines.Add("\nSerialized references in the first asset to the next asset in this chain:");
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                DescribeSceneText(path, nextHop[path], lines);
                return string.Join("\n", lines);
            }
            int count = 0;
            try
            {
                var owners = new HashSet<Object>(AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o != null));
                foreach (var root in owners.OfType<GameObject>().ToArray())
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        owners.Add(transform.gameObject);
                        foreach (var component in transform.GetComponents<Component>())
                            if (component != null) owners.Add(component);
                    }
                foreach (var owner in owners)
                {
                    using (var serialized = new SerializedObject(owner))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var reference = property.objectReferenceValue;
                            if (reference == null || AssetDatabase.GetAssetPath(reference) != nextHop[path]) continue;
                            lines.Add($"{OwnerName(owner)} [{owner.GetType().Name}]{FPAssetTracerIdentity.ObjectId(owner)} → {property.propertyPath} → {reference.name}{FPAssetTracerIdentity.ObjectId(reference)}" +
                                (nextHop[path] == searchedPath ? (reference == target ? " (exact selected object)" : " (same asset file; different object)") : ""));
                            count++;
                        }
                    }
                    if (owner is Material material)
                        foreach (string slot in material.GetTexturePropertyNames())
                        {
                            var texture = material.GetTexture(slot);
                            if (texture != null && AssetDatabase.GetAssetPath(texture) == nextHop[path])
                                lines.Add($"Material texture slot: {slot} → {texture.name}");
                        }
                }
            }
            catch (Exception ex) { lines.Add("Detail inspection stopped: " + ex.Message); }
            if (count == 0) lines.Add("No exposed serialized object-reference field found. The dependency may be importer-managed or inherited. File-level dependency remains valid.");
            return string.Join("\n", lines);
        }

        private static string OwnerName(Object owner)
        {
            var transform = owner is Component component ? component.transform : (owner as GameObject)?.transform;
            if (transform == null) return owner.name;
            var names = new Stack<string>();
            while (transform != null) { names.Push(transform.name); transform = transform.parent; }
            return string.Join("/", names);
        }

        private static void DescribeSceneText(string path, string dependency, List<string> output)
        {
            output.Add("Saved scene text (scene is not opened). Document IDs and prefab override property paths identify the serialized reference:");
            string guid = AssetDatabase.AssetPathToGUID(dependency);
            var previous = new Queue<string>();
            string document = "";
            int lineNumber = 0;
            int matches = 0;
            try
            {
                string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", path));
                string text = System.IO.File.ReadAllText(fullPath);
                var identities = new FPAssetTracerIdentity(text);
                int offset = 0;
                foreach (System.Text.RegularExpressions.Match savedLine in System.Text.RegularExpressions.Regex.Matches(text, @"[^\r\n]*(?:\r\n|\n|$)"))
                {
                    string line = savedLine.Value.TrimEnd('\r', '\n');
                    offset = savedLine.Index;
                    lineNumber++;
                    if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                    {
                        document = line;
                        previous.Clear();
                    }
                    if (!string.IsNullOrEmpty(guid) && line.Contains("guid: " + guid))
                    {
                        output.Add($"\n{path}:{lineNumber} — {document}");
                        output.Add(identities.At(offset));
                        string sourceTarget = FPAssetTracerIdentity.SourceTarget(string.Join("\n", previous));
                        if (sourceTarget.Length > 0) output.Add(sourceTarget);
                        output.Add("Referenced asset: " + FPAssetTracerIdentity.Asset(guid));
                        output.AddRange(previous);
                        output.Add(line);
                        if (++matches >= 200) { output.Add("Display limited to the first 200 references."); break; }
                    }
                    previous.Enqueue(line);
                    if (previous.Count > 5) previous.Dequeue();
                }
            }
            catch (Exception ex) { output.Add("Unable to read scene text: " + ex.Message); }
            if (matches == 0) output.Add("No text GUID match. This can be a binary scene or an importer-managed dependency.");
        }
    }
}
