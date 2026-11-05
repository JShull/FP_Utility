// Copyright (c) 2026 John B. Shull.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.
namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Playables;

    internal static class FPSceneReferenceApply
    {
        internal static string WriteBlocker => EditorApplication.isPlayingOrWillChangePlaymode ? "Exit Play Mode before applying." :
            EditorApplication.isCompiling || EditorApplication.isUpdating ? "Wait for compilation/import to finish." :
            PrefabStageUtility.GetCurrentPrefabStage() != null ? "Close Prefab Mode before applying." : null;

        internal static string Preflight(FPSceneReferencePlan plan)
        {
            if (WriteBlocker != null) return WriteBlocker;
            if (plan == null || !plan.Complete || !plan.HasPreview) return "Build a completed replacement preview first.";
            string error = FPSceneReferencePlanner.StaleReason(plan);
            if (error != null) return error;
            var selected = plan.Edits.Where(e => e.Selected).ToArray();
            if (selected.Length == 0) return "Select at least one edit.";
            if (selected.GroupBy(e => (e.Owner, e.Source.BindingKey, Path: e.Source.IsBinding ? null : e.Source.Path)).Any(g => g.Count() > 1))
                return "Several selected proposals write to the same destination. Select only one per field or binding.";
            foreach (var edit in selected)
            {
                error = edit.Blocker ?? FPSceneReferencePlanner.MappingError(plan, edit.Mapping);
                if (error != null) return error;
                if (edit.Transfer ? edit.Owner != edit.Mapping.New || edit.Proposed != edit.Source.Target : edit.Proposed != edit.Mapping.New)
                    return "Mappings changed; rebuild the preview.";
                if (!FPSceneReferencePlanner.Inside(edit.Proposed, plan.NewRoot) && !edit.Transfer) return "Replacement left the new hierarchy.";
                if (edit.Transfer && (!FPSceneReferencePlanner.Inside(edit.Owner, plan.NewRoot) || FPSceneReferencePlanner.Inside(edit.Proposed, plan.OldRoot))) return "Outgoing transfer membership changed.";
                if (edit.Source.IsBinding)
                {
                    var director = edit.Owner as PlayableDirector;
                    if (!FPSceneReferencePlanner.BindingMatches(director, edit.Source) || director.GetGenericBinding(edit.Source.BindingKey) != edit.Expected)
                        return "Timeline binding changed; rebuild the preview.";
                }
                else using (var so = new SerializedObject(edit.Owner))
                {
                    var p = so.FindProperty(edit.Source.Path);
                    if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue != edit.Expected) return "Serialized field changed; rebuild the preview.";
                }
                error = FPSceneReferencePlanner.Compatibility(edit);
                if (error != null) return error;
            }
            return null;
        }

        internal static bool Apply(FPSceneReferencePlan plan, out string result, Action<int> afterWrite = null)
        {
            string error;
            try { error = Preflight(plan); }
            catch (Exception ex) { result = "Changed 0; preflight failed: " + ex.Message; return false; }
            if (error != null) { result = "Changed 0; preflight blocked: " + error; return false; }
            var selected = plan.Edits.Where(e => e.Selected).ToArray();
            int changed = 0, skipped = 0;
            if (selected.All(e => e.Expected == e.Proposed))
            {
                plan.HasPreview = false;
                result = "Changed 0; skipped " + selected.Length + "; failed 0. Selected references already match; scene has not been saved.";
                return true;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Scene References");
            try
            {
                foreach (var owner in selected.Select(e => e.Owner).Distinct()) Undo.RegisterCompleteObjectUndo(owner, "Replace Scene References");
                foreach (var edit in selected)
                {
                    if (ReadCurrent(edit) != edit.Expected) throw new InvalidOperationException("A selected destination changed during apply; rebuild the preview.");
                    error = FPSceneReferencePlanner.Compatibility(edit);
                    if (error != null) throw new InvalidOperationException(error);
                    if (edit.Expected == edit.Proposed) { skipped++; continue; }
                    if (edit.Source.IsBinding) ((PlayableDirector)edit.Owner).SetGenericBinding(edit.Source.BindingKey, edit.Proposed);
                    else using (var so = new SerializedObject(edit.Owner))
                    {
                        so.FindProperty(edit.Source.Path).objectReferenceValue = edit.Proposed;
                        if (edit.Source.Path.EndsWith(".m_Target", StringComparison.Ordinal) && edit.Source.EventInfo != null)
                        {
                            var typeName = so.FindProperty(edit.Source.Path.Substring(0, edit.Source.Path.Length - "m_Target".Length) + "m_TargetAssemblyTypeName");
                            if (typeName != null) typeName.stringValue = edit.Proposed.GetType().AssemblyQualifiedName;
                        }
                        if (!so.ApplyModifiedPropertiesWithoutUndo()) throw new InvalidOperationException("Unity did not accept the serialized assignment.");
                    }
                    if (ReadCurrent(edit) != edit.Proposed) throw new InvalidOperationException("Reference or binding assignment was rejected.");
                    PrefabUtility.RecordPrefabInstancePropertyModifications(edit.Owner);
                    EditorUtility.SetDirty(edit.Owner);
                    changed++;
                    afterWrite?.Invoke(changed);
                }
                // A later OnValidate callback may have altered a previously written field.
                foreach (var edit in selected)
                {
                    if (ReadCurrent(edit) != edit.Proposed) throw new InvalidOperationException("A selected destination changed after its assignment.");
                    error = FPSceneReferencePlanner.Compatibility(edit);
                    if (error != null) throw new InvalidOperationException(error);
                }
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
                if (changed > 0) EditorSceneManager.MarkSceneDirty(plan.Scene);
                plan.HasPreview = false;
                result = "Changed " + changed + "; skipped " + skipped + "; failed 0. Scene has not been saved.";
                return true;
            }
            catch (Exception ex)
            {
                Undo.FlushUndoRecordObjects(); Undo.RevertAllDownToGroup(group);
                plan.HasPreview = false;
                result = "Changed 0; skipped " + skipped + "; failed 1. Rolled back " + changed + " write(s): " + ex.Message;
                return false;
            }
        }

        internal static UnityEngine.Object ReadCurrent(FPSceneReferenceEdit edit)
        {
            if (edit.Source.IsBinding)
            {
                var director = edit.Owner as PlayableDirector;
                if (!FPSceneReferencePlanner.BindingMatches(director, edit.Source)) throw new InvalidOperationException("Timeline output changed during apply.");
                return director.GetGenericBinding(edit.Source.BindingKey);
            }
            using (var so = new SerializedObject(edit.Owner))
            {
                var property = so.FindProperty(edit.Source.Path);
                if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) throw new InvalidOperationException("Serialized destination disappeared during apply.");
                return property.objectReferenceValue;
            }
        }
    }
}
