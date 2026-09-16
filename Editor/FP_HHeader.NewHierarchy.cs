// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

#if UNITY_6000_6_OR_NEWER
namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using Unity.Hierarchy;
    using Unity.Hierarchy.Editor;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public static partial class FP_HHeader
    {
        private const string NewHierarchyFoldoutName = "fp-hheader-foldout";
        private const string NewHierarchySelectAllName = "fp-hheader-select-all";
        private const string NewHierarchyControlsName = "fp-hheader-controls";
        private const string NewHierarchyItemClass = "fp-hheader-item";
        private const string NewHierarchyBackgroundName = "fp-hheader-background";
        // Resolve the asset itself so Assets checkouts and UPM installations share one lookup.
        private const string NewHierarchyStyleSheetGuid = "61fb69c1b67260b458a0e472eba58316";
        private static StyleSheet newHierarchyStyleSheet;
        private const float NewHierarchyControlSize = 15f;
        private const float NewHierarchyControlsWidth = 34f;

        private static readonly Dictionary<HierarchyViewItem, GameObject> boundNewHierarchyHeaders = new();
        private static readonly Dictionary<HierarchyViewItem, NewHierarchyPaletteBinding> newHierarchyPaletteBindings = new();

        private sealed class NewHierarchyPaletteBinding
        {
            internal VisualElement Row;
            internal EventCallback<PointerDownEvent> Callback;
        }

        private static void RegisterNewHierarchyCallbacks()
        {
            HierarchyWindow.BindViewItem -= OnBindNewHierarchyItem;
            HierarchyWindow.BindViewItem += OnBindNewHierarchyItem;
            HierarchyWindow.UnbindViewItem -= OnUnbindNewHierarchyItem;
            HierarchyWindow.UnbindViewItem += OnUnbindNewHierarchyItem;
        }

        private static void OnBindNewHierarchyItem(
            HierarchyWindow window,
            HierarchyView view,
            HierarchyViewItem item)
        {
            UnregisterNewHierarchyPaletteBinding(item);
            ResetNewHierarchyItem(item);
            boundNewHierarchyHeaders.Remove(item);

            if (item.Handler is not HierarchyGameObjectHandler gameObjectHandler)
            {
                return;
            }

            GameObject gameObject = gameObjectHandler.GetGameObject(in item.Node);
            if (FPHierarchyIconUtility.IsEligibleTarget(gameObject))
            {
                RegisterNewHierarchyPaletteBinding(item, gameObject);
            }

            if (!IsEnabled || !IsHeaderObject(gameObject))
            {
                return;
            }

            boundNewHierarchyHeaders[item] = gameObject;
            ApplyNewHierarchyHeaderVisuals(item, gameObject);
        }

        private static void OnUnbindNewHierarchyItem(
            HierarchyWindow window,
            HierarchyView view,
            HierarchyViewItem item)
        {
            UnregisterNewHierarchyPaletteBinding(item);
            ResetNewHierarchyItem(item);
            boundNewHierarchyHeaders.Remove(item);
        }

        private static void RegisterNewHierarchyPaletteBinding(HierarchyViewItem item, GameObject gameObject)
        {
            VisualElement row = item.RowContainer;
            if (row == null)
            {
                return;
            }

            EventCallback<PointerDownEvent> callback = evt =>
            {
                if (evt.button != 0 || !evt.altKey)
                {
                    return;
                }

                if (FPHierarchyIconPalettePopup.Show(gameObject, evt.position))
                {
                    evt.StopImmediatePropagation();
                }
            };

            row.RegisterCallback(callback);
            newHierarchyPaletteBindings[item] = new NewHierarchyPaletteBinding
            {
                Row = row,
                Callback = callback
            };
        }

        private static void UnregisterNewHierarchyPaletteBinding(HierarchyViewItem item)
        {
            if (!newHierarchyPaletteBindings.TryGetValue(item, out NewHierarchyPaletteBinding binding))
            {
                return;
            }

            if (binding.Row != null && binding.Callback != null)
            {
                binding.Row.UnregisterCallback(binding.Callback);
            }

            newHierarchyPaletteBindings.Remove(item);
        }

        private static void ApplyNewHierarchyHeaderVisuals(HierarchyViewItem item, GameObject headerObject)
        {
            ResetNewHierarchyItem(item);
            if (!IsEnabled || !IsHeaderObject(headerObject))
            {
                return;
            }

            bool isExpanded = IsHeaderExpanded(headerObject);
            item.AddToClassList(NewHierarchyItemClass);
            // Headers are deliberately inactive GameObjects, but their editor controls
            // must not inherit Unity's inactive-object opacity.
            item.style.opacity = 1f;
            if (item.RowContainer != null)
            {
                if (newHierarchyStyleSheet == null)
                {
                    newHierarchyStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                        AssetDatabase.GUIDToAssetPath(NewHierarchyStyleSheetGuid));
                }
                if (newHierarchyStyleSheet != null)
                {
                    VisualElement row = item.RowContainer;
                    if (!row.styleSheets.Contains(newHierarchyStyleSheet)) row.styleSheets.Add(newHierarchyStyleSheet);
                    var background = new VisualElement { name = NewHierarchyBackgroundName, pickingMode = PickingMode.Ignore };
                    background.AddToClassList(NewHierarchyBackgroundName);
                    background.style.backgroundColor = isExpanded ? headerColor : collapsedColor;
                    // Own only this backing element. Unity owns the row background and
                    // animates it for ping; the stylesheet yields to ping and selection.
                    row.Insert(0, background);
                }
            }

            if (item.Name != null)
            {
                item.Name.style.color = Color.white;
                item.Name.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            VisualElement nameContainer = item.Name?.parent;
            if (nameContainer == null)
            {
                return;
            }

            nameContainer.style.flexDirection = FlexDirection.Row;
            nameContainer.style.alignItems = Align.Center;

            var controls = new VisualElement
            {
                name = NewHierarchyControlsName,
                pickingMode = PickingMode.Ignore,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 0f,
                    width = NewHierarchyControlsWidth,
                    height = NewHierarchyControlSize
                }
            };

            VisualElement foldout = CreateNewHierarchyControl(
                NewHierarchyFoldoutName,
                isExpanded ? hhOpenIcon : hhCloseIcon,
                isExpanded ? "Collapse FP Header section" : "Expand FP Header section",
                isExpanded ? "▼" : "▶");
            foldout.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                ToggleHeaderSection(headerObject);
                evt.StopImmediatePropagation();
            });
            controls.Add(foldout);

            VisualElement selectAll = CreateNewHierarchyControl(
                NewHierarchySelectAllName,
                hhSelectAllIcon,
                "Select this FP Header section",
                "●");
            selectAll.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                SelectAllSubsequentObjects(headerObject);
                evt.StopImmediatePropagation();
            });
            controls.Add(selectAll);

            int nameIndex = nameContainer.IndexOf(item.Name);
            nameContainer.Insert(nameIndex < 0 ? 0 : nameIndex, controls);
        }

        private static VisualElement CreateNewHierarchyControl(
            string elementName,
            Texture2D texture,
            string tooltip,
            string fallbackText)
        {
            VisualElement element;
            if (texture != null)
            {
                element = new VisualElement();
                element.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                element = new Label(fallbackText)
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleCenter,
                        color = Color.white
                    }
                };
            }

            element.name = elementName;
            element.tooltip = tooltip;
            element.pickingMode = PickingMode.Position;
            element.style.width = NewHierarchyControlSize;
            element.style.minWidth = NewHierarchyControlSize;
            element.style.height = NewHierarchyControlSize;
            element.style.flexShrink = 0f;
            element.style.marginRight = 2f;
            return element;
        }

        private static void ResetNewHierarchyItem(HierarchyViewItem item)
        {
            if (!item.ClassListContains(NewHierarchyItemClass)) return;
            item.RemoveFromClassList(NewHierarchyItemClass);
            item.style.opacity = StyleKeyword.Null;
            RemoveNewHierarchyElement(item.RowContainer, NewHierarchyBackgroundName);

            if (item.Name != null)
            {
                item.Name.style.color = StyleKeyword.Null;
                item.Name.style.unityFontStyleAndWeight = StyleKeyword.Null;

                VisualElement nameContainer = item.Name.parent;
                if (nameContainer != null)
                {
                    nameContainer.style.flexDirection = StyleKeyword.Null;
                    nameContainer.style.alignItems = StyleKeyword.Null;
                    RemoveNewHierarchyElement(nameContainer, NewHierarchyControlsName);
                }
            }
        }

        private static void RemoveNewHierarchyElement(VisualElement container, string elementName)
        {
            VisualElement element = container?.Q<VisualElement>(elementName);
            if (element != null)
            {
                element.RemoveFromHierarchy();
            }
        }

        private static void RefreshNewHierarchyWindows()
        {
            var staleItems = new List<HierarchyViewItem>();
            var boundItems = new List<KeyValuePair<HierarchyViewItem, GameObject>>(boundNewHierarchyHeaders);
            for (int i = 0; i < boundItems.Count; i++)
            {
                KeyValuePair<HierarchyViewItem, GameObject> pair = boundItems[i];
                if (pair.Key == null || pair.Value == null)
                {
                    staleItems.Add(pair.Key);
                    continue;
                }

                ApplyNewHierarchyHeaderVisuals(pair.Key, pair.Value);
            }

            for (int i = 0; i < staleItems.Count; i++)
            {
                if (staleItems[i] != null)
                {
                    boundNewHierarchyHeaders.Remove(staleItems[i]);
                }
            }

            HierarchyWindow[] windows = Resources.FindObjectsOfTypeAll<HierarchyWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i].View != null)
                {
                    windows[i].View.Update();
                }

                windows[i].Repaint();
            }
        }
    }
}
#endif
