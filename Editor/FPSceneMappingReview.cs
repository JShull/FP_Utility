// Copyright (c) 2026 John B. Shull. GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal sealed class FPSceneMappingCandidate
    {
        internal Object Target;
        internal string Reason, Blocker;
    }

    /// <summary>Review aids only. A compatible signature does not establish the same semantic role.</summary>
    internal static class FPSceneMappingReview
    {
        internal static IEnumerable<FPSceneLink> IncomingUses(FPSceneReferencePlan plan, Object target) =>
            plan.Links.Where(l => l.Target == target && (l.Category == FPSceneLinkCategory.Incoming || l.Category == FPSceneLinkCategory.ReplacementToOld));

        internal static string Diagnosis(FPSceneReferencePlan plan, FPSceneObjectMapping mapping)
        {
            if (mapping == null || mapping.Old == null) return "Old target is unavailable; trace again.";
            if (mapping.Ignored) return "Ignored by explicit mapping choice; references remain unchanged.";
            if (mapping.New != null) return FPSceneReferencePlanner.MappingError(plan, mapping) ?? mapping.Reason;
            if (plan.NewRoot == null) return "Assign a new root to inspect replacement candidates.";
            if (mapping.Old is Component component)
            {
                plan.NewComponentTypeCounts.TryGetValue(component.GetType(), out int count);
                return (mapping.Reason ?? "Unmapped") + ". " + (count == 0
                    ? "Component type " + component.GetType().Name + " is absent from the replacement hierarchy. Functionality may need to be implemented or the old scene-specific object preserved."
                    : count + " component(s) of type " + component.GetType().Name + " exist elsewhere in the replacement hierarchy. A path may be moved, renamed or ambiguous; review candidates without assuming the same role.");
            }
            return (mapping.Reason ?? "Unmapped") + ". GameObject names and paths are matching aids. Choose an explicit replacement or review preserving this subtree.";
        }

        internal static List<FPSceneMappingCandidate> FindCandidates(FPSceneReferencePlan plan, FPSceneObjectMapping mapping)
        {
            var result = new List<FPSceneMappingCandidate>();
            if (plan.NewRoot == null || mapping?.Old == null) return result;
            var uses = IncomingUses(plan, mapping.Old).ToArray();
            bool hasCallbacks = uses.Any(l => l.EventInfo != null);
            IEnumerable<Object> pool = mapping.Old is GameObject
                ? plan.NewRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == mapping.Old.name).Select(t => (Object)t.gameObject)
                : plan.NewRoot.GetComponentsInChildren<Component>(true).Where(c => c != null &&
                    (c.GetType() == mapping.Old.GetType() || (hasCallbacks && !(c is Transform)))).Cast<Object>();
            foreach (var target in pool)
            {
                bool exactType = target.GetType() == mapping.Old.GetType();
                string blocker = null;
                foreach (var use in uses)
                {
                    blocker = FPSceneReferencePlanner.Compatibility(new FPSceneReferenceEdit
                    { Source = use, Owner = use.Owner, Expected = use.Target, Proposed = target });
                    if (blocker != null) break;
                }
                // Incompatible exact-type matches explain why a familiar-looking candidate cannot be used.
                if (!exactType && blocker != null) continue;
                result.Add(new FPSceneMappingCandidate { Target = target, Blocker = blocker,
                    Reason = (exactType ? "Exact object/component type" : "Different component type with compatible persisted callbacks") +
                        "; " + uses.Length + " incoming reference(s) checked. Role and outgoing configuration still require review." });
            }
            return result.OrderBy(c => c.Blocker != null).ThenBy(c => c.Target.GetType() != mapping.Old.GetType())
                .ThenBy(c => FPSceneReferencePlanner.Describe(c.Target), System.StringComparer.Ordinal).ToList();
        }
    }
}
