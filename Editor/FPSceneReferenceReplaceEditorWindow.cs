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
        [SerializeField] private bool blockedOnly;
        [SerializeField] private GameObject preservationParent;
        [SerializeField] private List<GameObject> preservationRoots = new List<GameObject>();
        private FPSceneReferencePlan plan;
        private FPScenePreservationPlan preservation;
        private Object candidateSource;
        private List<FPSceneMappingCandidate> candidates;
        private bool preservationStale;
        private Vector2 controlsScroll, resultsScroll;
        private string status = "Trace an old root to inspect scene connections. Plans are cleared after domain reload.";
        private bool stale;
        private int page;

        [MenuItem("FuzzPhyte/Utility/Scene/Scene Reference Replacer", priority = FP_UtilityData.MENU_UTILITY_SCENE + 12)]
        public static void Open()
        {
            var window = GetWindow<FPSceneReferenceReplaceEditorWindow>();
            window.titleContent = new GUIContent("Scene Ref Replacer");
            window.minSize = new Vector2(1000, 600);
            window.Show();
        }
        private void OnEnable()
        {
            plan = null;
            preservation = null; candidates = null;
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
        private void Invalidate() { if (plan != null) stale = true; if (preservation != null) preservationStale = true; Repaint(); }
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
            if (EditorGUI.EndChangeCheck()) { plan = null; preservation = null; candidates = null; preservationRoots.Clear(); stale = false; }
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
                candidates = null;
                plan = FPSceneReferencePlanner.Trace(oldGameObject, newGameObject,
                    () => EditorUtility.DisplayCancelableProgressBar("Scene Reference Replacer", "Scanning active scene", 0.5f));
                if (plan.Complete)
                {
                    if (previous != null && previous.OldRoot == plan.OldRoot && previous.NewRoot == plan.NewRoot)
                        foreach (var mapping in plan.Mappings)
                        {
                            var old = previous.Mappings.FirstOrDefault(m => m.Old == mapping.Old && (m.Manual || m.Ignored));
                            if (old != null) { mapping.New = old.New; mapping.Manual = old.Manual; mapping.Ignored = old.Ignored; mapping.Reason = old.Reason; mapping.ReviewManyToOne = old.ReviewManyToOne; }
                        }
                    if (preview)
                    {
                        if (!hierarchyMode) ConfigurePair();
                        FPSceneReferencePlanner.BuildPreview(plan, transferOutgoing);
                    }
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
        private bool Matches(FPSceneReferenceEdit e) => (!blockedOnly || !e.Ready) && Matches(FPSceneReferencePlanner.Describe(e.Owner) + " " + e.Source.Path + " " + e.Blocker + " " + FPSceneReferencePlanner.Describe(e.Expected) + " " + FPSceneReferencePlanner.Describe(e.Proposed));
        private void DrawResults()
        {
            EditorGUI.BeginChangeCheck();
            tab = GUILayout.Toolbar(tab, new[] { "Inventory", "Connections", "Mappings", "Preview", "Warnings", "Suggestions", "Keep Objects" });
            filter = EditorGUILayout.TextField("Filter (display only)", filter);
            if (EditorGUI.EndChangeCheck()) { page = 0; resultsScroll = Vector2.zero; }
            if (tab == 6)
            {
                resultsScroll = EditorGUILayout.BeginScrollView(resultsScroll);
                DrawPreservation();
                EditorGUILayout.EndScrollView();
                return;
            }
            if (plan == null) { EditorGUILayout.HelpBox("Choose roots and trace scene links to begin.", MessageType.Info); return; }
            if (tab == 1) category = (FPSceneLinkCategory)EditorGUILayout.EnumPopup("Connection group", category);
            if (tab == 3)
            {
                blockedOnly = EditorGUILayout.ToggleLeft("Show blocked edits only", blockedOnly);
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
                        if (!string.IsNullOrEmpty(edit.ReviewReason)) EditorGUILayout.HelpBox(edit.ReviewReason, MessageType.Warning);
                        if (edit.Transfer && edit.Expected != null && edit.Expected != edit.Proposed) EditorGUILayout.LabelField("Conflict: destination is populated.", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(edit.Mapping?.Reason ?? "Unmapped", EditorStyles.miniLabel);
                        if (!edit.Ready && edit.Mapping != null) MappingActions(edit.Mapping);
                    }
            if (tab == 4)
            {
                foreach (string warning in plan.Warnings.Where(Matches)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                EditorGUILayout.LabelField("Director reference audit", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(plan.AuditedDirectors + " director(s) with playable assets; " + plan.DirectorReferences.Count(r => r.Kind == "Exposed reference") + " exposed fields; " + plan.DirectorReferences.Count(r => r.Kind == "Asset-held reference") + " asset-held fields; " + plan.DirectorReferences.Count(r => r.TargetsOld) + " targeting old hierarchy; " + plan.DirectorAuditFailures + " unreadable assets.", MessageType.Info);
                EditorGUILayout.HelpBox("Read-only locations below are not replacement edits. Covers assigned playable assets and Timeline tracks/clips. Custom nested asset graphs and orphan Director table entries remain outside this audit. Unresolved exposed fields show their key and default/override status.", MessageType.Info);
                foreach (var row in PageRows(plan.DirectorReferences.Where(r => Matches(r.Location + " " + r.Kind + " " + r.Detail + " " + FPSceneReferencePlanner.Describe(r.Director) + " " + FPSceneReferencePlanner.Describe(r.Target)))))
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField(row.Kind + (row.TargetsOld ? " — TARGETS OLD HIERARCHY" : ""), EditorStyles.boldLabel);
                        ObjectRow("Director", row.Director); ObjectRow("Asset / clip", row.Asset);
                        EditorGUILayout.LabelField(row.Location + "\n" + row.Detail, EditorStyles.wordWrappedLabel);
                        ObjectRow("Resolved target", row.Target);
                    }
            }
            if (tab == 5) DrawSuggestions();
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
                    if (mapping.New == null && !mapping.Ignored)
                    {
                        EditorGUILayout.LabelField(FPSceneMappingReview.Diagnosis(plan, mapping), EditorStyles.wordWrappedLabel);
                        MappingActions(mapping);
                    }
                }
        }
        private void MappingActions(FPSceneObjectMapping mapping)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(plan.NewRoot == null))
                    if (GUILayout.Button("Review candidates"))
                    {
                        candidateSource = mapping.Old;
                        candidates = FPSceneMappingReview.FindCandidates(plan, mapping);
                        tab = 5; page = 0; resultsScroll = Vector2.zero; filter = "";
                    }
                var go = FPSceneReferencePlanner.GameObjectOf(mapping.Old);
                using (new EditorGUI.DisabledScope(go == null || go == oldGameObject))
                    if (GUILayout.Button("Queue object to keep")) QueuePreservation(go);
            }
        }
        private void DrawSuggestions()
        {
            var mapping = plan.Mappings.FirstOrDefault(m => m.Old == candidateSource);
            if (mapping == null) { EditorGUILayout.HelpBox("Use Review candidates on a mapping or blocked preview row.", MessageType.Info); return; }
            ObjectRow("Old target", mapping.Old);
            EditorGUILayout.HelpBox(FPSceneMappingReview.Diagnosis(plan, mapping), MessageType.Info);
            foreach (var use in FPSceneMappingReview.IncomingUses(plan, mapping.Old))
                EditorGUILayout.LabelField(FPSceneReferencePlanner.Describe(use.Owner) + " / " + use.Path + " / " + use.EventInfo, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox("Signature/type compatibility does not establish the same role. Review the object's purpose and outgoing configuration. Suggestions never change mappings automatically.", MessageType.Warning);
            if (GUILayout.Button("Refresh candidate search")) candidates = FPSceneMappingReview.FindCandidates(plan, mapping);
            if (candidates == null) return;
            EditorGUILayout.LabelField(candidates.Count + " candidate(s)", EditorStyles.boldLabel);
            foreach (var candidate in PageRows(candidates.Where(c => Matches(FPSceneReferencePlanner.Describe(c.Target) + " " + c.Reason + " " + c.Blocker))))
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    ObjectRow("Candidate", candidate.Target);
                    EditorGUILayout.LabelField(candidate.Blocker ?? candidate.Reason, EditorStyles.wordWrappedLabel);
                    using (new EditorGUI.DisabledScope(candidate.Blocker != null || stale || !FPSceneReferencePlanner.Inside(candidate.Target, newGameObject)))
                        if (GUILayout.Button("Use this reviewed mapping"))
                        {
                            mapping.New = candidate.Target; mapping.Manual = true; mapping.Ignored = false;
                            mapping.ReviewManyToOne = false; mapping.Reason = "Manually Assigned: reviewed candidate";
                            plan.HasPreview = false;
                            status = "Mapping assigned. Rebuild Preview and review the resulting edits before applying.";
                        }
                }
            using (new EditorGUI.DisabledScope(FPSceneReferencePlanner.GameObjectOf(mapping.Old) == oldGameObject))
                if (GUILayout.Button("Queue this old object's subtree to keep")) QueuePreservation(FPSceneReferencePlanner.GameObjectOf(mapping.Old));
        }
        private void QueuePreservation(GameObject root)
        {
            if (root == null || root == oldGameObject || !FPSceneReferencePlanner.Inside(root, oldGameObject))
            {
                status = "Select a descendant inside the old hierarchy to queue its entire subtree.";
                return;
            }
            if (!preservationRoots.Contains(root)) preservationRoots.Add(root);
            preservation = null; tab = 6; filter = ""; page = 0; resultsScroll = Vector2.zero;
        }
        private void DrawPreservation()
        {
            EditorGUILayout.LabelField("Keep scene-specific objects", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This separate operation moves explicitly queued subtrees outside both system hierarchies. It keeps existing references; it does not map, copy or delete objects. Review remaining dependencies on the old system before retiring it.", MessageType.Info);
            if (GUILayout.Button("Queue selected old descendant")) QueuePreservation(Selection.activeGameObject);
            EditorGUI.BeginChangeCheck();
            preservationParent = (GameObject)EditorGUILayout.ObjectField("Destination parent", preservationParent, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) preservation = null;
            EditorGUILayout.LabelField("Empty destination = scene root. Selected descendants of a queued parent are included in that parent's move.", EditorStyles.wordWrappedMiniLabel);
            for (int i = 0; i < preservationRoots.Count; i++)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    ObjectRow("Queued subtree", preservationRoots[i]);
                    if (GUILayout.Button("Remove from queue")) { preservationRoots.RemoveAt(i--); preservation = null; }
                }
            using (new EditorGUI.DisabledScope(preservationRoots.Count == 0 || FPSceneReferencePlanner.ValidateRoots(oldGameObject, newGameObject, false) != null || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("Build Preservation Preview"))
                {
                    try
                    {
                        preservation = FPSceneObjectPreservation.Build(oldGameObject, newGameObject, preservationRoots, preservationParent,
                            () => EditorUtility.DisplayCancelableProgressBar("Scene-specific preservation", "Scanning dependencies for preservation preview", 0.5f));
                        preservationStale = false;
                    }
                    catch (Exception ex) { preservation = null; status = ex.Message; }
                    finally { EditorUtility.ClearProgressBar(); }
                }
            if (preservation == null) return;
            if (!preservation.Trace.Complete) EditorGUILayout.HelpBox("Preservation scan is incomplete. No moves can apply; build the preview again.", MessageType.Error);
            if (preservationStale) EditorGUILayout.HelpBox("Preservation preview is stale. Build it again before applying.", MessageType.Warning);
            foreach (var row in preservation.Rows)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    ObjectRow("Move subtree", row.Root);
                    ObjectRow("Current parent", row.ExpectedParent);
                    ObjectRow("Destination", preservation.Parent);
                    EditorGUILayout.LabelField(row.ObjectCount + " GameObjects / " + row.ComponentCount + " components / " + row.ReferencesToRemainingOld + " links back to remaining old hierarchy", EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("World position: " + row.WorldPosition + " | effective active: " + row.ActiveInHierarchy, EditorStyles.wordWrappedMiniLabel);
                    if (row.Blocker != null) EditorGUILayout.HelpBox(row.Blocker, MessageType.Error);
                }
            EditorGUILayout.HelpBox("World position/rotation are retained. Review scale/shear when parents have nonuniform scale. Links back to the remaining old hierarchy stay intact and must be reviewed in the fresh audit.", MessageType.Warning);
            if (GUILayout.Button("Copy preservation report")) EditorGUIUtility.systemCopyBuffer = FPSceneObjectPreservation.Report(preservation);
            using (new EditorGUI.DisabledScope(preservationStale || preservation.Applied || preservation.Rows.Count == 0 || !preservation.Trace.Complete || preservation.Rows.Any(r => r.Blocker != null) || FPSceneReferenceApply.WriteBlocker != null))
                if (GUILayout.Button("Apply Preservation Moves", GUILayout.Height(32)))
                {
                    if (FPSceneObjectPreservation.Apply(preservation, out status))
                    {
                        string moved = status;
                        preservationRoots.Clear(); preservation = null;
                        Trace(false);
                        status = moved + " Fresh audit: " + (plan == null ? "incomplete; trace again." : plan.Links.Count(l => l.Category == FPSceneLinkCategory.Incoming || l.Category == FPSceneLinkCategory.ReplacementToOld) + " incoming/replacement-to-old links remain.");
                    }
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
