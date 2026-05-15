namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public class FPSVGExtruderWindow : EditorWindow
    {
        [SerializeField]
        private FPSVGExtruderSettings settings = FPSVGExtruderSettings.Default;
        [SerializeField]
        private Object svgSource;
        [SerializeField]
        private Material previewMaterial;
        [SerializeField]
        private Transform targetParent;
        [SerializeField]
        private bool createSceneObject = true;
        [SerializeField]
        private bool addMeshCollider;
        [SerializeField]
        private float previewHeight = 360f;
        [SerializeField]
        private Color regionLabelColor = new Color(1f, 0.82f, 0.25f, 1f);

        private readonly List<FPSVGRegion> regions = new List<FPSVGRegion>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> errors = new List<string>();
        private Rect svgBounds = new Rect(0f, 0f, 1f, 1f);
        private Vector2 scrollPosition;
        private string loadedSourcePath;

        [MenuItem("FuzzPhyte/Utility/Rendering/SVG Extruder", priority = FP_UtilityData.ORDER_MENU + 8)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPSVGExtruderWindow>("FP SVG Extruder");
            window.minSize = new Vector2(420f, 520f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            settings ??= FPSVGExtruderSettings.Default;
            previewHeight = Mathf.Clamp(previewHeight <= 0f ? 360f : previewHeight, 160f, 900f);
            SyncSelectionDefaults();
        }

        private void OnGUI()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollView.scrollPosition;

                DrawHeader();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawSourceSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawMeshSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawPreview();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawSceneSettings();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawActions();
                DrawMessages();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("FP SVG Extruder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Parse closed SVG paths into selectable regions, mark the regions to fill, then generate an extruded mesh asset. " +
                "Nested unselected regions inside selected regions are treated as holes where the triangulator can bridge them.",
                MessageType.Info);
        }

        private void DrawSourceSettings()
        {
            EditorGUILayout.LabelField("SVG Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            svgSource = EditorGUILayout.ObjectField("SVG File", svgSource, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                settings.SvgFile = svgSource as TextAsset;
                ParseCurrentSource();
            }

            Rect dropRect = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop SVG/TextAsset Here", EditorStyles.helpBox);
            HandleDragAndDrop(dropRect);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Parse SVG"))
                {
                    ParseCurrentSource();
                }

                using (new EditorGUI.DisabledScope(regions.Count == 0))
                {
                    if (GUILayout.Button("Select All"))
                    {
                        SetAllRegionsIncluded(true);
                    }

                    if (GUILayout.Button("Clear"))
                    {
                        SetAllRegionsIncluded(false);
                    }
                }
            }

            EditorGUILayout.LabelField("Parsed Regions", regions.Count.ToString());
            if (!string.IsNullOrWhiteSpace(loadedSourcePath))
            {
                EditorGUILayout.LabelField("Loaded Path", loadedSourcePath);
            }
        }

        private void DrawMeshSettings()
        {
            EditorGUILayout.LabelField("Mesh Settings", EditorStyles.boldLabel);
            settings.Scale = EditorGUILayout.FloatField("Scale", settings.Scale);
            settings.ExtrusionDepth = EditorGUILayout.FloatField("Extrusion Depth", settings.ExtrusionDepth);
            settings.PathSampleDistance = EditorGUILayout.FloatField("Path Sample Distance", settings.PathSampleDistance);
            settings.CenterPivot = EditorGUILayout.Toggle("Center Pivot", settings.CenterPivot);
            settings.GenerateDoubleSided = EditorGUILayout.Toggle("Generate Double Sided", settings.GenerateDoubleSided);
            settings.RecalculateNormals = EditorGUILayout.Toggle("Recalculate Normals", settings.RecalculateNormals);
            settings.OutputMeshName = EditorGUILayout.TextField("Output Mesh Name", settings.OutputMeshName);
            settings.OutputFolder = EditorGUILayout.TextField("Output Folder", settings.OutputFolder);
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Region Preview", EditorStyles.boldLabel);
            previewHeight = EditorGUILayout.Slider("Preview Height", previewHeight, 160f, 900f);
            regionLabelColor = EditorGUILayout.ColorField("Label Color", regionLabelColor);

            Rect previewRect = GUILayoutUtility.GetRect(100f, previewHeight, GUILayout.ExpandWidth(true));
            if (FPSVGRegionPreview.Draw(previewRect, regions, svgBounds, regionLabelColor, out int clickedIndex))
            {
                regions[clickedIndex].Included = !regions[clickedIndex].Included;
                Repaint();
            }

            EditorGUILayout.HelpBox("Click a closed outline to toggle it as filled. If a selected region contains an unselected child outline, that child is used as a hole.", MessageType.None);
            DrawRegionList();
        }

        private void DrawRegionList()
        {
            if (regions.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Regions", EditorStyles.boldLabel);
            int includedCount = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                FPSVGRegion region = regions[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    region.Included = EditorGUILayout.Toggle(region.Included, GUILayout.Width(20f));
                    EditorGUILayout.LabelField(region.Id, GUILayout.MinWidth(120f));
                    EditorGUILayout.LabelField($"{region.OuterLoop.Count} pts", EditorStyles.miniLabel, GUILayout.Width(64f));
                }

                if (region.Included)
                {
                    includedCount++;
                }
            }

            EditorGUILayout.LabelField("Included Regions", includedCount.ToString());
        }

        private void DrawSceneSettings()
        {
            EditorGUILayout.LabelField("Scene Output", EditorStyles.boldLabel);
            createSceneObject = EditorGUILayout.Toggle("Create Scene Object", createSceneObject);
            targetParent = (Transform)EditorGUILayout.ObjectField("Parent", targetParent, typeof(Transform), true);
            previewMaterial = (Material)EditorGUILayout.ObjectField("Material", previewMaterial, typeof(Material), false);
            addMeshCollider = EditorGUILayout.Toggle("Add MeshCollider", addMeshCollider);

            if (GUILayout.Button("Use Current Selection As Parent"))
            {
                SyncSelectionDefaults();
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.Space();

            Color originalColor = GUI.color;
            GUI.color = FP_Utility_Editor.OkayColor;
            using (new EditorGUI.DisabledScope(regions.Count == 0))
            {
                if (GUILayout.Button("Generate Mesh", GUILayout.Height(32f)))
                {
                    GenerateMesh();
                }
            }

            GUI.color = originalColor;
        }

        private void DrawMessages()
        {
            for (int i = 0; i < errors.Count; i++)
            {
                EditorGUILayout.HelpBox(errors[i], MessageType.Error);
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
            }
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
            Event current = Event.current;
            if (!dropRect.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.DragUpdated || current.type == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    if (DragAndDrop.objectReferences.Length > 0)
                    {
                        svgSource = DragAndDrop.objectReferences[0];
                        settings.SvgFile = svgSource as TextAsset;
                        ParseCurrentSource();
                    }
                }

                current.Use();
            }
        }

        private void ParseCurrentSource()
        {
            warnings.Clear();
            errors.Clear();
            regions.Clear();
            loadedSourcePath = string.Empty;

            if (!TryReadSvgText(out string svgText))
            {
                Repaint();
                return;
            }

            FPSVGParseResult result = FPSVGPathParser.Parse(svgText, settings.Sanitized().PathSampleDistance);
            regions.AddRange(result.Regions);
            warnings.AddRange(result.Warnings);
            errors.AddRange(result.Errors);
            svgBounds = result.Bounds;
            Repaint();
        }

        private bool TryReadSvgText(out string svgText)
        {
            svgText = string.Empty;
            if (svgSource == null && settings.SvgFile != null)
            {
                svgSource = settings.SvgFile;
            }

            if (svgSource is TextAsset textAsset)
            {
                svgText = textAsset.text;
                loadedSourcePath = AssetDatabase.GetAssetPath(textAsset);
                return true;
            }

            if (svgSource != null)
            {
                string path = AssetDatabase.GetAssetPath(svgSource);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    string extension = Path.GetExtension(path).ToLowerInvariant();
                    if (extension == ".svg" || extension == ".txt" || extension == ".xml")
                    {
                        svgText = File.ReadAllText(path);
                        loadedSourcePath = path;
                        return true;
                    }
                }

                errors.Add("The selected object is not a TextAsset or readable .svg/.xml asset.");
                return false;
            }

            errors.Add("Assign or drop an SVG file before parsing.");
            return false;
        }

        private void GenerateMesh()
        {
            warnings.Clear();
            errors.Clear();

            List<FPSVGRegion> solidRegions = FPSVGRegionDetector.BuildSolidRegions(regions);
            if (solidRegions.Count == 0)
            {
                errors.Add("Select at least one SVG region before generating a mesh.");
                return;
            }

            FPSVGExtruderSettings safeSettings = settings.Sanitized();
            Mesh mesh = FPSVGExtrudedMeshBuilder.Build(solidRegions, safeSettings, out FPSVGMeshBuildReport report);
            warnings.AddRange(report.Warnings);
            errors.AddRange(report.Errors);
            if (mesh == null || !report.Success)
            {
                if (errors.Count == 0)
                {
                    errors.Add("Mesh generation failed.");
                }

                return;
            }

            Mesh savedMesh = FPSVGMeshAssetUtility.SaveMeshAsset(mesh, safeSettings.OutputFolder, safeSettings.OutputMeshName);
            if (savedMesh == null)
            {
                errors.Add("Mesh generation succeeded, but saving the mesh asset failed.");
                return;
            }

            if (createSceneObject)
            {
                CreateSceneObject(savedMesh);
            }
        }

        private void CreateSceneObject(Mesh mesh)
        {
            GameObject go = new GameObject(mesh.name);
            Undo.RegisterCreatedObjectUndo(go, "Create FP SVG Mesh");

            if (targetParent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, targetParent.gameObject);
                go.transform.SetParent(targetParent, false);
            }

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            if (previewMaterial != null)
            {
                meshRenderer.sharedMaterial = previewMaterial;
            }

            if (addMeshCollider)
            {
                MeshCollider meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            FP_Utility_Editor.FocusOnObject(go);
        }

        private void SetAllRegionsIncluded(bool included)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                regions[i].Included = included;
            }

            Repaint();
        }

        private void SyncSelectionDefaults()
        {
            if (Selection.activeTransform != null)
            {
                targetParent = Selection.activeTransform;
            }
        }
    }
}
