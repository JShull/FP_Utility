// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;

    public class FPReadmeLargeTextEditorWindow:EditorWindow
    {
        private string titleText;
        private string text;
        private Vector2 scroll;
        private Action<string> onApply;

        public static void Open(
            string title,
            string initialText,
            Action<string> onApplyCallback
        )
        {
            var window = CreateInstance<FPReadmeLargeTextEditorWindow>();

            window.titleContent = new GUIContent(title);
            window.titleText = title;
            window.text = initialText ?? string.Empty;
            window.onApply = onApplyCallback;
            window.minSize = new Vector2(700, 520);

            window.ShowUtility();
            window.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(titleText, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            var textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false
            };

            scroll = EditorGUILayout.BeginScrollView(scroll);

            text = EditorGUILayout.TextArea(
                text ?? string.Empty,
                textAreaStyle,
                GUILayout.ExpandHeight(true)
            );

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(28)))
                {
                    Close();
                }

                if (GUILayout.Button("Apply", GUILayout.Height(28)))
                {
                    onApply?.Invoke(text);
                    Close();
                }
            }
        }
    }
}
