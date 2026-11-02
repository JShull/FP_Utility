// Copyright (c) 2026 John B. Shull.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Playables;
    using UnityEngine.SceneManagement;
    using Object = UnityEngine.Object;

    internal enum FPSceneLinkCategory { Incoming, Outgoing, ReplacementToOld, Internal, Assets }

    internal sealed class FPSceneObjectMapping
    {
        internal Object Old, New;
        internal string Reason;
        internal bool Manual, Ignored, ReviewManyToOne;
    }

    internal sealed class FPSceneLink
    {
        internal Object Owner, Target, BindingKey;
        internal Type BindingType;
        internal string Path, EventInfo;
        internal FPSceneLinkCategory Category;
        internal bool IsBinding => BindingKey != null;
    }

    internal sealed class FPSceneReferenceEdit
    {
        internal FPSceneLink Source;
        internal Object Owner, Expected, Proposed;
        internal FPSceneObjectMapping Mapping;
        internal string Blocker;
        internal bool Selected, Transfer;
        internal bool Ready => string.IsNullOrEmpty(Blocker);
    }

    internal sealed class FPSceneReferencePlan
    {
        internal GameObject OldRoot, NewRoot;
        internal Scene Scene;
        internal bool Complete = true, HasPreview, Transfers;
        internal readonly List<Object> Inventory = new List<Object>();
        internal readonly List<FPSceneLink> Links = new List<FPSceneLink>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<FPSceneObjectMapping> Mappings = new List<FPSceneObjectMapping>();
        internal readonly List<FPSceneReferenceEdit> Edits = new List<FPSceneReferenceEdit>();
        internal readonly List<(Object Owner, string Json)> Snapshot = new List<(Object, string)>();
    }

    /// <summary>Live scene-only scan, correspondence and preview. Never mutates scene content.</summary>
    internal static class FPSceneReferencePlanner
    {
        internal static GameObject GameObjectOf(Object value) => value is GameObject go ? go : (value as Component)?.gameObject;
        internal static bool Inside(Object value, GameObject root)
        {
            var go = GameObjectOf(value);
            return go != null && root != null && (go == root || go.transform.IsChildOf(root.transform));
        }

        internal static string ValidateRoots(GameObject oldRoot, GameObject newRoot, bool requireNew)
        {
            if (oldRoot == null) return "Assign an old scene root.";
            var scene = oldRoot.scene;
            if (EditorUtility.IsPersistent(oldRoot) || !scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                return "Roots must belong to a normal loaded scene.";
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return "Close Prefab Mode before tracing or applying.";
            if (scene != SceneManager.GetActiveScene()) return "The old root must be in the active scene.";
            if (requireNew && newRoot == null) return "Assign a new scene root to build a preview.";
            if (newRoot == null) return null;
            if (EditorUtility.IsPersistent(newRoot) || newRoot.scene != scene) return "Both roots must be in the same active scene.";
            if (Inside(oldRoot, newRoot) || Inside(newRoot, oldRoot)) return "Roots must be distinct, non-overlapping hierarchies.";
            return null;
        }

        internal static IEnumerable<Object> SceneObjects(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                yield return t.gameObject;
                foreach (var c in t.GetComponents<Component>()) if (c != null) yield return c;
            }
        }

        internal static FPSceneReferencePlan Trace(GameObject oldRoot, GameObject newRoot = null, Func<bool> cancelled = null)
        {
            string error = ValidateRoots(oldRoot, newRoot, false);
            if (error != null) throw new InvalidOperationException(error);
            var plan = new FPSceneReferencePlan { OldRoot = oldRoot, NewRoot = newRoot, Scene = oldRoot.scene };
            plan.Warnings.Add("Scope: active scene objects only; other scenes, scene-global settings and runtime relationships are excluded. Assets are read-only context.");
            plan.Warnings.Add("PlayableDirector exposed-reference tables and asset-held clip/track references are unsupported; review them manually.");
            foreach (var owner in SceneObjects(plan.Scene))
            {
                if (cancelled != null && cancelled()) { plan.Complete = false; plan.Warnings.Add("Scan cancelled; results are incomplete."); break; }
                if (Inside(owner, oldRoot)) plan.Inventory.Add(owner);
                if (owner is GameObject go && go.GetComponents<Component>().Any(c => c == null))
                    plan.Warnings.Add("Missing script: " + Describe(go));
                try
                {
                    plan.Snapshot.Add((owner, EditorJsonUtility.ToJson(owner)));
                    using (var so = new SerializedObject(owner))
                    {
                        var p = so.GetIterator();
                        while (p.Next(true))
                        {
                            if (p.propertyType != SerializedPropertyType.ObjectReference || Structural(p.propertyPath, owner)) continue;
                            AddLink(plan, owner, p.objectReferenceValue, p.propertyPath, null, null, EventInfo(so, p.propertyPath));
                        }
                    }
                    if (owner is PlayableDirector director && director.playableAsset != null)
                    {
                        foreach (var outputs in director.playableAsset.outputs.Where(o => o.sourceObject != null).GroupBy(o => o.sourceObject))
                        {
                            var output = outputs.First();
                            bool ambiguous = outputs.Count() != 1;
                            if (ambiguous) plan.Warnings.Add("Ambiguous output binding key on " + Describe(director) + ": " + output.streamName);
                            AddLink(plan, owner, director.GetGenericBinding(output.sourceObject), output.streamName,
                                output.sourceObject, ambiguous ? null : output.outputTargetType, null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    plan.Complete = false;
                    plan.Warnings.Add("Unreadable " + Describe(owner) + ": " + ex.Message);
                }
            }
            if (newRoot != null) SuggestMappings(plan);
            return plan;
        }

        private static bool Structural(string path, Object owner)
        {
            string first = path.Split('.')[0];
            if (owner is PlayableDirector && (first == "m_SceneBindings" || first == "m_ExposedReferences")) return true;
            switch (first)
            {
                case "m_Script": case "m_GameObject": case "m_Component": case "m_Father": case "m_Children":
                case "m_CorrespondingSourceObject": case "m_PrefabInstance": case "m_PrefabAsset": case "m_RootOrder": return true;
                default: return false;
            }
        }

        private static void AddLink(FPSceneReferencePlan plan, Object owner, Object target, string path, Object key, Type bindingType, string eventInfo)
        {
            if (target == null) return;
            bool ownerOld = Inside(owner, plan.OldRoot), targetOld = Inside(target, plan.OldRoot);
            FPSceneLinkCategory category;
            if (EditorUtility.IsPersistent(target)) { if (!ownerOld) return; category = FPSceneLinkCategory.Assets; }
            else if (ownerOld && targetOld) category = FPSceneLinkCategory.Internal;
            else if (targetOld) category = Inside(owner, plan.NewRoot) ? FPSceneLinkCategory.ReplacementToOld : FPSceneLinkCategory.Incoming;
            else if (ownerOld && GameObjectOf(target) != null && GameObjectOf(target).scene == plan.Scene)
            {
                category = FPSceneLinkCategory.Outgoing;
            }
            else return;
            plan.Links.Add(new FPSceneLink { Owner = owner, Target = target, Path = path, BindingKey = key,
                BindingType = bindingType, Category = category, EventInfo = eventInfo });
        }

        // Length-prefixed segments avoid collisions from separator characters; duplicate paths remain ambiguous.
        private static string RelativeKey(Transform t, Transform root)
        {
            if (t == root) return "";
            string name = t.name;
            return RelativeKey(t.parent, root) + name.Length + ":" + name;
        }

        private static void SuggestMappings(FPSceneReferencePlan plan)
        {
            var oldPaths = plan.OldRoot.GetComponentsInChildren<Transform>(true).GroupBy(t => RelativeKey(t, plan.OldRoot.transform)).ToDictionary(g => g.Key, g => g.ToArray());
            var newPaths = plan.NewRoot.GetComponentsInChildren<Transform>(true).GroupBy(t => RelativeKey(t, plan.NewRoot.transform)).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var value in plan.Inventory)
            {
                var go = GameObjectOf(value);
                string key = RelativeKey(go.transform, plan.OldRoot.transform);
                bool found = newPaths.TryGetValue(key, out var targets);
                bool ambiguous = oldPaths[key].Length != 1 || (found && targets.Length != 1);
                // A duplicated ancestor invalidates every descendant even if its final full path is unique.
                for (var t = go.transform.parent; t != null && t != plan.OldRoot.transform.parent; t = t.parent)
                {
                    string ancestor = RelativeKey(t, plan.OldRoot.transform);
                    if (oldPaths[ancestor].Length != 1 || (newPaths.TryGetValue(ancestor, out var matches) && matches.Length != 1)) ambiguous = true;
                }
                var mapping = new FPSceneObjectMapping { Old = value, Reason = ambiguous ? "Ambiguous path" : "Missing path" };
                if (!ambiguous && found)
                {
                    var targetGO = targets[0].gameObject;
                    if (value is GameObject) { mapping.New = targetGO; mapping.Reason = "Suggested: exact unique path"; }
                    else
                    {
                        var type = value.GetType();
                        var sources = go.GetComponents<Component>().Where(c => c != null && c.GetType() == type).ToArray();
                        var candidates = targetGO.GetComponents<Component>().Where(c => c != null && c.GetType() == type).ToArray();
                        if (sources.Length == 1 && candidates.Length == 1) { mapping.New = candidates[0]; mapping.Reason = "Suggested: unique path and exact type"; }
                        else mapping.Reason = candidates.Length == 0 ? "Missing component" : "Ambiguous component type";
                    }
                }
                plan.Mappings.Add(mapping);
            }
        }

        internal static string MappingError(FPSceneReferencePlan plan, FPSceneObjectMapping mapping)
        {
            if (mapping == null || mapping.Ignored) return "Ignored or unmapped target.";
            if (mapping.New == null) return mapping.Reason;
            if (!Inside(mapping.Old, plan.OldRoot) || !Inside(mapping.New, plan.NewRoot)) return "Mapping must connect old and new hierarchy members.";
            if ((mapping.Old is GameObject) != (mapping.New is GameObject)) return "GameObjects and components cannot be interchanged.";
            if (!(mapping.New is GameObject) && !(mapping.New is Component)) return "Choose a scene GameObject or component.";
            if (plan.Mappings.Count(m => !m.Ignored && m.New == mapping.New) > 1 && !mapping.ReviewManyToOne)
                return "Many-to-one mapping requires explicit review.";
            return null;
        }

        internal static void BuildPreview(FPSceneReferencePlan plan, bool transfers)
        {
            string error = ValidateRoots(plan.OldRoot, plan.NewRoot, true);
            if (error != null) throw new InvalidOperationException(error);
            plan.Edits.Clear(); plan.HasPreview = true; plan.Transfers = transfers;
            foreach (var link in plan.Links)
            {
                bool transfer = link.Category == FPSceneLinkCategory.Outgoing;
                if (link.Category != FPSceneLinkCategory.Incoming && link.Category != FPSceneLinkCategory.ReplacementToOld && !(transfer && transfers)) continue;
                var mapping = plan.Mappings.FirstOrDefault(m => m.Old == (transfer ? link.Owner : link.Target));
                var edit = new FPSceneReferenceEdit { Source = link, Mapping = mapping, Transfer = transfer,
                    Owner = transfer ? mapping?.New : link.Owner, Proposed = transfer ? link.Target : mapping?.New,
                    Expected = transfer ? null : link.Target };
                edit.Blocker = MappingError(plan, mapping);
                if (edit.Blocker == null)
                {
                    if (transfer && link.EventInfo != null) edit.Blocker = "Manual Transfer Required: UnityEvents need structural listener migration.";
                    else if (link.IsBinding)
                    {
                        if (!(edit.Owner is PlayableDirector destination) || !BindingMatches(destination, link))
                            edit.Blocker = "Destination director does not expose one unambiguous compatible binding key.";
                        else edit.Expected = destination.GetGenericBinding(link.BindingKey);
                    }
                    else if (edit.Owner != null)
                    {
                        using (var so = new SerializedObject(edit.Owner))
                        {
                            var p = so.FindProperty(link.Path);
                            if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) edit.Blocker = "Destination field/collection slot is absent or has changed layout.";
                            else edit.Expected = p.objectReferenceValue;
                        }
                    }
                }
                if (edit.Blocker == null) edit.Blocker = Compatibility(edit);
                // Outgoing and replacement-to-old edits always require individual opt-in.
                edit.Selected = edit.Ready && !transfer && link.Category == FPSceneLinkCategory.Incoming;
                plan.Edits.Add(edit);
            }
        }

        internal static string Compatibility(FPSceneReferenceEdit edit)
        {
            if (edit.Owner == null || edit.Proposed == null || EditorUtility.IsPersistent(edit.Owner)) return "Missing scene owner or proposed value.";
            if (edit.Source.IsBinding)
                return edit.Owner is PlayableDirector director && BindingMatches(director, edit.Source) && edit.Source.BindingType != null && edit.Source.BindingType.IsInstanceOfType(edit.Proposed) ? null : "Incompatible, ambiguous or unknown Timeline output binding type.";
            using (var so = new SerializedObject(edit.Owner))
            {
                var p = so.FindProperty(edit.Source.Path);
                if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || !p.editable || Structural(p.propertyPath, edit.Owner))
                    return "Property is missing, structural or read-only.";
                Type type = PropertyType(edit.Owner.GetType(), p.propertyPath);
                if (type == null)
                {
                    // Native/managed-reference layouts without accessible field metadata are deliberately conservative.
                    var baseline = edit.Transfer ? edit.Source.Target : edit.Expected;
                    if (baseline == null || !baseline.GetType().IsInstanceOfType(edit.Proposed)) return "Cannot establish receiving property type compatibility.";
                }
                else if (!type.IsInstanceOfType(edit.Proposed)) return "Replacement is incompatible with the receiving field type.";
                if (edit.Transfer && p.propertyPath.Contains(".Array.data[") && !SameCollectionSizes(edit))
                    return "Collection sizes/layouts differ; transfer manually.";
                return ValidateEvent(so, p.propertyPath, edit.Proposed);
            }
        }

        internal static bool BindingMatches(PlayableDirector director, FPSceneLink link)
        {
            if (director == null || director.playableAsset == null || link.BindingKey == null || link.BindingType == null) return false;
            var outputs = director.playableAsset.outputs.Where(o => o.sourceObject == link.BindingKey).ToArray();
            return outputs.Length == 1 && outputs[0].outputTargetType == link.BindingType;
        }

        private static bool SameCollectionSizes(FPSceneReferenceEdit edit)
        {
            using (var source = new SerializedObject(edit.Source.Owner))
            using (var dest = new SerializedObject(edit.Owner))
            {
                string path = edit.Source.Path;
                int index = 0;
                while ((index = path.IndexOf(".Array.data[", index, StringComparison.Ordinal)) >= 0)
                {
                    string prefix = path.Substring(0, index);
                    var a = source.FindProperty(prefix); var b = dest.FindProperty(prefix);
                    if (a == null || b == null || !a.isArray || !b.isArray || a.arraySize != b.arraySize) return false;
                    index += 12;
                }
                return true;
            }
        }

        // Reflection reads declared field types only; reference discovery stays within Unity serialization.
        private static Type PropertyType(Type type, string path)
        {
            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "Array" && i + 1 < parts.Length && parts[i + 1].StartsWith("data[", StringComparison.Ordinal))
                {
                    type = type.IsArray ? type.GetElementType() : type.IsGenericType ? type.GetGenericArguments().FirstOrDefault() : null;
                    i++;
                }
                else
                {
                    FieldInfo field = null;
                    for (var t = type; t != null && field == null; t = t.BaseType)
                        field = t.GetField(parts[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    type = field?.FieldType;
                }
                if (type == null) return null;
            }
            return type;
        }

        private static string CallPath(string path)
        {
            int marker = path.IndexOf(".m_PersistentCalls.m_Calls.Array.data[", StringComparison.Ordinal);
            if (marker < 0) return null;
            int end = path.IndexOf(']', marker);
            return end < 0 ? null : path.Substring(0, end + 1);
        }

        private static string EventInfo(SerializedObject so, string path)
        {
            string call = CallPath(path);
            return call == null ? null : "UnityEvent: " + so.FindProperty(call + ".m_MethodName")?.stringValue +
                " | call state " + so.FindProperty(call + ".m_CallState")?.intValue;
        }

        private static string ValidateEvent(SerializedObject so, string path, Object proposed)
        {
            string call = CallPath(path);
            if (call == null) return null;
            var target = so.FindProperty(call + ".m_Target")?.objectReferenceValue;
            bool targetChange = path == call + ".m_Target";
            if (targetChange) target = proposed;
            string method = so.FindProperty(call + ".m_MethodName")?.stringValue;
            var modeProp = so.FindProperty(call + ".m_Mode");
            if (target == null || string.IsNullOrEmpty(method) || modeProp == null) return "UnityEvent target/method cannot be validated.";
            Type[] args;
            switch ((PersistentListenerMode)modeProp.intValue)
            {
                case PersistentListenerMode.Void: args = Type.EmptyTypes; break;
                case PersistentListenerMode.Int: args = new[] { typeof(int) }; break;
                case PersistentListenerMode.Float: args = new[] { typeof(float) }; break;
                case PersistentListenerMode.String: args = new[] { typeof(string) }; break;
                case PersistentListenerMode.Bool: args = new[] { typeof(bool) }; break;
                case PersistentListenerMode.Object:
                    string name = so.FindProperty(call + ".m_Arguments.m_ObjectArgumentAssemblyTypeName")?.stringValue;
                    var arg = string.IsNullOrEmpty(name) ? typeof(Object) : Type.GetType(name, false);
                    if (arg == null) return "UnityEvent object argument type cannot be resolved.";
                    var value = path.EndsWith(".m_ObjectArgument", StringComparison.Ordinal) ? proposed : so.FindProperty(call + ".m_Arguments.m_ObjectArgument")?.objectReferenceValue;
                    if (value != null && !arg.IsInstanceOfType(value)) return "UnityEvent object argument is incompatible.";
                    args = new[] { arg }; break;
                case PersistentListenerMode.EventDefined:
                    string eventPath = path.Substring(0, path.IndexOf(".m_PersistentCalls", StringComparison.Ordinal));
                    var eventType = PropertyType(so.targetObject.GetType(), eventPath);
                    if (eventType == null) return "Dynamic UnityEvent signature cannot be resolved; review manually.";
                    var invoke = eventType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public);
                    if (invoke == null) return "Dynamic UnityEvent signature cannot be resolved.";
                    args = invoke.GetParameters().Select(p => p.ParameterType).ToArray(); break;
                default: return "Unsupported UnityEvent invocation mode.";
            }
            var valid = UnityEventBase.GetValidMethodInfo(target, method, args);
            return valid != null && valid.ReturnType == typeof(void) && !valid.IsStatic ? null : "Replacement does not support the persisted UnityEvent method/signature.";
        }

        internal static string StaleReason(FPSceneReferencePlan plan)
        {
            string error = ValidateRoots(plan.OldRoot, plan.NewRoot, plan.HasPreview);
            if (error != null) return error;
            var current = SceneObjects(plan.Scene).ToArray();
            if (current.Length != plan.Snapshot.Count) return "Scene inventory changed; trace again.";
            for (int i = 0; i < current.Length; i++)
                if (current[i] != plan.Snapshot[i].Owner || EditorJsonUtility.ToJson(current[i]) != plan.Snapshot[i].Json)
                    return "Scene hierarchy or serialized state changed; trace again.";
            return null;
        }

        internal static string Describe(Object value)
        {
            if (value == null) return "(none)";
            var go = GameObjectOf(value);
            if (go == null) return value.name + " (" + value.GetType().Name + ")";
            var parts = new List<string>();
            for (var t = go.transform; t != null; t = t.parent)
                parts.Add(t.name.Replace("\\", "\\\\").Replace("/", "\\/").Replace("[", "\\[").Replace("]", "\\]").Replace("\r", "\\r").Replace("\n", "\\n") + "[" + t.GetSiblingIndex() + "]");
            parts.Reverse();
            string component = value is Component c ? " / " + c.GetType().Name + "[" + Array.IndexOf(go.GetComponents(c.GetType()), c) + "]" : " / GameObject";
            return go.scene.name + ":" + string.Join("/", parts) + component;
        }

        internal static string Report(FPSceneReferencePlan plan)
        {
            var report = new StringBuilder("# Scene Reference Replacer audit\n\n");
            report.AppendLine("Scope: active scene `" + plan.Scene.name + "`. Complete: " + plan.Complete + ". Inventory: " + plan.Inventory.Count + ".\n");
            foreach (string warning in plan.Warnings) report.AppendLine("- " + warning);
            report.AppendLine("\n## Inventory\n");
            foreach (var item in plan.Inventory) report.AppendLine("- " + Describe(item));
            foreach (FPSceneLinkCategory category in Enum.GetValues(typeof(FPSceneLinkCategory)))
            {
                report.AppendLine("\n## " + category + "\n");
                foreach (var link in plan.Links.Where(l => l.Category == category))
                    report.AppendLine("- " + Describe(link.Owner) + " → `" + link.Path + "` → " + Describe(link.Target) + " " + link.EventInfo + (link.IsBinding ? " | binding key: " + Describe(link.BindingKey) : ""));
            }
            report.AppendLine("\n## Proposed edits\n");
            foreach (var edit in plan.Edits)
                report.AppendLine("- " + (edit.Selected ? "[x] " : "[ ] ") + Describe(edit.Owner) + " / `" + edit.Source.Path + "`: " +
                    Describe(edit.Expected) + " → " + Describe(edit.Proposed) + " | " + (edit.Blocker ?? "Ready") + " | " + edit.Mapping?.Reason);
            return report.ToString();
        }
    }
}
