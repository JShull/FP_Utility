// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. Commercial use requires a separate license.
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Reveals FP sections for Inspector reference clicks without changing selection.</summary>
    [InitializeOnLoad]
    internal static class FPHHeaderInspectorReveal
    {
        private static readonly HashSet<VisualElement> inspectorRoots = new();
        private static readonly EditorGUIUtility.PropertyCallbackScope propertyCallbacks;
        private static double nextInspectorScan;

        static FPHHeaderInspectorReveal()
        {
            propertyCallbacks = new EditorGUIUtility.PropertyCallbackScope(OnBeginProperty);
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnInspectorHeader;
            EditorApplication.delayCall += BindInspectors;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
        }

        private static void OnInspectorHeader(UnityEditor.Editor editor)
        {
            if (EditorApplication.timeSinceStartup < nextInspectorScan)
                return;
            nextInspectorScan = EditorApplication.timeSinceStartup + 1;
            BindInspectors();
        }

        private static void BindInspectors()
        {
            inspectorRoots.RemoveWhere(root => root.panel == null);
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                bool inspector = false;
                for (var type = window.GetType(); type != null; type = type.BaseType)
                {
                    if (type.FullName == "UnityEditor.PropertyEditor" || type.FullName == "UnityEditor.InspectorWindow")
                    {
                        inspector = true;
                        break;
                    }
                }
                if (!inspector)
                    continue;

                var root = window.rootVisualElement;
                // Remove first as a detached root may have been rebound since the last scan.
                root.UnregisterCallback<MouseDownEvent>(OnReferenceMouseDown, TrickleDown.TrickleDown);
                root.RegisterCallback<MouseDownEvent>(OnReferenceMouseDown, TrickleDown.TrickleDown);
                inspectorRoots.Add(root);
            }
        }

        private static void OnReferenceMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.clickCount != 1 || evt.shiftKey || evt.ctrlKey)
                return;

            QueueReveal(GetReferenceFromDisplay(evt.target as VisualElement));
        }

        internal static Object GetReferenceFromDisplay(VisualElement element)
        {
            bool display = false;
            for (; element != null; element = element.parent)
            {
                if (element.ClassListContains(ObjectField.selectorUssClassName))
                    return null;
                display |= element.ClassListContains(ObjectField.objectUssClassName);
                if (element is ObjectField field)
                    return display ? field.value : null;
            }
            return null;
        }

        private static void OnBeginProperty(Rect rect, SerializedProperty property)
        {
            var evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 0 ||
                evt.clickCount != 1 || evt.shift || evt.control ||
                property.propertyType != SerializedPropertyType.ObjectReference || property.hasMultipleDifferentValues)
                return;

            // Standard serialized IMGUI fields: exclude their label and object-picker button.
            rect.xMin += EditorGUIUtility.labelWidth;
            rect.xMax -= 20;
            if (rect.Contains(evt.mousePosition))
                QueueReveal(property.objectReferenceValue);
        }

        private static void QueueReveal(Object reference)
        {
            if (!FP_HHeader.IsEnabled || EditorApplication.isPlayingOrWillChangePlaymode ||
                (reference is not GameObject && reference is not Component) || EditorUtility.IsPersistent(reference))
                return;

            // Finish the original Inspector event before changing hierarchy visibility.
            EditorApplication.delayCall += () =>
            {
                if (reference != null)
                    FP_HHeader.RevealHeaderForPing(reference);
            };
        }

        private static void Cleanup()
        {
            propertyCallbacks.Dispose();
            UnityEditor.Editor.finishedDefaultHeaderGUI -= OnInspectorHeader;
            EditorApplication.delayCall -= BindInspectors;
            foreach (var root in inspectorRoots)
                root.UnregisterCallback<MouseDownEvent>(OnReferenceMouseDown, TrickleDown.TrickleDown);
            inspectorRoots.Clear();
        }
    }
}
