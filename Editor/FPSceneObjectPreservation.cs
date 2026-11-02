// Copyright (c) 2026 John B. Shull. GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    internal sealed class FPScenePreservationRow
    {
        internal GameObject Root;
        internal Transform ExpectedParent;
        internal Vector3 WorldPosition;
        internal Quaternion WorldRotation;
        internal bool ActiveInHierarchy;
        internal int ObjectCount, ComponentCount, ReferencesToRemainingOld;
        internal string Blocker;
    }

    internal sealed class FPScenePreservationPlan
    {
        internal FPSceneReferencePlan Trace;
        internal GameObject Parent;
        internal bool Applied;
        internal readonly List<FPScenePreservationRow> Rows = new List<FPScenePreservationRow>();
    }

    /// <summary>Explicit subtree moves, independent of reference replacement. No unpacking or reference rewrites.</summary>
    internal static class FPSceneObjectPreservation
    {
        internal static FPScenePreservationPlan Build(GameObject oldRoot, GameObject newRoot, IEnumerable<GameObject> selected, GameObject parent, Func<bool> cancelled = null)
        {
            var plan = new FPScenePreservationPlan { Trace = FPSceneReferencePlanner.Trace(oldRoot, newRoot, cancelled), Parent = parent };
            var roots = selected.Where(g => g != null).Distinct().ToArray();
            // A selected ancestor already includes its descendants; never move a descendant twice.
            roots = roots.Where(g => !roots.Any(other => other != g && g.transform.IsChildOf(other.transform))).ToArray();
            foreach (var root in roots)
            {
                var row = new FPScenePreservationRow { Root = root, ExpectedParent = root.transform.parent,
                    WorldPosition = root.transform.position, WorldRotation = root.transform.rotation, ActiveInHierarchy = root.activeInHierarchy };
                var transforms = root.GetComponentsInChildren<Transform>(true);
                row.ObjectCount = transforms.Length;
                row.ComponentCount = transforms.Sum(t => t.GetComponents<Component>().Count(c => c != null));
                row.ReferencesToRemainingOld = plan.Trace.Links.Count(l => FPSceneReferencePlanner.Inside(l.Owner, root) &&
                    FPSceneReferencePlanner.Inside(l.Target, oldRoot) && !roots.Any(g => FPSceneReferencePlanner.Inside(l.Target, g)));
                row.Blocker = RowError(plan, row);
                plan.Rows.Add(row);
            }
            return plan;
        }

        private static string RowError(FPScenePreservationPlan plan, FPScenePreservationRow row)
        {
            var root = row.Root;
            if (root == null || root == plan.Trace.OldRoot || !FPSceneReferencePlanner.Inside(root, plan.Trace.OldRoot))
                return "Choose a descendant of the old root; the old root itself cannot be preserved by this operation.";
            var parent = plan.Parent;
            if (parent != null && (EditorUtility.IsPersistent(parent) || parent.scene != plan.Trace.Scene ||
                FPSceneReferencePlanner.Inside(parent, plan.Trace.OldRoot) || FPSceneReferencePlanner.Inside(parent, plan.Trace.NewRoot)))
                return "Destination must be a scene root or a parent in the same scene outside both system hierarchies.";
            if ((root.hideFlags & HideFlags.NotEditable) != 0 || (parent != null && (parent.hideFlags & HideFlags.NotEditable) != 0))
                return "Source or destination is not editable.";
            if (PrefabUtility.IsPartOfPrefabInstance(root) && PrefabUtility.GetOutermostPrefabInstanceRoot(root) != root)
                return "This object is inside a prefab instance. Unpack it manually or preserve an eligible outermost instance root; the tool never unpacks prefabs.";
            if (parent != null && PrefabUtility.IsPartOfPrefabInstance(parent))
                return "Choose a non-prefab destination parent to avoid changing prefab structure.";
            bool resultingActive = root.activeSelf && (parent == null || parent.activeInHierarchy);
            if (resultingActive != root.activeInHierarchy)
                return "Moving would change effective activation. Adjust source or destination activation manually, then rebuild.";
            return null;
        }

        internal static string Preflight(FPScenePreservationPlan plan)
        {
            if (FPSceneReferenceApply.WriteBlocker != null) return FPSceneReferenceApply.WriteBlocker;
            if (plan == null || plan.Applied || !plan.Trace.Complete || plan.Rows.Count == 0) return "Build a complete preservation preview with at least one eligible subtree.";
            string error = FPSceneReferencePlanner.StaleReason(plan.Trace);
            if (error != null) return error;
            foreach (var row in plan.Rows)
            {
                error = row.Blocker ?? RowError(plan, row);
                if (error != null) return error;
                if (row.Root.transform.parent != row.ExpectedParent) return "Source parent changed; rebuild preservation preview.";
            }
            return null;
        }

        internal static bool Apply(FPScenePreservationPlan plan, out string result, Action<int> afterMove = null)
        {
            string error;
            try { error = Preflight(plan); }
            catch (Exception ex) { result = "Moved 0; preflight failed: " + ex.Message; return false; }
            if (error != null) { result = "Moved 0; preflight blocked: " + error; return false; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup(), moved = 0;
            Undo.SetCurrentGroupName("Preserve Scene-specific Objects");
            try
            {
                foreach (var row in plan.Rows)
                {
                    if (row.Root == null || row.Root.transform.parent != row.ExpectedParent) throw new InvalidOperationException("Source hierarchy changed during preservation.");
                    Undo.SetTransformParent(row.Root.transform, plan.Parent == null ? null : plan.Parent.transform, "Preserve Scene-specific Objects");
                    PrefabUtility.RecordPrefabInstancePropertyModifications(row.Root.transform);
                    moved++;
                    afterMove?.Invoke(moved);
                }
                foreach (var row in plan.Rows)
                    if (row.Root == null || row.Root.transform.parent != (plan.Parent == null ? null : plan.Parent.transform) ||
                        row.Root.activeInHierarchy != row.ActiveInHierarchy || Vector3.Distance(row.Root.transform.position, row.WorldPosition) > 0.001f ||
                        Quaternion.Angle(row.Root.transform.rotation, row.WorldRotation) > 0.01f)
                        throw new InvalidOperationException("Moved subtree did not retain its expected parent, world position/rotation or activation.");
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(plan.Trace.Scene);
                plan.Applied = true;
                result = "Preserved " + moved + " subtree(s). References were retained; scene has not been saved. Review remaining dependencies on the old system.";
                return true;
            }
            catch (Exception ex)
            {
                Undo.FlushUndoRecordObjects(); Undo.RevertAllDownToGroup(group);
                plan.Applied = true;
                result = "Moved 0; rolled back " + moved + " move(s): " + ex.Message;
                return false;
            }
        }

        internal static string Report(FPScenePreservationPlan plan)
        {
            var report = new StringBuilder("# Scene-specific object preservation preview\n\n");
            report.AppendLine("Destination: " + (plan.Parent == null ? "Scene root" : FPSceneReferencePlanner.Describe(plan.Parent)));
            report.AppendLine("Complete scan: " + plan.Trace.Complete);
            foreach (string warning in plan.Trace.Warnings) report.AppendLine("- " + warning);
            foreach (var row in plan.Rows)
                report.AppendLine("- " + FPSceneReferencePlanner.Describe(row.Root) + " | " + row.ObjectCount + " GameObjects, " + row.ComponentCount +
                    " components | references to remaining old hierarchy: " + row.ReferencesToRemainingOld + " | " + (row.Blocker ?? "Ready"));
            report.AppendLine("\nMoves preserve references and world position/rotation. Review nonuniform scale/shear and all runtime dependencies. No copying, deletion, activation changes or saving is performed.");
            return report.ToString();
        }
    }
}
