// Copyright (c) 2026 John B. Shull. GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Events;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.SceneManagement;
    using Object = UnityEngine.Object;
    using FuzzPhyte.Utility.Tests;

    public sealed class FPSceneReferencePlannerTests
    {
        private Scene previous, scene;
        private GameObject oldRoot, newRoot;
        private FPSceneReferenceTestOwner outside, oldOwner, newOwner;
        private readonly List<Object> temporary = new List<Object>();
        private string folder;

        [SetUp]
        public void SetUp()
        {
            previous = SceneManager.GetActiveScene();
            folder = "Assets/__FPSceneReferenceTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var fixture = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("FPSceneReferenceEmpty t:Scene").Single());
            string scenePath = folder + "/Scene.unity";
            Assert.That(AssetDatabase.CopyAsset(fixture, scenePath), Is.True);
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            oldRoot = Create("Old"); newRoot = Create("New");
            oldOwner = oldRoot.AddComponent<FPSceneReferenceTestOwner>();
            newOwner = newRoot.AddComponent<FPSceneReferenceTestOwner>();
            outside = Create("Outside").AddComponent<FPSceneReferenceTestOwner>();
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (var o in FPSceneReferencePlanner.SceneObjects(scene)) Undo.ClearUndo(o);
                EditorSceneManager.CloseScene(scene, true);
            }
            SceneManager.SetActiveScene(previous);
            foreach (var item in temporary) if (item != null) Object.DestroyImmediate(item);
            temporary.Clear();
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }

        private GameObject Create(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent != null) go.transform.SetParent(parent.transform);
            return go;
        }
        private FPSceneReferencePlan Preview(bool transfers = false)
        {
            var plan = FPSceneReferencePlanner.Trace(oldRoot, newRoot);
            Assert.That(plan.Complete, Is.True, string.Join("\n", plan.Warnings));
            FPSceneReferencePlanner.BuildPreview(plan, transfers);
            return plan;
        }

        [Test]
        public void Window_UnrelatedNotificationsKeepPreviewReady_RealChangesInvalidate()
        {
            outside.Target = oldOwner;
            var plan = Preview();
            var window = ScriptableObject.CreateInstance<FPSceneReferenceReplaceEditorWindow>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = window.GetType();
            try
            {
                type.GetField("plan", flags).SetValue(window, plan);
                type.GetMethod("Invalidate", flags).Invoke(window, null);
                type.GetMethod("ValidatePendingChanges", flags).Invoke(window, null);
                Assert.That(type.GetField("stale", flags).GetValue(window), Is.EqualTo(false));
                using (var so = new SerializedObject(outside))
                {
                    so.FindProperty("Target").objectReferenceValue = newOwner;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                type.GetMethod("Invalidate", flags).Invoke(window, null);
                type.GetMethod("ValidatePendingChanges", flags).Invoke(window, null);
                Assert.That(type.GetField("stale", flags).GetValue(window), Is.EqualTo(true));
                Assert.That(FPSceneReferenceApply.Preflight(plan), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(window); }
        }
        [Test]
        public void DirectorAudit_ReportsExposedAndAssetFieldsPerDirectorWithoutEdits()
        {
            var asset = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(asset);
            asset.SceneTarget = new ExposedReference<GameObject> { exposedName = new PropertyName("scene-target") };
            asset.DirectTarget = oldOwner;
            var a = outside.gameObject.AddComponent<PlayableDirector>(); a.playableAsset = asset;
            var b = newRoot.AddComponent<PlayableDirector>(); b.playableAsset = asset;
            a.SetReferenceValue(new PropertyName("scene-target"), oldRoot);
            b.SetReferenceValue(new PropertyName("scene-target"), newRoot);
            bool dirty = scene.isDirty;
            var plan = Preview();
            Assert.That(plan.AuditedDirectors, Is.EqualTo(2));
            Assert.That(plan.DirectorAuditFailures, Is.Zero);
            var exposed = plan.DirectorReferences.Where(r => r.Kind == "Exposed reference").ToArray();
            Assert.That(exposed.Length, Is.EqualTo(2));
            Assert.That(exposed.Single(r => r.Director == a).Target, Is.EqualTo(oldRoot));
            Assert.That(exposed.Single(r => r.Director == b).Target, Is.EqualTo(newRoot));
            Assert.That(exposed.Single(r => r.Director == a).TargetsOld, Is.True);
            Assert.That(plan.DirectorReferences.Any(r => r.Location.Contains("DirectTarget") && r.Target == oldOwner), Is.True);
            Assert.That(FPSceneReferencePlanner.Report(plan), Does.Contain("scene-target"));
            Assert.That(scene.isDirty, Is.EqualTo(dirty));
            Assert.That(plan.Edits.Any(e => e.Source.Path.Contains("SceneTarget")), Is.False);
        }
        [Test]
        public void Trace_HiddenNestedInactiveAndCategories_AreReadOnly()
        {
            var child = Create("Child", oldRoot).AddComponent<BoxCollider>();
            Create("Child", newRoot).AddComponent<BoxCollider>();
            outside.gameObject.SetActive(false); child.gameObject.SetActive(false);
            outside.HiddenTarget = child; outside.Nested = new FPSceneReferenceTestNested { Target = child };
            oldOwner.Target = outside; oldOwner.HiddenTarget = child; newOwner.Target = child;
            bool wasDirty = scene.isDirty;
            var plan = Preview();
            Assert.That(plan.Inventory, Does.Contain(child));
            Assert.That(plan.Links.Any(l => l.Owner == outside && l.Path == "HiddenTarget" && l.Target == child), Is.True);
            Assert.That(plan.Links.Any(l => l.Owner == outside && l.Path == "Nested.Target"), Is.True);
            foreach (var category in new[] { FPSceneLinkCategory.Incoming, FPSceneLinkCategory.Outgoing, FPSceneLinkCategory.Internal, FPSceneLinkCategory.ReplacementToOld })
                Assert.That(plan.Links.Any(l => l.Category == category), Is.True, category.ToString());
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
            Assert.That(plan.Links.Any(l => l.Path == "m_GameObject" || l.Path.StartsWith("m_Children")), Is.False);
            Assert.That(FPSceneReferencePlanner.Report(plan), Does.Contain("HiddenTarget"));
        }

        [Test]
        public void Apply_SelectedIncomingOnly_SupportsUndoRedoAndAudit()
        {
            var oldChild = Create("Child", oldRoot).AddComponent<BoxCollider>();
            var newChild = Create("Child", newRoot).AddComponent<BoxCollider>();
            outside.Target = oldChild; outside.HiddenTarget = oldChild; newOwner.Target = oldChild;
            var plan = Preview();
            plan.Edits.Single(e => e.Source.Path == "HiddenTarget").Selected = false;
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(outside.Target, Is.EqualTo(newChild));
            Assert.That(outside.HiddenTarget, Is.EqualTo(oldChild));
            Assert.That(newOwner.Target, Is.EqualTo(oldChild));
            Undo.PerformUndo(); Assert.That(outside.Target, Is.EqualTo(oldChild));
            Undo.PerformRedo(); Assert.That(outside.Target, Is.EqualTo(newChild));
            var audit = FPSceneReferencePlanner.Trace(oldRoot, newRoot);
            Assert.That(audit.Links.Count(l => l.Category == FPSceneLinkCategory.Incoming), Is.EqualTo(1));
            Assert.That(audit.Links.Count(l => l.Category == FPSceneLinkCategory.ReplacementToOld), Is.EqualTo(1));
        }

        [Test]
        public void Mappings_DuplicateAncestorsRepeatedComponentsAndTransformKinds_AreNotGuessed()
        {
            var a = Create("Duplicate", oldRoot); Create("Duplicate", oldRoot); var leaf = Create("UniqueLeaf", a);
            var b = Create("Duplicate", newRoot); Create("UniqueLeaf", b);
            var repeated = Create("Repeated", oldRoot); repeated.AddComponent<BoxCollider>(); repeated.AddComponent<BoxCollider>();
            Create("Repeated", newRoot).AddComponent<BoxCollider>();
            var rect = Create("Rect", oldRoot);
            var otherRect = Create("Rect", newRoot); Object.DestroyImmediate(otherRect); otherRect = new GameObject("Rect", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(otherRect, scene); otherRect.transform.SetParent(newRoot.transform);
            var plan = Preview();
            Assert.That(plan.Mappings.Single(m => m.Old == leaf).New, Is.Null);
            Assert.That(plan.Mappings.Where(m => m.Old is BoxCollider).All(m => m.New == null), Is.True);
            Assert.That(plan.Mappings.Single(m => m.Old == rect.transform).New, Is.Null);
            Assert.That(plan.Mappings.Single(m => m.Old == oldRoot).New, Is.EqualTo(newRoot));
        }

        [Test]
        public void ManualMapping_RenamedAndChangedTypes_ValidatesReceivingField()
        {
            var oldChild = Create("Original", oldRoot).AddComponent<BoxCollider>();
            var renamed = Create("Renamed", newRoot).AddComponent<SphereCollider>();
            outside.Target = oldChild; outside.ColliderTarget = oldChild;
            var plan = Preview();
            var mapping = plan.Mappings.Single(m => m.Old == oldChild);
            mapping.New = renamed; mapping.Manual = true;
            FPSceneReferencePlanner.BuildPreview(plan, false);
            Assert.That(plan.Edits.Single(e => e.Source.Path == "Target").Ready, Is.True);
            Assert.That(plan.Edits.Single(e => e.Source.Path == "ColliderTarget").Ready, Is.False);
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(outside.Target, Is.EqualTo(renamed)); Assert.That(outside.ColliderTarget, Is.EqualTo(oldChild));
        }

        [Test]
        public void ManyToOneAndOldHierarchyMappings_RequireReviewOrReject()
        {
            var a = Create("A", oldRoot); var b = Create("B", oldRoot);
            var plan = Preview();
            var ma = plan.Mappings.Single(m => m.Old == a); var mb = plan.Mappings.Single(m => m.Old == b);
            ma.New = newRoot; mb.New = newRoot;
            Assert.That(FPSceneReferencePlanner.MappingError(plan, ma), Does.Contain("Many-to-one"));
            ma.ReviewManyToOne = true;
            Assert.That(FPSceneReferencePlanner.MappingError(plan, ma), Is.Null);
            ma.New = b;
            Assert.That(FPSceneReferencePlanner.MappingError(plan, ma), Does.Contain("hierarchy"));
        }

        [TestCase(false)] [TestCase(true)]
        public void StalePreview_BlocksEntireSelectionBeforeWriting(bool hierarchyChange)
        {
            outside.Target = oldOwner; outside.HiddenTarget = oldRoot;
            var plan = Preview();
            if (hierarchyChange) Create("Added", newRoot); else outside.HiddenTarget = newRoot;
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.False);
            Assert.That(outside.Target, Is.EqualTo(oldOwner));
            Assert.That(result, Does.Contain("Changed 0"));
        }

        [Test]
        public void UnexpectedFailure_RollsBackAllToolWrites()
        {
            outside.Target = oldOwner; outside.HiddenTarget = oldRoot;
            var plan = Preview();
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result, count => throw new InvalidOperationException("injected failure")), Is.False);
            Assert.That(result, Does.Contain("Rolled back"));
            Assert.That(outside.Target, Is.EqualTo(oldOwner)); Assert.That(outside.HiddenTarget, Is.EqualTo(oldRoot));
        }

        [Test]
        public void Outgoing_ScalarsAndExistingSlots_AreOptInAndNeverCopyEventListeners()
        {
            oldOwner.Target = outside; oldOwner.Items.Add(outside); newOwner.Items.Add(null);
            newOwner.Target = outside.transform;
            oldOwner.ConfigurationValue = 10; newOwner.ConfigurationValue = 20;
            UnityEventTools.AddPersistentListener(oldOwner.Callback, outside.Receive);
            UnityEventTools.AddPersistentListener(newOwner.Callback, newOwner.Receive);
            var plan = Preview(true);
            Assert.That(plan.Edits.Where(e => e.Transfer).All(e => !e.Selected), Is.True);
            Assert.That(plan.Edits.Single(e => e.Transfer && e.Source.Path == "Target").Expected, Is.EqualTo(outside.transform));
            var eventEdit = plan.Edits.Single(e => e.Source.EventInfo != null);
            Assert.That(eventEdit.Blocker, Does.Contain("Manual Transfer Required"));
            foreach (var edit in plan.Edits.Where(e => e.Transfer && e.Ready)) edit.Selected = true;
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(newOwner.Target, Is.EqualTo(outside)); Assert.That(newOwner.Items[0], Is.EqualTo(outside));
            Assert.That(newOwner.ConfigurationValue, Is.EqualTo(20));
            Assert.That(newOwner.Callback.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(newOwner.Callback.GetPersistentTarget(0), Is.EqualTo(newOwner));
        }

        [Test]
        public void Outgoing_AlreadyMatchingSelection_DoesNotDirtySceneOrCreateUndoGroup()
        {
            oldOwner.Target = outside; newOwner.Target = outside;
            Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
            var plan = Preview(true);
            plan.Edits.Single(e => e.Transfer).Selected = true;
            int group = Undo.GetCurrentGroup();
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(result, Does.Contain("Changed 0; skipped 1"));
            Assert.That(scene.isDirty, Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group));
        }

        [Test]
        public void Incoming_ManualRepeatedComponentMapping_ChoosesExplicitInstance()
        {
            oldRoot.AddComponent<BoxCollider>();
            var oldSecond = oldRoot.AddComponent<BoxCollider>();
            var newFirst = newRoot.AddComponent<BoxCollider>();
            newRoot.AddComponent<BoxCollider>();
            outside.ColliderTarget = oldSecond;
            var plan = Preview();
            Assert.That(plan.Edits.Single().Ready, Is.False);
            var mapping = plan.Mappings.Single(m => m.Old == oldSecond);
            mapping.New = newFirst; mapping.Manual = true;
            FPSceneReferencePlanner.BuildPreview(plan, false);
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(outside.ColliderTarget, Is.EqualTo(newFirst));
        }

        [Test]
        public void ActiveSceneScope_LeavesOtherLoadedSceneReferencesUntouched()
        {
            string path = folder + "/Other.unity";
            var fixture = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("FPSceneReferenceEmpty t:Scene").Single());
            Assert.That(AssetDatabase.CopyAsset(fixture, path), Is.True);
            var otherScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var foreign = Create("Foreign").AddComponent<FPSceneReferenceTestOwner>();
                SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
                foreign.Target = oldOwner;
                outside.Target = oldOwner;
                SceneManager.SetActiveScene(scene);
                var plan = Preview();
                Assert.That(plan.Links.All(l => l.Owner != foreign), Is.True);
                Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
                Assert.That(outside.Target, Is.EqualTo(newOwner));
                Assert.That(foreign.Target, Is.EqualTo(oldOwner));
                Assert.That(FPSceneReferencePlanner.ValidateRoots(oldRoot, foreign.gameObject, true), Is.Not.Null);
            }
            finally { EditorSceneManager.CloseScene(otherScene, true); }
        }

        [Test]
        public void Menu_OpensWithoutSelection()
        {
            var selected = Selection.objects;
            var existing = Resources.FindObjectsOfTypeAll<FPSceneReferenceReplaceEditorWindow>();
            try
            {
                Selection.objects = new Object[0];
                Assert.That(EditorApplication.ExecuteMenuItem("FuzzPhyte/Utility/Scene/Scene Reference Replacer"), Is.True);
                Assert.That(Resources.FindObjectsOfTypeAll<FPSceneReferenceReplaceEditorWindow>().Length, Is.GreaterThan(0));
            }
            finally
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<FPSceneReferenceReplaceEditorWindow>().Except(existing)) window.Close();
                Selection.objects = selected;
            }
        }

        [Test]
        public void Outgoing_DifferentCollectionSize_IsUnresolved()
        {
            oldOwner.Items.Add(outside); oldOwner.Items.Add(outside); newOwner.Items.Add(null);
            var plan = Preview(true);
            Assert.That(plan.Edits.Where(e => e.Transfer).All(e => !e.Ready), Is.True);
        }

        [Test]
        public void Outgoing_ManyOwnersToOneDestination_BlocksCollidingSelection()
        {
            var other = Create("Other", oldRoot).AddComponent<FPSceneReferenceTestOwner>();
            oldOwner.Target = outside;
            other.Target = outside.gameObject;
            outside.HiddenTarget = oldRoot;
            var plan = Preview(true);
            var mapping = plan.Mappings.Single(m => m.Old == other);
            mapping.New = newOwner; mapping.Manual = true; mapping.ReviewManyToOne = true;
            plan.Mappings.Single(m => m.Old == oldOwner).ReviewManyToOne = true;
            FPSceneReferencePlanner.BuildPreview(plan, true);
            foreach (var edit in plan.Edits.Where(e => e.Transfer)) edit.Selected = true;
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.False);
            Assert.That(result, Does.Contain("same destination"));
            Assert.That(newOwner.Target, Is.Null);
            Assert.That(outside.HiddenTarget, Is.EqualTo(oldRoot), "Atomic preflight must also preserve incoming fields.");
            plan.Edits.Single(e => e.Transfer && e.Source.Owner == other).Selected = false;
            Assert.That(FPSceneReferenceApply.Apply(plan, out result), Is.True, result);
            Assert.That(newOwner.Target, Is.EqualTo(outside));
        }

        [Test]
        public void Outgoing_ChangedCollectionTypeWithEqualSize_IsUnresolved()
        {
            oldOwner.Items.Add(outside);
            var arrayOwner = newRoot.AddComponent<FPSceneReferenceArrayTestOwner>();
            arrayOwner.Items = new Object[1];
            var plan = Preview(true);
            var mapping = plan.Mappings.Single(m => m.Old == oldOwner);
            mapping.New = arrayOwner; mapping.Manual = true;
            FPSceneReferencePlanner.BuildPreview(plan, true);
            Assert.That(plan.Edits.Single(e => e.Transfer).Ready, Is.False);
            Assert.That(arrayOwner.Items[0], Is.Null);
        }

        [Test]
        public void Outgoing_UnknownEmptyNativeField_DoesNotAssumeSourceType()
        {
            oldOwner.m_ConnectedBody = outside;
            var joint = newRoot.AddComponent<FixedJoint>();
            var plan = Preview(true);
            var mapping = plan.Mappings.Single(m => m.Old == oldOwner);
            mapping.New = joint; mapping.Manual = true;
            FPSceneReferencePlanner.BuildPreview(plan, true);
            Assert.That(plan.Edits.Single(e => e.Transfer).Ready, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void Apply_CallbackChangingAnotherSelectedField_RollsBack(bool changeEarlierField)
        {
            outside.Target = oldOwner; outside.HiddenTarget = oldRoot;
            var plan = Preview();
            var first = plan.Edits.First(e => e.Selected);
            var second = plan.Edits.Last(e => e.Selected);
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result, count =>
            {
                if (count != (changeEarlierField ? 2 : 1)) return;
                var edit = changeEarlierField ? first : second;
                using (var so = new SerializedObject(edit.Owner))
                {
                    so.FindProperty(edit.Source.Path).objectReferenceValue = newRoot;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }), Is.False);
            Assert.That(result, Does.Contain("Rolled back"));
            Assert.That(outside.Target, Is.EqualTo(oldOwner));
            Assert.That(outside.HiddenTarget, Is.EqualTo(oldRoot));
        }

        [TestCase(false)] [TestCase(true)]
        public void Outgoing_TimelineTransfersOnlyIdenticalPlayableAsset(bool differentAsset)
        {
            var key = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(key);
            var asset = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(asset); asset.Key = key;
            var oldDirector = oldRoot.AddComponent<PlayableDirector>(); oldDirector.playableAsset = asset;
            var newDirector = newRoot.AddComponent<PlayableDirector>(); newDirector.playableAsset = asset;
            if (differentAsset)
            {
                var otherAsset = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(otherAsset);
                otherAsset.Key = key; newDirector.playableAsset = otherAsset;
            }
            oldDirector.SetGenericBinding(key, outside.gameObject);
            newDirector.SetGenericBinding(key, newRoot);
            var plan = Preview(true);
            var edit = plan.Edits.Single(e => e.Transfer);
            Assert.That(edit.Ready, Is.EqualTo(!differentAsset));
            Assert.That(edit.Selected, Is.False);
            if (differentAsset) return;
            edit.Selected = true;
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(newDirector.GetGenericBinding(key), Is.EqualTo(outside.gameObject));
            Assert.That(oldDirector.GetGenericBinding(key), Is.EqualTo(outside.gameObject));
            Undo.PerformUndo(); Assert.That(newDirector.GetGenericBinding(key), Is.EqualTo(newRoot));
            Undo.PerformRedo(); Assert.That(newDirector.GetGenericBinding(key), Is.EqualTo(outside.gameObject));
        }

        [Test]
        public void IncomingEvents_ValidateMethodsAndPreserveListenerData()
        {
            UnityEventTools.AddPersistentListener(outside.Callback, oldOwner.Receive);
            UnityEventTools.AddObjectPersistentListener(outside.ObjectCallback, outside.ReceiveObject, oldOwner);
            var plan = Preview();
            Assert.That(plan.Edits.Where(e => e.Source.EventInfo != null).All(e => e.Ready), Is.True);
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(outside.Callback.GetPersistentTarget(0), Is.EqualTo(newOwner));
            Assert.That(outside.Callback.GetPersistentMethodName(0), Is.EqualTo("Receive"));
            using (var so = new SerializedObject(outside))
                Assert.That(so.FindProperty("ObjectCallback.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_ObjectArgument").objectReferenceValue, Is.EqualTo(newOwner));
        }

        [Test]
        public void IncomingEvent_MissingMethod_IsBlocked()
        {
            UnityEventTools.AddPersistentListener(outside.Callback, oldOwner.Receive);
            var plan = Preview();
            var mapping = plan.Mappings.Single(m => m.Old == oldOwner);
            mapping.New = newRoot.AddComponent<BoxCollider>();
            FPSceneReferencePlanner.BuildPreview(plan, false);
            Assert.That(plan.Edits.Single().Blocker, Does.Contain("method/signature"));
        }

        [TestCase(false)] [TestCase(true)]
        public void Timeline_GenericBinding_IsDeduplicatedAndUndoableUnlessAmbiguous(bool duplicateOutput)
        {
            var key = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(key);
            var asset = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(asset); asset.Key = key; asset.DuplicateOutput = duplicateOutput;
            var director = outside.gameObject.AddComponent<PlayableDirector>(); director.playableAsset = asset;
            director.SetGenericBinding(key, oldRoot);
            var plan = Preview();
            Assert.That(plan.Links.Count(l => l.Owner == director && l.Target == oldRoot), Is.EqualTo(1));
            if (duplicateOutput)
            {
                Assert.That(plan.Edits.Single().Ready, Is.False);
                Assert.That(FPSceneReferenceApply.Apply(plan, out _), Is.False);
                Assert.That(director.GetGenericBinding(key), Is.EqualTo(oldRoot));
                return;
            }
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(director.GetGenericBinding(key), Is.EqualTo(newRoot));
            Undo.PerformUndo(); Assert.That(director.GetGenericBinding(key), Is.EqualTo(oldRoot));
        }

        [Test]
        public void Timeline_ChangedAssetOutputs_AreRevalidatedAtApply()
        {
            var key = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(key);
            var asset = ScriptableObject.CreateInstance<FPSceneReferenceTestPlayable>(); temporary.Add(asset); asset.Key = key;
            var director = outside.gameObject.AddComponent<PlayableDirector>(); director.playableAsset = asset;
            director.SetGenericBinding(key, oldRoot);
            outside.Target = oldOwner;
            var plan = Preview();
            asset.Key = null;
            Assert.That(FPSceneReferenceApply.Apply(plan, out _), Is.False);
            Assert.That(director.GetGenericBinding(key), Is.EqualTo(oldRoot));
            Assert.That(outside.Target, Is.EqualTo(oldOwner));
        }

        [Test]
        public void CancelledScan_CannotApply()
        {
            var plan = FPSceneReferencePlanner.Trace(oldRoot, newRoot, () => true);
            Assert.That(plan.Complete, Is.False);
            FPSceneReferencePlanner.BuildPreview(plan, false);
            Assert.That(FPSceneReferenceApply.Apply(plan, out _), Is.False);
        }

        [Test]
        public void Roots_OverlapOrOtherScene_AreRejected()
        {
            Assert.That(FPSceneReferencePlanner.ValidateRoots(oldRoot, oldRoot, true), Is.Not.Null);
            Assert.That(FPSceneReferencePlanner.ValidateRoots(oldRoot, Create("Child", oldRoot), true), Is.Not.Null);
            Assert.That(FPSceneReferencePlanner.ValidateRoots(oldRoot, null, false), Is.Null);
        }

        [Test]
        public void PrefabOverride_SurvivesSceneSaveReload()
        {
            string prefabPath = folder + "/Owner.prefab", scenePath = folder + "/Scene.unity";
            var prefab = PrefabUtility.SaveAsPrefabAsset(outside.gameObject, prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var owner = instance.GetComponent<FPSceneReferenceTestOwner>(); owner.Target = oldOwner;
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
            var plan = Preview();
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(PrefabUtility.GetPropertyModifications(instance).Any(m => m.propertyPath == "Target" && m.objectReference == newOwner), Is.True);
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            EditorSceneManager.CloseScene(scene, true);
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var restored = scene.GetRootGameObjects().Single(g => PrefabUtility.IsPartOfPrefabInstance(g)).GetComponent<FPSceneReferenceTestOwner>();
            Assert.That(((Component)restored.Target).gameObject.name, Is.EqualTo("New"));
        }

        [Test]
        public void MappingReview_MovedComponentAndDifferentTypeCallbacks_AreSuggestionsOnly()
        {
            var source = Create("OldSceneObjective", oldRoot).AddComponent<FPSceneReferenceTestOwner>();
            var moved = Create("DifferentObjectiveRole", newRoot).AddComponent<FPSceneReferenceTestOwner>();
            var alternate = newRoot.AddComponent<FPSceneReferenceArrayTestOwner>();
            UnityEventTools.AddPersistentListener(outside.Callback, source.Receive);
            var plan = Preview();
            var mapping = plan.Mappings.Single(m => m.Old == source);
            bool wasDirty = scene.isDirty;
            Assert.That(FPSceneMappingReview.Diagnosis(plan, mapping), Does.Contain("exist elsewhere"));
            var candidates = FPSceneMappingReview.FindCandidates(plan, mapping);
            Assert.That(candidates.Any(c => c.Target == moved && c.Blocker == null), Is.True);
            Assert.That(candidates.Any(c => c.Target == alternate && c.Blocker == null), Is.True);
            Assert.That(mapping.New, Is.Null, "A signature match must not silently assign a semantic role.");
            Assert.That(outside.Callback.GetPersistentTarget(0), Is.EqualTo(source));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
            mapping.New = alternate; mapping.Manual = true;
            FPSceneReferencePlanner.BuildPreview(plan, false);
            Assert.That(FPSceneReferenceApply.Apply(plan, out var result), Is.True, result);
            Assert.That(outside.Callback.GetPersistentTarget(0), Is.EqualTo(alternate));
        }

        [Test]
        public void MappingReview_AbsentTypeAndIncompatibleCallbacks_AreExplained()
        {
            var source = Create("OldSceneObjective", oldRoot).AddComponent<FPSceneReferenceTestOwner>();
            var alternate = newRoot.AddComponent<FPSceneReferenceArrayTestOwner>();
            UnityEventTools.AddObjectPersistentListener(outside.ObjectCallback, source.ReceiveObject, outside);
            var absent = oldRoot.AddComponent<BoxCollider>(); outside.ColliderTarget = absent;
            var plan = Preview();
            var mapping = plan.Mappings.Single(m => m.Old == source);
            Assert.That(FPSceneMappingReview.FindCandidates(plan, mapping).Any(c => c.Target == alternate), Is.False);
            Assert.That(FPSceneMappingReview.Diagnosis(plan, plan.Mappings.Single(m => m.Old == absent)), Does.Contain("absent"));
            Assert.That(plan.Edits.Single(e => e.Source.Path == "ColliderTarget").Blocker, Does.Contain("absent"));
        }

        [Test]
        public void MappingReview_MovedGameObjects_UseExactNameAndNeverSiblingOrder()
        {
            var source = Create("Specific", oldRoot);
            var parent = Create("Moved", newRoot);
            var a = Create("Specific", parent); var b = Create("Specific", parent);
            outside.Target = source;
            var plan = Preview();
            var mapping = plan.Mappings.Single(m => m.Old == source);
            var candidates = FPSceneMappingReview.FindCandidates(plan, mapping);
            Assert.That(candidates.Select(c => c.Target), Is.EquivalentTo(new Object[] { a, b }));
            Assert.That(mapping.New, Is.Null);
        }

        [Test]
        public void Preserve_SubtreeRetainsPoseReferencesAndUndoRedo_ThenAuditsOldDependencies()
        {
            var root = Create("SceneObjective", oldRoot);
            var child = Create("SceneAction", root);
            var owner = root.AddComponent<FPSceneReferenceTestOwner>();
            owner.Target = oldOwner; owner.HiddenTarget = child;
            outside.Target = owner; oldOwner.Target = owner;
            oldRoot.transform.SetPositionAndRotation(new Vector3(10, 3, 4), Quaternion.Euler(0, 25, 0));
            root.transform.localPosition = new Vector3(2, 1, 3);
            var destination = Create("SceneSpecific"); destination.transform.position = new Vector3(-5, 1, 3);
            Vector3 world = root.transform.position; Quaternion rotation = root.transform.rotation;
            bool wasDirty = scene.isDirty;
            var preservation = FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { root, child }, destination);
            Assert.That(preservation.Rows.Count, Is.EqualTo(1));
            Assert.That(preservation.Rows[0].ReferencesToRemainingOld, Is.EqualTo(1));
            Assert.That(root.transform.parent, Is.EqualTo(oldRoot.transform));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty), "Building preservation preview must remain read-only.");
            Assert.That(FPSceneObjectPreservation.Report(preservation), Does.Contain("references to remaining old hierarchy: 1"));
            Assert.That(FPSceneObjectPreservation.Apply(preservation, out var result), Is.True, result);
            Assert.That(root.transform.parent, Is.EqualTo(destination.transform));
            Assert.That(Vector3.Distance(root.transform.position, world), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(root.transform.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(child.transform.parent, Is.EqualTo(root.transform));
            Assert.That(outside.Target, Is.EqualTo(owner)); Assert.That(oldOwner.Target, Is.EqualTo(owner));
            Assert.That(owner.Target, Is.EqualTo(oldOwner)); Assert.That(owner.HiddenTarget, Is.EqualTo(child));
            var audit = FPSceneReferencePlanner.Trace(oldRoot, newRoot);
            Assert.That(audit.Links.Any(l => l.Owner == owner && l.Target == oldOwner && l.Category == FPSceneLinkCategory.Incoming), Is.True);
            Undo.PerformUndo(); Assert.That(root.transform.parent, Is.EqualTo(oldRoot.transform));
            Undo.PerformRedo(); Assert.That(root.transform.parent, Is.EqualTo(destination.transform));
        }

        [TestCase(false)] [TestCase(true)]
        public void Preserve_StalePreviewOrWriteFailure_LeavesParentsUnchanged(bool failDuringMove)
        {
            var a = Create("A", oldRoot); var b = Create("B", oldRoot);
            var preservation = FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { a, b }, null);
            if (!failDuringMove) outside.ConfigurationValue++;
            Assert.That(FPSceneObjectPreservation.Apply(preservation, out var result,
                failDuringMove ? count => throw new InvalidOperationException("injected failure") : (Action<int>)null), Is.False);
            Assert.That(a.transform.parent, Is.EqualTo(oldRoot.transform));
            Assert.That(b.transform.parent, Is.EqualTo(oldRoot.transform));
            Assert.That(result, Does.Contain(failDuringMove ? "rolled back" : "preflight blocked"));
        }

        [TestCase(false)] [TestCase(true)]
        public void Preserve_PrefabChildBlocksButOutermostInstanceCanMove(bool preserveChild)
        {
            var template = Create("Prefab"); Create("AuthoredChild", template);
            var asset = PrefabUtility.SaveAsPrefabAsset(template, folder + "/Preserve.prefab");
            Object.DestroyImmediate(template);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            instance.transform.SetParent(oldRoot.transform);
            var root = preserveChild ? instance.transform.GetChild(0).gameObject : instance;
            var preservation = FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { root }, null);
            Assert.That(preservation.Rows[0].Blocker == null, Is.EqualTo(!preserveChild));
            if (preserveChild)
            {
                Assert.That(FPSceneObjectPreservation.Apply(preservation, out _), Is.False);
                Assert.That(root.transform.parent, Is.EqualTo(instance.transform));
                return;
            }
            Assert.That(FPSceneObjectPreservation.Apply(preservation, out var result), Is.True, result);
            Assert.That(instance.transform.parent, Is.Null);
            Assert.That(PrefabUtility.IsPartOfPrefabInstance(instance), Is.True);
            Undo.PerformUndo(); Assert.That(instance.transform.parent, Is.EqualTo(oldRoot.transform));
        }

        [TestCase(false)] [TestCase(true)]
        public void Preserve_ActivationInheritanceChangesBlock_ButInactiveRootsCanMove(bool activeSelf)
        {
            var root = Create("SceneSpecific", oldRoot); root.SetActive(activeSelf); oldRoot.SetActive(false);
            var preservation = FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { root }, null);
            Assert.That(preservation.Rows[0].Blocker == null, Is.EqualTo(!activeSelf));
            Assert.That(FPSceneObjectPreservation.Apply(preservation, out _), Is.EqualTo(!activeSelf));
            Assert.That(root.activeInHierarchy, Is.False);
            Assert.That(root.activeSelf, Is.EqualTo(activeSelf));
        }

        [Test]
        public void Preserve_InvalidRootsAndDestinations_AreBlocked()
        {
            var root = Create("SceneSpecific", oldRoot);
            foreach (var invalid in new[] { oldRoot, outside.gameObject })
                Assert.That(FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { invalid }, null).Rows[0].Blocker, Is.Not.Null);
            foreach (var parent in new[] { oldRoot, newRoot, root })
                Assert.That(FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { root }, parent).Rows[0].Blocker, Is.Not.Null);
        }

        [Test]
        public void Preserve_CancelledScanCannotMoveObjects()
        {
            var root = Create("SceneSpecific", oldRoot);
            var preservation = FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { root }, null, () => true);
            Assert.That(preservation.Trace.Complete, Is.False);
            Assert.That(FPSceneObjectPreservation.Apply(preservation, out _), Is.False);
            Assert.That(root.transform.parent, Is.EqualTo(oldRoot.transform));
        }

        [Test]
        public void Window_RescanRetainsUnrelatedManualMappingsAfterPreservation()
        {
            var source = Create("OldName", oldRoot).AddComponent<BoxCollider>();
            var candidate = Create("NewName", newRoot).AddComponent<BoxCollider>();
            var keep = Create("SceneSpecific", oldRoot);
            var plan = Preview();
            var mapping = plan.Mappings.Single(m => m.Old == source);
            mapping.New = candidate; mapping.Manual = true; mapping.Reason = "Manually Assigned";
            var window = ScriptableObject.CreateInstance<FPSceneReferenceReplaceEditorWindow>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                typeof(FPSceneReferenceReplaceEditorWindow).GetField("oldGameObject", flags).SetValue(window, oldRoot);
                typeof(FPSceneReferenceReplaceEditorWindow).GetField("newGameObject", flags).SetValue(window, newRoot);
                typeof(FPSceneReferenceReplaceEditorWindow).GetField("plan", flags).SetValue(window, plan);
                var preservation = FPSceneObjectPreservation.Build(oldRoot, newRoot, new[] { keep }, null);
                Assert.That(FPSceneObjectPreservation.Apply(preservation, out var result), Is.True, result);
                typeof(FPSceneReferenceReplaceEditorWindow).GetMethod("Trace", flags).Invoke(window, new object[] { false });
                var refreshed = (FPSceneReferencePlan)typeof(FPSceneReferenceReplaceEditorWindow).GetField("plan", flags).GetValue(window);
                Assert.That(refreshed.Mappings.Single(m => m.Old == source).New, Is.EqualTo(candidate));
                Assert.That(refreshed.Inventory, Has.No.Member(keep));
            }
            finally { Object.DestroyImmediate(window); }
        }
    }

    public sealed class FPSceneReferenceTestPlayable : PlayableAsset
    {
        public ExposedReference<GameObject> SceneTarget;
        public Object DirectTarget;
        public Object Key;
        public bool DuplicateOutput;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) => Playable.Null;
        public override IEnumerable<PlayableBinding> outputs
        {
            get
            {
                if (Key != null) yield return ScriptPlayableBinding.Create("Test binding", Key, typeof(GameObject));
                if (Key != null && DuplicateOutput) yield return ScriptPlayableBinding.Create("Duplicate binding", Key, typeof(GameObject));
            }
        }
    }
}
