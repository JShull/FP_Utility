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
        [SerializeField]
        private Color selectionColor = new Color(0.01f, 0.61f, 0.98f, 0.28f);
        [SerializeField]
        private bool regionsFoldout = true;

        private readonly List<FPSVGRegion> regions = new List<FPSVGRegion>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> errors = new List<string>();
        private Rect svgBounds = new Rect(0f, 0f, 1f, 1f);
        private Vector2 parameterScrollPosition;
        private Vector2 messageScrollPosition;
        private string loadedSourcePath;
        private bool outputMeshNameManuallyEdited;
        private bool generateMeshQueued;

        private const float ParameterPanelWidth = 352f;
        private const float BottomDebugHeight = 108f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private const float PreviewHeightScrollSensitivity = 0.08f;

        [MenuItem("FuzzPhyte/Utility/Mesh/SVG Extruder", priority = FP_UtilityData.MENU_UTILITY_RENDERING + 4)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPSVGExtruderWindow>("SVG Extruder");
            window.minSize = new Vector2(760f, 520f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            settings ??= FPSVGExtruderSettings.Default;
            previewHeight = Mathf.Clamp(previewHeight <= 0f ? 360f : previewHeight, 160f, 900f);
            if (string.IsNullOrWhiteSpace(settings.OutputFolder) || settings.OutputFolder == "Assets/GeneratedMeshes")
            {
                settings.OutputFolder = "Assets/_FPUtility";
            }

            SyncSelectionDefaults();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= ExecuteQueuedGenerateMesh;
            generateMeshQueued = false;
        }

        private void OnGUI()
        {
            GUILayout.Label("SVG Extruder", EditorStyles.boldLabel);
            DrawWorkspace();
        }

        private void DrawWorkspace()
        {
            Rect previousRect = GUILayoutUtility.GetLastRect();
            float workspaceTop = previousRect.yMax + 4f;
            Rect workspaceRect = new Rect(
                WorkspacePadding,
                workspaceTop,
                Mathf.Max(100f, position.width - (WorkspacePadding * 2f)),
                Mathf.Max(100f, position.height - workspaceTop - WorkspacePadding));

            float topHeight = Mathf.Max(180f, workspaceRect.height - BottomDebugHeight - PanelGap);
            Rect topRect = new Rect(workspaceRect.x, workspaceRect.y, workspaceRect.width, topHeight);
            Rect debugRect = new Rect(workspaceRect.x, topRect.yMax + PanelGap, workspaceRect.width, workspaceRect.height - topHeight - PanelGap);

            float leftWidth = Mathf.Clamp(ParameterPanelWidth, 260f, Mathf.Max(260f, topRect.width - 280f - PanelGap));
            Rect parameterRect = new Rect(topRect.x, topRect.y, leftWidth, topRect.height);
            Rect previewRect = new Rect(parameterRect.xMax + PanelGap, topRect.y, Mathf.Max(100f, topRect.xMax - parameterRect.xMax - PanelGap), topRect.height);

            DrawParameterPanelContainer(parameterRect);
            DrawPreviewPanelContainer(previewRect);
            DrawDebugPanel(debugRect);
        }

        private void DrawParameterPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, GetParameterViewHeight());

            parameterScrollPosition = GUI.BeginScrollView(innerRect, parameterScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawParameterPanel();
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawParameterPanel()
        {
            DrawSourceSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawMeshSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawSceneSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawActions();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawRegionPanel();
        }

        private void DrawPreviewPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            HandlePreviewHeightInput(rect);

            float availablePreviewHeight = Mathf.Max(24f, rect.height - 20f);
            float drawnPreviewHeight = Mathf.Min(availablePreviewHeight, previewHeight);
            float previewY = rect.y + 10f + ((availablePreviewHeight - drawnPreviewHeight) * 0.5f);
            Rect previewRect = new Rect(rect.x + 10f, previewY, rect.width - 20f, drawnPreviewHeight);

            if (FPSVGRegionPreview.Draw(previewRect, regions, svgBounds, regionLabelColor, selectionColor, out int clickedIndex))
            {
                regions[clickedIndex].Included = !regions[clickedIndex].Included;
                Repaint();
            }

            DrawPreviewOverlay(rect);
        }

        private void DrawDebugPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, Mathf.Max(innerRect.height, 34f + ((errors.Count + warnings.Count + 1) * 38f)));

            messageScrollPosition = GUI.BeginScrollView(innerRect, messageScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawMessages();
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawSourceSettings()
        {
            EditorGUILayout.LabelField("SVG Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            svgSource = EditorGUILayout.ObjectField("SVG File", svgSource, typeof(Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                settings.SvgFile = svgSource as TextAsset;
                outputMeshNameManuallyEdited = false;
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
            settings.TriangulationBackend = (FPSVGTriangulationBackend)EditorGUILayout.EnumPopup("Triangulation Backend", settings.TriangulationBackend);
            if (settings.TriangulationBackend == FPSVGTriangulationBackend.UnityVectorGraphics && !FPSVGUnityVectorGraphicsTriangulator.IsAvailable)
            {
                EditorGUILayout.HelpBox(FPSVGUnityVectorGraphicsTriangulator.UnavailableReason, MessageType.Info);
            }

            settings.PathSampleDistance = EditorGUILayout.FloatField("Path Sample Distance", settings.PathSampleDistance);
            settings.BoundarySimplifyTolerance = EditorGUILayout.FloatField("Simplify Tolerance", settings.BoundarySimplifyTolerance);
            settings.CollinearTolerance = EditorGUILayout.FloatField("Collinear Tolerance", settings.CollinearTolerance);
            settings.OptimizeSurfaceTriangulation = EditorGUILayout.Toggle("Optimize Triangles", settings.OptimizeSurfaceTriangulation);
            using (new EditorGUI.DisabledScope(!settings.OptimizeSurfaceTriangulation))
            {
                settings.SurfaceOptimizationPasses = EditorGUILayout.IntSlider("Optimize Passes", settings.SurfaceOptimizationPasses, 0, 32);
            }

            settings.UseZOrderEarSearch = EditorGUILayout.Toggle("Use Z-Order Ear Search", settings.UseZOrderEarSearch);
            settings.CenterPivot = EditorGUILayout.Toggle("Center Pivot", settings.CenterPivot);
            settings.GenerateDoubleSided = EditorGUILayout.Toggle("Generate Double Sided", settings.GenerateDoubleSided);
            settings.RecalculateNormals = EditorGUILayout.Toggle("Recalculate Normals", settings.RecalculateNormals);
            EditorGUI.BeginChangeCheck();
            settings.OutputMeshName = DrawWideTextField("Output Mesh Name", settings.OutputMeshName);
            if (EditorGUI.EndChangeCheck())
            {
                outputMeshNameManuallyEdited = true;
            }

            settings.OutputFolder = DrawWideTextField("Output Folder", settings.OutputFolder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SVG Viewer / Selector", EditorStyles.boldLabel);
            regionLabelColor = EditorGUILayout.ColorField("Label Color", regionLabelColor);
            selectionColor = EditorGUILayout.ColorField("Selection Color", selectionColor);
        }

        private void DrawRegionList()
        {
            if (regions.Count == 0)
            {
                EditorGUILayout.HelpBox("Click a closed outline in the preview to toggle it as filled.", MessageType.None);
                return;
            }

            int includedCount = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                FPSVGRegion region = regions[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    region.Included = EditorGUILayout.Toggle(region.Included, GUILayout.Width(20f));
                    Rect swatchRect = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
                    DrawRegionColorSwatch(swatchRect, region);
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

        private void DrawRegionPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    regionsFoldout = EditorGUILayout.Foldout(
                        regionsFoldout,
                        "Regions",
                        true
                    );

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        $"{CountIncludedRegions()} / {regions.Count}",
                        EditorStyles.miniLabel,
                        GUILayout.Width(72f)
                    );
                }

                if (!regionsFoldout)
                {
                    return;
                }

                DrawRegionList();
            }
        }

        private float GetParameterViewHeight()
        {
            float baseHeight = 760f;
            if (!regionsFoldout)
            {
                return baseHeight;
            }

            return baseHeight + (regions.Count * 22f);
        }

        private void HandlePreviewHeightInput(Rect rect)
        {
            Event current = Event.current;
            if (current.type != EventType.ScrollWheel || !rect.Contains(current.mousePosition))
            {
                return;
            }

            float scale = Mathf.Exp(-current.delta.y * PreviewHeightScrollSensitivity);
            previewHeight = Mathf.Clamp(previewHeight * scale, 160f, 900f);
            current.Use();
            Repaint();
        }

        private void DrawPreviewOverlay(Rect rect)
        {
            Rect overlayRect = new Rect(rect.x + 8f, rect.y + 8f, 186f, 58f);
            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(overlayRect.x + 6f, overlayRect.y + 5f, overlayRect.width - 12f, 18f);
            GUI.Label(lineRect, $"Regions: {regions.Count}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Included: {CountIncludedRegions()}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Pixels: {Mathf.RoundToInt(previewHeight)}", EditorStyles.miniLabel);
        }

        private static void DrawRegionColorSwatch(Rect rect, FPSVGRegion region)
        {
            Color swatch = region.HasFillColor
                ? region.FillColor
                : new Color(0.2f, 0.2f, 0.2f, 1f);
            EditorGUI.DrawRect(rect, swatch);
            Handles.BeginGUI();
            Handles.color = region.HasStrokeColor ? region.StrokeColor : Color.gray;
            Handles.DrawAAPolyLine(1f,
                new Vector3(rect.xMin, rect.yMin),
                new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin));
            Handles.EndGUI();
        }

        private static string DrawWideTextField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(118f));
                return EditorGUILayout.TextField(value);
            }
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
            using (new EditorGUI.DisabledScope(regions.Count == 0 || generateMeshQueued))
            {
                string buttonLabel = generateMeshQueued ? "Generating Mesh..." : "Generate Mesh";
                if (GUILayout.Button(buttonLabel, GUILayout.Height(32f)))
                {
                    QueueGenerateMesh();
                }
            }

            GUI.color = originalColor;
        }

        private void DrawMessages()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Regions: {regions.Count}", GUILayout.Width(90f));
                EditorGUILayout.LabelField($"Included: {CountIncludedRegions()}", GUILayout.Width(92f));
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("No SVG extruder warnings or errors.", MessageType.None);
                return;
            }

            for (int i = 0; i < errors.Count; i++)
            {
                EditorGUILayout.HelpBox(errors[i], MessageType.Error);
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
            }
        }

        private int CountIncludedRegions()
        {
            int count = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Included)
                {
                    count++;
                }
            }

            return count;
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
                        outputMeshNameManuallyEdited = false;
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
                ApplyDefaultOutputMeshName(loadedSourcePath);
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
                        ApplyDefaultOutputMeshName(loadedSourcePath);
                        return true;
                    }
                }

                errors.Add("The selected object is not a TextAsset or readable .svg/.xml asset.");
                return false;
            }

            errors.Add("Assign or drop an SVG file before parsing.");
            return false;
        }

        private void ApplyDefaultOutputMeshName(string sourcePath)
        {
            if (outputMeshNameManuallyEdited || string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                settings.OutputMeshName = $"{fileName}_GeneratedSVGMesh";
            }
        }

        private void QueueGenerateMesh()
        {
            if (generateMeshQueued)
            {
                return;
            }

            generateMeshQueued = true;
            EditorApplication.delayCall += ExecuteQueuedGenerateMesh;
        }

        private void ExecuteQueuedGenerateMesh()
        {
            generateMeshQueued = false;
            if (this == null)
            {
                return;
            }

            GenerateMesh();
            Repaint();
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

            Mesh savedMesh = FPSVGMeshAssetUtility.SaveMeshAsset(mesh, safeSettings.OutputFolder, safeSettings.OutputMeshName, out string saveMessage);
            if (savedMesh == null)
            {
                errors.Add(string.IsNullOrWhiteSpace(saveMessage)
                    ? "Mesh generation succeeded, but saving the mesh asset failed."
                    : saveMessage);
                return;
            }

            if (!string.IsNullOrWhiteSpace(saveMessage))
            {
                warnings.Add(saveMessage);
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

            warnings.Add($"Scene object created: {go.name}");
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
