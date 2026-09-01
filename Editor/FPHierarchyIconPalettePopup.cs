// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    internal sealed class FPHierarchyIconPalettePopup : PopupWindowContent
    {
        private const float IconSize = 38f;
        private const float IconSpacing = 4f;
        private const int ColumnCount = 5;

        private readonly GameObject target;
        private readonly List<Texture2D> icons;

        private FPHierarchyIconPalettePopup(GameObject target)
        {
            this.target = target;
            icons = FPHierarchyIconUtility.GetPaletteIcons();
        }

        internal static bool Show(GameObject target, Vector2 activatorPosition)
        {
            if (!FPHierarchyIconUtility.IsEligibleTarget(target))
            {
                return false;
            }

            PopupWindow.Show(
                new Rect(activatorPosition, Vector2.zero),
                new FPHierarchyIconPalettePopup(target));
            return true;
        }

        public override Vector2 GetWindowSize()
        {
            int rowCount = Mathf.Max(1, Mathf.CeilToInt(icons.Count / (float)ColumnCount));
            return new Vector2(
                12f + ColumnCount * (IconSize + IconSpacing),
                82f + rowCount * (IconSize + IconSpacing));
        }

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField("Hierarchy Icon", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(target == null ? "Missing GameObject" : target.name, EditorStyles.miniLabel);
            EditorGUILayout.Space(3f);

            if (icons.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Add icons to the Alt-Click Palette in FuzzPhyte > Header > Header Options.",
                    MessageType.Info);
            }
            else
            {
                DrawIconGrid();
            }

            using (new EditorGUI.DisabledScope(target == null || FPHierarchyIconUtility.GetIcon(target) == null))
            {
                if (GUILayout.Button("Clear Icon"))
                {
                    FPHierarchyIconUtility.ClearIcons(new[] { target });
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawIconGrid()
        {
            for (int index = 0; index < icons.Count; index += ColumnCount)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int rowEnd = Mathf.Min(index + ColumnCount, icons.Count);
                    for (int iconIndex = index; iconIndex < rowEnd; iconIndex++)
                    {
                        Texture2D icon = icons[iconIndex];
                        var content = new GUIContent(icon, icon.name);
                        if (GUILayout.Button(content, GUILayout.Width(IconSize), GUILayout.Height(IconSize)))
                        {
                            FPHierarchyIconUtility.ApplyIcon(new[] { target }, icon);
                            editorWindow.Close();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }
    }
}
