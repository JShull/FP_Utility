// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
#if UNITY_6000_6_OR_NEWER
    using System;
    using Unity.Hierarchy;
    using UnityEngine.UIElements;
#endif

    public class FPHHeaderCompatibilityTests
    {
        private Scene previewScene;
        private GameObject testObject;
        private Dictionary<string, bool> savedFoldoutStates;
        private static readonly FieldInfo FoldoutStatesField = typeof(FP_HHeader).GetField(
            "foldoutStates", BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            savedFoldoutStates = (Dictionary<string, bool>)FoldoutStatesField.GetValue(null);
            FoldoutStatesField.SetValue(null, new Dictionary<string, bool>());
        }

        [TearDown]
        public void TearDown()
        {
            FoldoutStatesField.SetValue(null, savedFoldoutStates);
            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        [Test]
        public void IsHeaderObject_AcceptsInactiveAllCapsLeaf()
        {
            testObject = new GameObject("HEADER");
            SceneManager.MoveGameObjectToScene(testObject, previewScene);
            testObject.SetActive(false);

            Assert.That(FP_HHeader.IsHeaderObject(testObject), Is.True);
        }

        [Test]
        public void IsHeaderObject_RejectsMixedCaseInactiveLeaf()
        {
            testObject = new GameObject("Header");
            SceneManager.MoveGameObjectToScene(testObject, previewScene);
            testObject.SetActive(false);

            Assert.That(FP_HHeader.IsHeaderObject(testObject), Is.False);
        }

        [Test]
        public void IsHeaderObject_RejectsHeaderWithChildren()
        {
            testObject = new GameObject("HEADER");
            var child = new GameObject("Child");
            SceneManager.MoveGameObjectToScene(testObject, previewScene);
            SceneManager.MoveGameObjectToScene(child, previewScene);
            child.transform.SetParent(testObject.transform);
            testObject.SetActive(false);

            Assert.That(FP_HHeader.IsHeaderObject(testObject), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RevealSelection_OpensAllContainingClosedSectionsOnly(bool innerAlreadyOpen)
        {
            var outer = CreateObject("OUTER", header: true);
            var container = CreateObject("Container");
            var inner = CreateObject("INNER", container, true);
            var target = CreateObject("Target", container);
            var unrelated = CreateObject("UNRELATED", header: true);
            var unrelatedTarget = CreateObject("UnrelatedTarget");
            var states = (Dictionary<string, bool>)FoldoutStatesField.GetValue(null);
            states[outer.name] = false;
            states[inner.name] = innerAlreadyOpen;
            states[unrelated.name] = false;
            container.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
            target.hideFlags = innerAlreadyOpen ? HideFlags.None : HideFlags.HideInHierarchy;
            unrelatedTarget.hideFlags = HideFlags.HideInHierarchy;

            Assert.That(FP_HHeader.ExpandContainingHeaderSections(target), Is.True);

            Assert.That(states[outer.name], Is.True);
            Assert.That(states[inner.name], Is.True);
            Assert.That(states[unrelated.name], Is.False);
            Assert.That(container.hideFlags, Is.EqualTo(HideFlags.NotEditable));
            Assert.That(target.hideFlags, Is.EqualTo(HideFlags.None));
            Assert.That(unrelatedTarget.hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
            Assert.That(FP_HHeader.ExpandContainingHeaderSections(target), Is.False);
        }

        [Test]
        public void RevealSelection_OutsideSectionsPreservesHiddenFlags()
        {
            var target = CreateObject("Target");
            target.hideFlags = HideFlags.HideInHierarchy;
            var header = CreateObject("LATER", header: true);
            var states = (Dictionary<string, bool>)FoldoutStatesField.GetValue(null);
            states[header.name] = false;

            Assert.That(FP_HHeader.ExpandContainingHeaderSections(target), Is.False);
            Assert.That(target.hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
            Assert.That(states[header.name], Is.False);
        }

        [Test]
        public void RevealSelection_HeaderOpensOuterSectionButNotPreviousOrOwnSection()
        {
            var outer = CreateObject("OUTER", header: true);
            var container = CreateObject("Container");
            var previous = CreateObject("PREVIOUS", container, true);
            var previousTarget = CreateObject("PreviousTarget", container);
            var selectedHeader = CreateObject("SELECTED", container, true);
            var ownTarget = CreateObject("OwnTarget", container);
            var states = (Dictionary<string, bool>)FoldoutStatesField.GetValue(null);
            states[outer.name] = false;
            states[previous.name] = false;
            states[selectedHeader.name] = false;
            container.hideFlags = HideFlags.HideInHierarchy;
            previousTarget.hideFlags = HideFlags.HideInHierarchy;
            ownTarget.hideFlags = HideFlags.HideInHierarchy;

            Assert.That(FP_HHeader.ExpandContainingHeaderSections(selectedHeader), Is.True);
            Assert.That(states[outer.name], Is.True);
            Assert.That(states[previous.name], Is.False);
            Assert.That(states[selectedHeader.name], Is.False);
            Assert.That(container.hideFlags, Is.EqualTo(HideFlags.None));
            Assert.That(previousTarget.hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
            Assert.That(ownTarget.hideFlags, Is.EqualTo(HideFlags.HideInHierarchy));
        }

        private GameObject CreateObject(string name, GameObject parent = null, bool header = false)
        {
            var obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, previewScene);
            if (parent != null)
            {
                obj.transform.SetParent(parent.transform);
            }
            if (header)
            {
                obj.SetActive(false);
            }
            return obj;
        }

#if UNITY_6000_6_OR_NEWER
        [Test]
        public void InspectorReference_DisplayResolvesComponentWithoutSelectingIt()
        {
            var target = CreateObject("Target");
            var selection = UnityEditor.Selection.activeObject;
            var field = new UnityEditor.UIElements.ObjectField { value = target.transform };
            var display = field.Q<VisualElement>(className: UnityEditor.UIElements.ObjectField.objectUssClassName);

            Assert.That(FPHHeaderInspectorReveal.GetReferenceFromDisplay(display), Is.SameAs(target.transform));
            Assert.That(UnityEditor.Selection.activeObject, Is.SameAs(selection));
        }

        [Test]
        public void InspectorReference_LabelAndPickerDoNotRequestReveal()
        {
            var target = CreateObject("Target");
            var field = new UnityEditor.UIElements.ObjectField("Reference") { value = target };
            var picker = field.Q<VisualElement>(className: UnityEditor.UIElements.ObjectField.selectorUssClassName);

            Assert.That(FPHHeaderInspectorReveal.GetReferenceFromDisplay(picker), Is.Null);
            Assert.That(FPHHeaderInspectorReveal.GetReferenceFromDisplay(field.labelElement), Is.Null);
            Assert.That(FPHHeaderInspectorReveal.GetReferenceFromDisplay(field), Is.Null);
        }

        [Test]
        public void NewHierarchy_ResetOrdinaryItemPreservesUnityStyles()
        {
            var item = (HierarchyViewItem)Activator.CreateInstance(typeof(HierarchyViewItem), true);
            var row = new VisualElement();
            row.AddToClassList("unity-multi-column-view__row-container");
            row.Add(item);
            row.style.backgroundColor = Color.yellow;
            item.style.opacity = 0.4f;
            item.Name.style.color = Color.cyan;

            InvokeHierarchyMethod("ResetNewHierarchyItem", item);

            Assert.That(row.style.backgroundColor.value, Is.EqualTo(Color.yellow));
            Assert.That(item.style.opacity.value, Is.EqualTo(0.4f));
            Assert.That(item.Name.style.color.value, Is.EqualTo(Color.cyan));
        }

        [Test]
        public void NewHierarchy_HeaderBackingIsBehindControlsAndDoesNotOwnPingBackground()
        {
            testObject = new GameObject("HEADER");
            SceneManager.MoveGameObjectToScene(testObject, previewScene);
            testObject.SetActive(false);
            var item = (HierarchyViewItem)Activator.CreateInstance(typeof(HierarchyViewItem), true);
            var row = new VisualElement();
            row.AddToClassList("unity-multi-column-view__row-container");
            row.Add(item);
            row.style.backgroundColor = Color.yellow;

            InvokeHierarchyMethod("ApplyNewHierarchyHeaderVisuals", item, testObject);

            Assert.That(row.style.backgroundColor.value, Is.EqualTo(Color.yellow));
            Assert.That(row[0].name, Is.EqualTo("fp-hheader-background"));
            Assert.That(row[0].pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(item.Q<VisualElement>("fp-hheader-controls"), Is.Not.Null);
            Assert.That(item.style.opacity.value, Is.EqualTo(1f));

            InvokeHierarchyMethod("ResetNewHierarchyItem", item);

            Assert.That(row.Q<VisualElement>("fp-hheader-background"), Is.Null);
            Assert.That(item.Q<VisualElement>("fp-hheader-controls"), Is.Null);
            Assert.That(item.style.opacity.keyword, Is.EqualTo(StyleKeyword.Null));
            Assert.That(row.style.backgroundColor.value, Is.EqualTo(Color.yellow));
        }

        private static void InvokeHierarchyMethod(string name, params object[] arguments)
        {
            typeof(FP_HHeader).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);
        }
#endif
    }
}
