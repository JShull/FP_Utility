namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    public class FPReadmeBuilderWindow:EditorWindow
    {
        private enum BuilderMode
        {
            CreateNew,
            EditExisting
        }

        private BuilderMode mode = BuilderMode.CreateNew;

        private FPReadmeAsset existingReadme;
        private Vector2 overviewScroll;
        private Texture2D icon;
        private string readmeTitle = "FuzzPhyte Package";
        private string subtitle = "Package documentation and setup notes.";
        private string version = "0.0.1";

        private string overview =
            "This package contains tools, scripts, and assets for a FuzzPhyte Unity module.";

        private DefaultAsset outputFolder;

        private readonly List<SectionDraft> sections = new();

        private Vector2 scroll;

        [MenuItem("FuzzPhyte/Utility/Editor/Readme Tool/Readme Builder", priority = FP_UtilityData.MENU_UTILITY_EDITOR + 30)]
        public static void Open()
        {
            var window = GetWindow<FPReadmeBuilderWindow>();
            window.titleContent = new GUIContent("Readme Builder");
            window.minSize = new Vector2(560, 540);
            window.Show();
        }

        [MenuItem("FuzzPhyte/Utility/Editor/Readme Tool/Edit Readme Asset", true)]
        private static bool ValidateEditSelectedReadme()
        {
            return Selection.activeObject is FPReadmeAsset;
        }

        [MenuItem("FuzzPhyte/Utility/Editor/Readme Tool/Edit Readme Asset", priority = FP_UtilityData.MENU_UTILITY_EDITOR + 31)]
        private static void EditSelectedReadme()
        {
            var readme = Selection.activeObject as FPReadmeAsset;

            if (readme == null)
            {
                return;
            }

            var window = GetWindow<FPReadmeBuilderWindow>();
            window.titleContent = new GUIContent("Readme Builder");
            window.minSize = new Vector2(560, 540);
            window.LoadExistingReadme(readme);
            window.Show();
        }

        private void OnEnable()
        {
            if (sections.Count == 0 && existingReadme == null)
            {
                ResetToDefaultDraft();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawModeControls();
            EditorGUILayout.Space(12);

            DrawHeaderFields();
            EditorGUILayout.Space(12);

            DrawSections();
            EditorGUILayout.Space(12);

            DrawBuildControls();

            EditorGUILayout.EndScrollView();
        }

        private void DrawModeControls()
        {
            EditorGUILayout.LabelField("Readme Mode", EditorStyles.boldLabel);

            var newMode = (BuilderMode)EditorGUILayout.EnumPopup("Mode", mode);

            if (newMode != mode)
            {
                mode = newMode;

                if (mode == BuilderMode.CreateNew)
                {
                    existingReadme = null;
                }
            }

            if (mode == BuilderMode.EditExisting)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var selectedReadme = (FPReadmeAsset)EditorGUILayout.ObjectField(
                        "Existing Readme",
                        existingReadme,
                        typeof(FPReadmeAsset),
                        false
                    );

                    if (selectedReadme != existingReadme)
                    {
                        LoadExistingReadme(selectedReadme);
                    }

                    using (new EditorGUI.DisabledScope(existingReadme == null))
                    {
                        if (GUILayout.Button("Reload", GUILayout.Width(72)))
                        {
                            LoadExistingReadme(existingReadme);
                        }
                    }
                }

                if (existingReadme != null)
                {
                    var path = AssetDatabase.GetAssetPath(existingReadme);
                    EditorGUILayout.HelpBox($"Editing: {path}", MessageType.None);
                }
            }
            else
            {
                outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    "Output Folder",
                    outputFolder,
                    typeof(DefaultAsset),
                    false
                );
            }
        }

        private void DrawHeaderFields()
        {
            EditorGUILayout.LabelField("Readme Header", EditorStyles.boldLabel);

            icon = (Texture2D)EditorGUILayout.ObjectField(
                "Icon",
                icon,
                typeof(Texture2D),
                false
            );

            readmeTitle = EditorGUILayout.TextField("Title", readmeTitle);
            subtitle = EditorGUILayout.TextField("Subtitle", subtitle);
            version = EditorGUILayout.TextField("Version", version);

            overview = DrawWrappedScrollableTextAreaWithLargeEditor(
                "Overview",
                overview,
                ref overviewScroll,
                updatedText =>
                {
                    overview = updatedText;
                    Repaint();
                },
                minHeight: 100f,
                maxHeight: 180f
                );
        }

        private void DrawSections()
        {
            EditorGUILayout.LabelField("Sections", EditorStyles.boldLabel);

            for (int i = 0; i < sections.Count; i++)
            {
                DrawSection(i);
                EditorGUILayout.Space(8);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add Section"))
                {
                    sections.Add(new SectionDraft
                    {
                        heading = "New Section",
                        body = ""
                    });
                }

                if (GUILayout.Button("Reset Draft"))
                {
                    if (EditorUtility.DisplayDialog(
                            "Reset Readme Draft",
                            "This will clear the current editor-window draft. It will not change the asset until you save/apply.",
                            "Reset",
                            "Cancel"))
                    {
                        ResetToDefaultDraft();
                    }
                }
            }
        }

        private void DrawSection(int index)
        {
            var section = sections[index];

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    section.foldout = EditorGUILayout.Foldout(
                        section.foldout,
                        string.IsNullOrWhiteSpace(section.heading)
                            ? $"Section {index + 1}"
                            : section.heading,
                        true
                    );

                    if (GUILayout.Button("Up", GUILayout.Width(42)))
                    {
                        MoveSection(index, -1);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Down", GUILayout.Width(52)))
                    {
                        MoveSection(index, 1);
                        GUIUtility.ExitGUI();
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(72)))
                    {
                        sections.RemoveAt(index);
                        GUIUtility.ExitGUI();
                    }
                }

                if (!section.foldout)
                {
                    return;
                }

                section.heading = EditorGUILayout.TextField(
                    "Heading",
                    section.heading
                );

                section.body = DrawWrappedScrollableTextAreaWithLargeEditor(
                    "Body",
                    section.body,
                    ref section.scrollPosition,
                    updatedText =>
                    {
                        section.body = updatedText;
                        Repaint();
                    },
                    minHeight: 120f,
                    maxHeight: 240f
                );

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Links", EditorStyles.miniBoldLabel);

                for (int i = 0; i < section.links.Count; i++)
                {
                    var link = section.links[i];

                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Link {i + 1}", EditorStyles.miniBoldLabel);

                            if (GUILayout.Button("X", GUILayout.Width(24)))
                            {
                                section.links.RemoveAt(i);
                                GUIUtility.ExitGUI();
                            }
                        }

                        link.label = EditorGUILayout.TextField("Label", link.label);
                        link.url = EditorGUILayout.TextField("URL", link.url);
                    }
                }

                if (GUILayout.Button("+ Add Link"))
                {
                    section.links.Add(new LinkDraft
                    {
                        label = "Documentation",
                        url = "https://"
                    });
                }
            }
        }

        private void DrawBuildControls()
        {
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);

            if (mode == BuilderMode.CreateNew)
            {
                using (new EditorGUI.DisabledScope(!CanCreate()))
                {
                    if (GUILayout.Button("Create Readme.asset", GUILayout.Height(32)))
                    {
                        CreateReadmeAsset();
                    }
                }

                if (!CanCreate())
                {
                    EditorGUILayout.HelpBox(
                        "Choose a valid output folder before creating the Readme asset.",
                        MessageType.Info
                    );
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(existingReadme == null))
                {
                    if (GUILayout.Button("Apply Changes To Existing Readme.asset", GUILayout.Height(32)))
                    {
                        ApplyChangesToExistingReadme();
                    }
                }

                if (existingReadme == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign an existing FPReadmeAsset to edit it.",
                        MessageType.Info
                    );
                }
            }
        }

        private bool CanCreate()
        {
            if (outputFolder == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(outputFolder);
            return AssetDatabase.IsValidFolder(path);
        }

        private void CreateReadmeAsset()
        {
            var folderPath = AssetDatabase.GetAssetPath(outputFolder);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{folderPath}/Readme.asset"
            );

            var readme = CreateInstance<FPReadmeAsset>();

            WriteDraftToAsset(readme);

            AssetDatabase.CreateAsset(readme, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            existingReadme = readme;
            mode = BuilderMode.EditExisting;

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = readme;
            EditorGUIUtility.PingObject(readme);

            Debug.Log($"Created Readme asset at: {assetPath}", readme);
        }

        private void ApplyChangesToExistingReadme()
        {
            if (existingReadme == null)
            {
                return;
            }

            Undo.RecordObject(existingReadme, "Edit Readme Asset");

            WriteDraftToAsset(existingReadme);

            EditorUtility.SetDirty(existingReadme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = existingReadme;
            EditorGUIUtility.PingObject(existingReadme);

            Debug.Log($"Updated Readme asset: {AssetDatabase.GetAssetPath(existingReadme)}", existingReadme);
        }

        private void WriteDraftToAsset(FPReadmeAsset readme)
        {
            readme.icon = icon;
            readme.title = readmeTitle;
            readme.subtitle = subtitle;
            readme.version = version;
            readme.overview = overview;

            readme.sections = new List<FPReadmeSection>();

            foreach (var draft in sections)
            {
                var section = new FPReadmeSection
                {
                    heading = draft.heading,
                    body = draft.body,
                    links = new List<FPReadmeLink>()
                };

                foreach (var linkDraft in draft.links)
                {
                    section.links.Add(new FPReadmeLink
                    {
                        label = linkDraft.label,
                        url = linkDraft.url
                    });
                }

                readme.sections.Add(section);
            }
        }

        private void LoadExistingReadme(FPReadmeAsset readme)
        {
            existingReadme = readme;
            mode = BuilderMode.EditExisting;

            sections.Clear();

            if (readme == null)
            {
                return;
            }

            icon = readme.icon;
            readmeTitle = readme.title;
            subtitle = readme.subtitle;
            version = readme.version;
            overview = readme.overview;

            if (readme.sections != null)
            {
                foreach (var existingSection in readme.sections)
                {
                    var sectionDraft = new SectionDraft
                    {
                        heading = existingSection.heading,
                        body = existingSection.body,
                        foldout = true,
                        links = new List<LinkDraft>()
                    };

                    if (existingSection.links != null)
                    {
                        foreach (var existingLink in existingSection.links)
                        {
                            sectionDraft.links.Add(new LinkDraft
                            {
                                label = existingLink.label,
                                url = existingLink.url
                            });
                        }
                    }

                    sections.Add(sectionDraft);
                }
            }

            if (sections.Count == 0)
            {
                sections.Add(new SectionDraft
                {
                    heading = "Overview",
                    body = "",
                    foldout = true
                });
            }

            Repaint();
        }

        private void ResetToDefaultDraft()
        {
            icon = null;
            readmeTitle = "FuzzPhyte Package";
            subtitle = "Package documentation and setup notes.";
            version = "0.0.1";
            overview = "This package contains tools, scripts, and assets for a FuzzPhyte Unity module.";

            sections.Clear();

            sections.Add(new SectionDraft
            {
                heading = "Overview",
                body = "Describe what this package or scene contains.",
                foldout = true
            });

            sections.Add(new SectionDraft
            {
                heading = "Setup",
                body = "Explain any setup steps, dependencies, or required scene configuration.",
                foldout = true
            });

            sections.Add(new SectionDraft
            {
                heading = "Usage",
                body = "Explain how developers or designers should use this package.",
                foldout = true
            });
        }

        private void MoveSection(int index, int direction)
        {
            var newIndex = index + direction;

            if (newIndex < 0 || newIndex >= sections.Count)
            {
                return;
            }

            (sections[index], sections[newIndex]) = (sections[newIndex], sections[index]);
        }

        private static string DrawWrappedScrollableTextArea(
            string label,
            string value,
            ref Vector2 scrollPosition,
            float minHeight = 120f,
            float maxHeight = 220f
            )
        {
            EditorGUILayout.LabelField(label);

            var textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                stretchHeight = true
            };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                scrollPosition = EditorGUILayout.BeginScrollView(
                    scrollPosition,
                    GUILayout.MinHeight(minHeight),
                    GUILayout.MaxHeight(maxHeight)
                );

                value = EditorGUILayout.TextArea(
                    value ?? string.Empty,
                    textAreaStyle,
                    GUILayout.ExpandHeight(true)
                );

                EditorGUILayout.EndScrollView();
            }

            return value;
        }
        private static string DrawWrappedScrollableTextAreaWithLargeEditor(
            string label,
            string value,
            ref Vector2 scrollPosition,
            System.Action<string> onTextUpdated,
            float minHeight = 120f,
            float maxHeight = 220f
            )
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

                if (GUILayout.Button("Open Large Editor", GUILayout.Width(140)))
                {
                    FPReadmeLargeTextEditorWindow.Open(
                        label,
                        value ?? string.Empty,
                        updatedText =>
                        {
                            onTextUpdated?.Invoke(updatedText);
                        }
                    );
                }
            }

            var textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                stretchHeight = true
            };

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                scrollPosition = EditorGUILayout.BeginScrollView(
                    scrollPosition,
                    GUILayout.MinHeight(minHeight),
                    GUILayout.MaxHeight(maxHeight)
                );

                value = EditorGUILayout.TextArea(
                    value ?? string.Empty,
                    textAreaStyle,
                    GUILayout.ExpandHeight(true)
                );

                EditorGUILayout.EndScrollView();
            }

            return value;
        }
        private class SectionDraft
        {
            public string heading;
            public string body;
            public bool foldout = true;
            public Vector2 scrollPosition;
            public List<LinkDraft> links = new();
        }

        private class LinkDraft
        {
            public string label;
            public string url;
        }
    }
}
