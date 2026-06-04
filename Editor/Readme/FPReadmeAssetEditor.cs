// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    
    using UnityEditor;
    using UnityEngine;
    [CustomEditor(typeof(FPReadmeAsset))]
    public class FPReadmeAssetEditor:UnityEditor.Editor
    {
        private const float HeaderIconSize = 72f;

        public override void OnInspectorGUI()
        {
            var readme = (FPReadmeAsset)target;

            DrawHeader(readme);

            EditorGUILayout.Space(12);

            if (!string.IsNullOrWhiteSpace(readme.overview))
            {
                DrawBodyText(readme.overview);
                EditorGUILayout.Space(12);
            }

            foreach (var section in readme.sections)
            {
                DrawSection(section);
            }

            EditorGUILayout.Space(16);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Readme Asset",
                    readme,
                    typeof(FPReadmeAsset),
                    false
                );
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Edit Readme Data"))
            {
                Selection.activeObject = readme;
                EditorGUIUtility.PingObject(readme);
            }
        }

        private static void DrawHeader(FPReadmeAsset readme)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (readme.icon != null)
                {
                    GUILayout.Label(
                        readme.icon,
                        GUILayout.Width(HeaderIconSize),
                        GUILayout.Height(HeaderIconSize)
                    );
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(4);

                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 20,
                        wordWrap = true
                    };

                    EditorGUILayout.LabelField(readme.title, titleStyle);

                    if (!string.IsNullOrWhiteSpace(readme.subtitle))
                    {
                        var subtitleStyle = new GUIStyle(EditorStyles.label)
                        {
                            fontSize = 12,
                            wordWrap = true
                        };

                        EditorGUILayout.LabelField(readme.subtitle, subtitleStyle);
                    }

                    if (!string.IsNullOrWhiteSpace(readme.version))
                    {
                        EditorGUILayout.LabelField(
                            $"Version {readme.version}",
                            EditorStyles.miniLabel
                        );
                    }
                }
            }
        }

        private static void DrawSection(FPReadmeSection section)
        {
            if (section == null)
            {
                return;
            }

            EditorGUILayout.Space(8);

            if (!string.IsNullOrWhiteSpace(section.heading))
            {
                var headingStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    wordWrap = true
                };

                EditorGUILayout.LabelField(section.heading, headingStyle);
            }

            if (!string.IsNullOrWhiteSpace(section.body))
            {
                DrawBodyText(section.body);
            }

            if (section.links != null)
            {
                foreach (var link in section.links)
                {
                    DrawLink(link);
                }
            }
        }

        private static void DrawBodyText(string text)
        {
            var bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                richText = true
            };

            EditorGUILayout.LabelField(text, bodyStyle);
        }

        private static void DrawLink(FPReadmeLink link)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.label))
            {
                return;
            }

            var linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                wordWrap = true
            };

            var rect = GUILayoutUtility.GetRect(
                new GUIContent(link.label),
                linkStyle
            );

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (GUI.Button(rect, link.label, linkStyle))
            {
                if (!string.IsNullOrWhiteSpace(link.url))
                {
                    Application.OpenURL(link.url);
                }
            }
        }
    }
}
