// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.
namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class FPSceneReferenceReplaceEditorWindow : EditorWindow
    {
        [SerializeField] private GameObject oldGameObject, newGameObject;
        [SerializeField] private MonoScript componentScript;
        [SerializeField] private bool hierarchyMode = true, replaceGameObjectReferences = true, replaceSpecificComponentReferences = true;
        [SerializeField] private bool transferOutgoing;
        [SerializeField] private string filter = "";
        [SerializeField] private int tab;
        [SerializeField] private FPSceneLinkCategory category = FPSceneLinkCategory.Incoming;
        private FPSceneReferencePlan plan;
        private Vector2 controlsScroll, resultsScroll;
        private string status = "Trace an old root to inspect scene connections. Plans are cleared after domain reload.";
        private bool stale;
        private int page;

        [MenuItem("FuzzPhyte/Utility/Scene/Scene Reference Replacer", priority = FP_UtilityData.MENU_UTILITY_SCENE + 12)]
        public static void Open()
        {
            var window = GetWindow<FPSceneReferenceReplaceEditorWindow>();
            window.titleContent = new GUIContent("Scene Ref Replacer");
            window.minSize = new Vector2(860, 520);
            window.Show();
        }
        private void OnEnable()
        {
            plan = null;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            ObjectChangeEvents.changesPublished += ObjectsChanged;
        }
        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
            Undo.undoRedoPerformed -= Invalidate;
            ObjectChangeEvents.changesPublished -= ObjectsChanged;
        }
        private void ObjectsChanged(ref ObjectChangeEventStream stream) => Invalidate();
        private void Invalidate() { if (plan != null) stale = true; Repaint(); }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(300))) DrawControls();
                GUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) DrawResults();
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinHeight(70)))
            {
                EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                if (FPSceneReferenceApply.WriteBlocker != null) EditorGUILayout.HelpBox(FPSceneReferenceApply.WriteBlocker, MessageType.Warning);
                if (stale) EditorGUILayout.HelpBox("Scene or project changed. Rebuild Preview before applying; manual mappings are retained.", MessageType.Warning);
                EditorGUILayout.HelpBox(status, MessageType.Info);
            }
        }
        private void DrawControls()
        {
            controlsScroll = EditorGUILayout.BeginScrollView(controlsScroll);
            EditorGUILayout.LabelField("Scene Reference Replacer", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            hierarchyMode = GUILayout.Toolbar(hierarchyMode ? 1 : 0, new[] { "Object Pair", "Hierarchy" }) == 1;
            oldGameObject = RootField("Old Root", oldGameObject);
            newGameObject = RootField("New Root", newGameObject);
            if (!hierarchyMode)
            {
                componentScript = (MonoScript)EditorGUILayout.ObjectField("Component Script", componentScript, typeof(MonoScript), false);
                replaceGameObjectReferences = EditorGUILayout.ToggleLeft("Replace GameObject references", replaceGameObjectReferences);
                replaceSpecificComponentReferences = EditorGUILayout.ToggleLeft("Replace selected component type", replaceSpecificComponentReferences);
            }
            if (EditorGUI.EndChangeCheck()) { plan = null; stale = false; }
            EditorGUILayout.HelpBox("Active scene only, including inactive objects and Timeline generic bindings. Trace and preview are read-only.", MessageType.Info);
            string traceError = FPSceneReferencePlanner.ValidateRoots(oldGameObject, newGameObject, false);
            if (traceError != null) EditorGUILayout.HelpBox(traceError, MessageType.Warning);
            using (new EditorGUI.DisabledScope(traceError != null || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("Trace Scene Links")) Trace(false);
            bool nextTransfers = EditorGUILayout.ToggleLeft("Transfer outgoing scene links (opt-in)", transferOutgoing);
            if (nextTransfers != transferOutgoing) { transferOutgoing = nextTransfers; if (plan != null) plan.HasPreview = false; }
            string previewError = FPSceneReferencePlanner.ValidateRoots(oldGameObject, newGameObject, true);
            using (new EditorGUI.DisabledScope(previewError != null || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button(plan == null ? "Build Replacement Preview" : "Rebuild Preview", GUILayout.Height(30))) Trace(true);
            if (plan != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Scope: " + plan.Scene.name);
                EditorGUILayout.LabelField("Inventory: " + plan.Inventory.Count + " objects/components");
                EditorGUILayout.LabelField("Discovered links: " + plan.Links.Count);
                EditorGUILayout.LabelField("Eligible: " + plan.Edits.Count(e => e.Ready) + " / Selected: " + plan.Edits.Count(e => e.Selected));
                EditorGUILayout.LabelField("Unmapped objects: " + plan.Mappings.Count(m => !m.Ignored && m.New == null));
                EditorGUILayout.LabelField("Blocked edits: " + plan.Edits.Count(e => !e.Ready));
                var color = GUI.backgroundColor;
                GUI.backgroundColor = FP_Utility_Editor.OkayColor;
                using (new EditorGUI.DisabledScope(stale || !plan.Complete || !plan.HasPreview || !plan.Edits.Any(e => e.Selected) || FPSceneReferenceApply.WriteBlocker != null))
                    if (GUILayout.Button("Apply Selected Changes", GUILayout.Height(32))) Apply();
                GUI.backgroundColor = color;
                if (GUILayout.Button("Copy Markdown Report")) EditorGUIUtility.systemCopyBuffer = FPSceneReferencePlanner.Report(plan);
                if (GUILayout.Button("Export Markdown Report"))
                {
                    string path = EditorUtility.SaveFilePanel("Export scene reference audit", "", "SceneReferenceAudit", "md");
                    if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, FPSceneReferencePlanner.Report(plan));
                }
            }
            EditorGUILayout.EndScrollView();
        }
        private static GameObject RootField(string label, GameObject value)
        {
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 70;
            value = (GameObject)EditorGUILayout.ObjectField(label, value, typeof(GameObject), true);
            EditorGUIUtility.labelWidth = labelWidth;
            if (GUILayout.Button("Use Selection for " + label)) value = Selection.activeGameObject;
            return value;
        }
        private void Trace(bool preview)
        {
            try
            {
                var previous = plan;
                plan = FPSceneReferencePlanner.Trace(oldGameObject, newGameObject,
                    () => EditorUtility.DisplayCancelableProgressBar("Scene Reference Replacer", "Scanning active scene", 0.5f));
                if (preview && plan.Complete)
                {
                    if (previous != null && previous.OldRoot == plan.OldRoot && previous.NewRoot == plan.NewRoot)
                        foreach (var mapping in plan.Mappings)
                        {
                            var old = previous.Mappings.FirstOrDefault(m => m.Old == mapping.Old && (m.Manual || m.Ignored));
                            if (old != null) { mapping.New = old.New; mapping.Manual = old.Manual; mapping.Ignored = old.Ignored; mapping.Reason = old.Reason; mapping.ReviewManyToOne = old.ReviewManyToOne; }
                        }
                    if (!hierarchyMode) ConfigurePair();
                    FPSceneReferencePlanner.BuildPreview(plan, transferOutgoing);
                }
                stale = false;
                page = 0;
                status = plan.Complete ? preview ? "Preview ready. Review mappings and selected edits before applying." : "Read-only trace completed." : "Scan incomplete. Apply is blocked.";
            }
            catch (Exception ex) { status = ex.Message; plan = null; }
            finally { EditorUtility.ClearProgressBar(); }
        }
        private void ConfigurePair()
        {
            var type = componentScript == null ? null : componentScript.GetClass();
            if (replaceSpecificComponentReferences && (type == null || !typeof(Component).IsAssignableFrom(type))) throw new InvalidOperationException("Choose a Component script or disable component replacement.");
            foreach (var mapping in plan.Mappings)
                mapping.Ignored = !(mapping.Old == oldGameObject && replaceGameObjectReferences) &&
                    !(replaceSpecificComponentReferences && mapping.Old is Component c && c.gameObject == oldGameObject && c.GetType() == type);
        }
        private void Apply()
        {
            if (!FPSceneReferenceApply.Apply(plan, out status)) { stale = true; return; }
            string applied = status;
            plan = FPSceneReferencePlanner.Trace(oldGameObject, newGameObject);
            int incoming = plan.Links.Count(l => l.Category == FPSceneLinkCategory.Incoming);
            int replacement = plan.Links.Count(l => l.Category == FPSceneLinkCategory.ReplacementToOld);
            status = applied + " Audit: " + incoming + " surrounding-scene links and " + replacement + " replacement-to-old links remain. Retire the old hierarchy manually and validate runtime behavior.";
            stale = false;
        }
        private bool Matches(string text) => string.IsNullOrEmpty(filter) || text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        private bool Matches(FPSceneReferenceEdit e) => Matches(FPSceneReferencePlanner.Describe(e.Owner) + " " + e.Source.Path + " " + e.Blocker + " " + FPSceneReferencePlanner.Describe(e.Expected) + " " + FPSceneReferencePlanner.Describe(e.Proposed));
        private void DrawResults()
        {
            EditorGUI.BeginChangeCheck();
            tab = GUILayout.Toolbar(tab, new[] { "Inventory", "Connections", "Mappings", "Preview", "Warnings" });
            filter = EditorGUILayout.TextField("Filter (display only)", filter);
            if (EditorGUI.EndChangeCheck()) { page = 0; resultsScroll = Vector2.zero; }
            if (plan == null) { EditorGUILayout.HelpBox("Choose roots and trace scene links to begin.", MessageType.Info); return; }
            if (tab == 1) category = (FPSceneLinkCategory)EditorGUILayout.EnumPopup("Connection group", category);
            if (tab == 3)
            {
                EditorGUILayout.HelpBox("Incoming edits start selected. Transfers, collection slots and replacement-to-old edits require individual selection. Bulk actions affect all filtered rows across pages; selection includes only ready incoming rows.", MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select filtered incoming")) foreach (var e in plan.Edits.Where(e => e.Ready && !e.Transfer && e.Source.Category == FPSceneLinkCategory.Incoming && Matches(e))) e.Selected = true;
                    if (GUILayout.Button("Deselect filtered")) foreach (var e in plan.Edits.Where(Matches)) e.Selected = false;
                }
            }
            resultsScroll = EditorGUILayout.BeginScrollView(resultsScroll);
            if (tab == 0) foreach (var item in PageRows(plan.Inventory.Where(o => Matches(FPSceneReferencePlanner.Describe(o))))) ObjectRow("Inventory", item);
            if (tab == 1)
                foreach (var link in PageRows(plan.Links.Where(l => l.Category == category && Matches(FPSceneReferencePlanner.Describe(l.Owner) + " " + l.Path + " " + FPSceneReferencePlanner.Describe(l.Target)))))
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        ObjectRow("Owner", link.Owner); EditorGUILayout.LabelField(link.IsBinding ? "Timeline: " + link.Path : link.Path, EditorStyles.wordWrappedLabel);
                        if (link.IsBinding) ObjectRow("Binding key", link.BindingKey);
                        ObjectRow("Target", link.Target); if (link.EventInfo != null) EditorGUILayout.LabelField(link.EventInfo, EditorStyles.wordWrappedLabel);
                    }
            if (tab == 2) DrawMappings();
            if (tab == 3)
                foreach (var edit in PageRows(plan.Edits.Where(Matches)))
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUI.DisabledScope(!edit.Ready || !plan.HasPreview || stale)) edit.Selected = EditorGUILayout.ToggleLeft((edit.Transfer ? "Outgoing transfer" : edit.Source.Category.ToString()) + " / " + edit.Source.Path, edit.Selected);
                        ObjectRow("Owner", edit.Owner); ObjectRow("Expected current", edit.Expected); ObjectRow("Proposed", edit.Proposed);
                        if (edit.Source.IsBinding) ObjectRow("Binding key", edit.Source.BindingKey);
                        EditorGUILayout.LabelField(edit.Blocker ?? (edit.Transfer ? "Ready - explicit review required" : "Ready"), EditorStyles.wordWrappedLabel);
                        if (edit.Transfer && edit.Expected != null && edit.Expected != edit.Proposed) EditorGUILayout.LabelField("Conflict: destination is populated.", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(edit.Mapping?.Reason ?? "Unmapped", EditorStyles.miniLabel);
                    }
            if (tab == 4) foreach (string warning in plan.Warnings.Where(Matches)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }
        private void DrawMappings()
        {
            foreach (var mapping in PageRows(plan.Mappings.Where(m => Matches(FPSceneReferencePlanner.Describe(m.Old) + " " + FPSceneReferencePlanner.Describe(m.New) + " " + m.Reason))))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    ObjectRow("Old", mapping.Old);
                    EditorGUI.BeginChangeCheck();
                    var proposed = EditorGUILayout.ObjectField("New member", mapping.New, typeof(Object), true);
                    bool ignored = EditorGUILayout.Toggle("Ignore mapping", mapping.Ignored);
                    bool reviewed = EditorGUILayout.Toggle("Reviewed many-to-one", mapping.ReviewManyToOne);
                    if (EditorGUI.EndChangeCheck())
                    {
                        mapping.New = proposed; mapping.Ignored = ignored; mapping.ReviewManyToOne = reviewed;
                        mapping.Manual = true; mapping.Reason = "Manually Assigned"; plan.HasPreview = false;
                    }
                    EditorGUILayout.LabelField(FPSceneReferencePlanner.MappingError(plan, mapping) ?? mapping.Reason, EditorStyles.wordWrappedLabel);
                }
        }
        private IEnumerable<T> PageRows<T>(IEnumerable<T> source)
        {
            const int pageSize = 100;
            var rows = source.ToArray();
            int lastPage = Mathf.Max(0, (rows.Length - 1) / pageSize);
            page = Mathf.Clamp(page, 0, lastPage);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(page == 0)) if (GUILayout.Button("Previous", GUILayout.Width(70))) { page--; resultsScroll = Vector2.zero; }
                GUILayout.Label(rows.Length + " filtered rows | Page " + (page + 1) + " / " + (lastPage + 1), EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(page == lastPage)) if (GUILayout.Button("Next", GUILayout.Width(50))) { page++; resultsScroll = Vector2.zero; }
            }
            return rows.Skip(page * pageSize).Take(pageSize);
        }
        private static void ObjectRow(string label, Object value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(label, value, typeof(Object), true);
                using (new EditorGUI.DisabledScope(value == null))
                {
                    if (GUILayout.Button("Select", GUILayout.Width(48))) Selection.activeObject = value;
                    if (GUILayout.Button("Ping", GUILayout.Width(38))) EditorGUIUtility.PingObject(value);
                }
            }
            EditorGUILayout.LabelField(FPSceneReferencePlanner.Describe(value), EditorStyles.wordWrappedMiniLabel);
        }
    }
}
