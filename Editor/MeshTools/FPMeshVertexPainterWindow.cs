namespace FuzzPhyte.Utility.Editor.MeshTools
{
    using System.Collections.Generic;
    using FuzzPhyte.Utility;
    using FuzzPhyte.Utility.Editor;
    using FuzzPhyte.Utility.MeshTools;
    using UnityEditor;
    using UnityEngine;

    public class FPMeshVertexPainterWindow : EditorWindow
    {
        private enum ToolMode
        {
            Select = 0,
            Plane = 1,
            Point = 2,
            Edge = 3,
            Triangle = 4
        }

        private enum PreviewInteractionMode
        {
            SelectVertices = 0,
            OrbitCamera = 1
        }

        private enum PreviewSelectionShape
        {
            Brush = 0,
            Lasso = 1
        }

        private enum EdgeInputMode
        {
            Click = 0,
            Paint = 1
        }

        private enum SurfaceBuildMode
        {
            BoundaryTriangles = 0,
            QuadPatch = 1,
            EdgeTriangles = 2
        }

        private readonly List<MeshFilter> sourceMeshes = new();
        private readonly Dictionary<int, HashSet<int>> selectedByMesh = new();
        private readonly Dictionary<FPMeshSurfaceEdgeEndpoint, int> surfaceIndexByEndpoint = new();
        private readonly Dictionary<FPMeshSurfaceEdgeEndpoint, FPMeshNavigationTags> tagsByEndpoint = new();
        private readonly List<Vector3> planePicks = new();
        private readonly List<Vector2> previewLassoPoints = new();

        private FPMeshVertexPaintAuthoring authoring;
        private FPMeshGeneratedPlane generatedPlane;
        private int activeMeshIndex;
        private ToolMode mode;
        private Vector2 scroll;
        private bool drawEdges = true;
        private bool drawPreviewVertices = true;
        private bool drawPreviewAllMeshes = true;
        private bool invertPreviewOrbit;
        private bool viewportOrbitHandlesEnabled = true;
        private bool flipGeneratedSurfaceNormals = true;
        private bool useSelectedTrianglesOnly;
        private PreviewInteractionMode previewInteractionMode;
        private PreviewSelectionShape previewSelectionShape;
        private EdgeInputMode edgeInputMode;
        private SurfaceBuildMode surfaceBuildMode;
        private int quadSurfaceColumns = 4;
        private int quadSurfaceRows = 4;
        private FPMeshNavigationTags generatedPointTags = FPMeshNavigationTags.Ground;
        private string generatedSurfaceOutputFolder = "Assets/_FPUtility/Meshes";
        private string generatedSurfaceMeshName = "FP_GeneratedVertexSurface";
        private bool createGeneratedSurfaceSceneObject = true;
        private float vertexSize = 0.045f;
        private float previewSelectionRadius = 14f;
        private float previewZoom = 1.35f;
        private Quaternion previewRotation = Quaternion.Euler(24f, -36f, 0f);
        private FPMeshPreviewProjection previewProjection;
        private PreviewRenderUtility previewUtility;
        private Material activePreviewMaterial;
        private Material inactivePreviewMaterial;
        private Material dimPreviewMaterial;
        private int activeOrbitAxis = -1;
        private int hoverViewportOrbitAxis = -1;
        private FPMeshSurfaceEdgeEndpoint pendingGeneratedEdgeStart;
        private bool hasPendingGeneratedEdgeStart;
        private bool isPaintingPreviewSelection;
        private bool isPaintingPreviewEdges;
        private bool isPaintingPreviewTriangles;
        private bool isDrawingPreviewLasso;
        private bool previewSelectionRemoves;
        private bool edgePaintRemoves;
        private bool trianglePaintRemoves;
        private bool previewLassoRemoves;
        private Vector3 previewPanOffset;

        private const float PreviewZoomMin = 0.08f;
        private const float PreviewZoomMax = 24f;
        private const float ViewportOrbitScreenRadius = 124f;
        private const float ViewportOrbitPickRadius = 18f;
        private const float PreviewLassoMinPointDistance = 2.5f;

        private readonly struct PreviewOverlayPoint
        {
            public readonly Vector2 Point;
            public readonly float Depth;
            public readonly Color Color;

            public PreviewOverlayPoint(Vector2 point, float depth, Color color)
            {
                Point = point;
                Depth = depth;
                Color = color;
            }
        }

        private readonly struct PreviewTrianglePick
        {
            public readonly FPMeshGeneratedTriangleRecord Triangle;
            public readonly Vector2 A;
            public readonly Vector2 B;
            public readonly Vector2 C;
            public readonly float Area;

            public PreviewTrianglePick(FPMeshGeneratedTriangleRecord triangle, Vector2 a, Vector2 b, Vector2 c, float area)
            {
                Triangle = triangle;
                A = a;
                B = b;
                C = c;
                Area = area;
            }
        }

        [MenuItem("FuzzPhyte/Utility/Mesh/Mesh Vertex Painter", priority = FP_UtilityData.MENU_UTILITY_MESH + 5)]
        public static void Open()
        {
            GetWindow<FPMeshVertexPainterWindow>("Mesh Vertex Painter");
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
            CleanupPreviewUtility();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftPane();
                DrawRightPane();
            }
        }

        private void DrawLeftPane()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(280)))
            {
                EditorGUILayout.LabelField("Mesh Graph Tool", EditorStyles.boldLabel);
                authoring = (FPMeshVertexPaintAuthoring)EditorGUILayout.ObjectField("Authoring", authoring, typeof(FPMeshVertexPaintAuthoring), true);

                if (GUILayout.Button("Use Selection"))
                {
                    PullFromSelection();
                }

                if (authoring != null && GUILayout.Button("Read From Authoring"))
                {
                    ReadFromAuthoring();
                }

                FPMeshPreviewEditorUtility.DrawSectionDivider();
                DrawMeshList();

                FPMeshPreviewEditorUtility.DrawSectionDivider();
                ToolMode nextMode = (ToolMode)GUILayout.Toolbar((int)mode, new[] { "Select", "Plane", "Point", "Edge", "Tri" });
                if (nextMode != mode)
                {
                    mode = nextMode;
                    ClearPendingGeneratedEdgeStart();
                    isPaintingPreviewEdges = false;
                    edgePaintRemoves = false;
                    isPaintingPreviewTriangles = false;
                    trianglePaintRemoves = false;
                    Repaint();
                }
                drawEdges = EditorGUILayout.Toggle("Draw Edges", drawEdges);
                drawPreviewVertices = EditorGUILayout.Toggle("Draw Vertices", drawPreviewVertices);
                drawPreviewAllMeshes = EditorGUILayout.Toggle("Preview All Meshes", drawPreviewAllMeshes);
                vertexSize = EditorGUILayout.Slider("Vertex Size", vertexSize, 0.01f, 0.25f);
                previewSelectionRadius = EditorGUILayout.Slider("Preview Brush Radius", previewSelectionRadius, 4f, 48f);
                DrawSelectionTools();

                DrawPlaneTools();
                DrawPointTools();
                DrawEdgeTools();
                DrawTriangleTools();

                FPMeshPreviewEditorUtility.DrawSectionDivider();
                DrawSurfaceTools();

                FPMeshPreviewEditorUtility.DrawSectionDivider();
                EditorGUILayout.LabelField("Maintenance", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(authoring == null))
                {
                    if (GUILayout.Button("Clear Generated Points"))
                    {
                        Undo.RecordObject(authoring, "Clear Generated Points");
                        authoring.ClearTransientGeneratedPoints();
                        ClearPendingGeneratedEdgeStart();
                        RefreshSurfaceIndexMapFromAuthoring();
                        EditorUtility.SetDirty(authoring);
                        Repaint();
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("Clear All Painted Data"))
                    {
                        Undo.RecordObject(authoring, "Clear Painted Mesh Data");
                        authoring.ClearPaintedData();
                        selectedByMesh.Clear();
                        surfaceIndexByEndpoint.Clear();
                        tagsByEndpoint.Clear();
                        generatedPlane = default;
                        planePicks.Clear();
                        ClearPendingGeneratedEdgeStart();
                        EditorUtility.SetDirty(authoring);
                        SceneView.RepaintAll();
                    }
                }
            }
        }

        private void DrawSelectionTools()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Preview Tools", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            FPMeshNavigationTags nextTags = (FPMeshNavigationTags)EditorGUILayout.EnumFlagsField("Default Tags", generatedPointTags);
            if (EditorGUI.EndChangeCheck())
            {
                SetWorkingDefaultTags(nextTags);
            }

            if (mode != ToolMode.Select)
            {
                return;
            }

            PreviewSelectionShape nextSelectionShape = (PreviewSelectionShape)EditorGUILayout.EnumPopup("Selection Shape", previewSelectionShape);
            if (nextSelectionShape != previewSelectionShape)
            {
                SetPreviewSelectionShape(nextSelectionShape);
            }
        }

        private void SetWorkingDefaultTags(FPMeshNavigationTags tags)
        {
            generatedPointTags = tags;
            if (authoring != null)
            {
                Undo.RecordObject(authoring, "Set Mesh Vertex Painter Default Tags");
                authoring.SetDefaultTags(tags);
                EditorUtility.SetDirty(authoring);
            }
        }

        private void SetViewportOrbitHandlesEnabled(bool enabled)
        {
            viewportOrbitHandlesEnabled = enabled;
            if (!enabled)
            {
                activeOrbitAxis = -1;
                hoverViewportOrbitAxis = -1;
            }

            Repaint();
        }

        private void SetPreviewSelectionShape(PreviewSelectionShape selectionShape)
        {
            previewSelectionShape = selectionShape;
            isPaintingPreviewSelection = false;
            isDrawingPreviewLasso = false;
            previewSelectionRemoves = false;
            previewLassoRemoves = false;
            previewLassoPoints.Clear();
            Repaint();
        }

        private void ClearPendingGeneratedEdgeStart()
        {
            pendingGeneratedEdgeStart = default;
            hasPendingGeneratedEdgeStart = false;
        }

        private void SelectSurfaceEndpoint(FPMeshSurfaceEdgeEndpoint endpoint, string undoLabel)
        {
            if (authoring == null)
            {
                return;
            }

            Undo.RecordObject(authoring, undoLabel);
            GetOrAssignSurfaceIndex(endpoint);
            AssignWorkingTagsIfMissing(endpoint);
            authoring.SetSourceMeshes(sourceMeshes, activeMeshIndex);
            authoring.SetSelectedSurfacePoint(endpoint);
            EditorUtility.SetDirty(authoring);
        }

        private int GetOrAssignSurfaceIndex(FPMeshSurfaceEdgeEndpoint endpoint)
        {
            if (surfaceIndexByEndpoint.TryGetValue(endpoint, out int surfaceIndex))
            {
                return surfaceIndex;
            }

            if (authoring != null && authoring.TryGetSurfaceIndex(endpoint, out surfaceIndex))
            {
                surfaceIndexByEndpoint[endpoint] = surfaceIndex;
                authoring.EnsureNextSurfaceIndexPast(surfaceIndex);
                return surfaceIndex;
            }

            surfaceIndex = authoring == null ? NextLocalSurfaceIndex() : authoring.ReserveSurfaceIndex();
            surfaceIndexByEndpoint[endpoint] = surfaceIndex;
            return surfaceIndex;
        }

        private int NextLocalSurfaceIndex()
        {
            int next = 0;
            foreach (int surfaceIndex in surfaceIndexByEndpoint.Values)
            {
                next = Mathf.Max(next, surfaceIndex + 1);
            }

            return next;
        }

        private string FormatSurfaceEndpointLabel(FPMeshSurfaceEdgeEndpoint endpoint)
        {
            int surfaceIndex = GetOrAssignSurfaceIndex(endpoint);
            return endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint
                ? $"S{surfaceIndex} (G{endpoint.GeneratedPointIndex})"
                : $"S{surfaceIndex} (M{endpoint.SourceMeshIndex}:V{endpoint.VertexIndex})";
        }

        private void RefreshSurfaceIndexMapFromAuthoring()
        {
            surfaceIndexByEndpoint.Clear();
            tagsByEndpoint.Clear();
            if (authoring == null)
            {
                return;
            }

            if (authoring.PaintedVertices != null)
            {
                for (int i = 0; i < authoring.PaintedVertices.Count; i++)
                {
                    FPMeshPaintedVertexRecord record = authoring.PaintedVertices[i];
                    FPMeshSurfaceEdgeEndpoint endpoint = FPMeshSurfaceEdgeEndpoint.Source(record.SourceMeshIndex, record.VertexIndex);
                    surfaceIndexByEndpoint[endpoint] = record.SurfaceIndex;
                    tagsByEndpoint[endpoint] = record.Tags;
                }
            }

            if (authoring.GeneratedPoints != null)
            {
                for (int i = 0; i < authoring.GeneratedPoints.Count; i++)
                {
                    FPMeshSurfaceEdgeEndpoint endpoint = FPMeshSurfaceEdgeEndpoint.Generated(i);
                    surfaceIndexByEndpoint[endpoint] = authoring.GeneratedPoints[i].SurfaceIndex;
                    tagsByEndpoint[endpoint] = authoring.GeneratedPoints[i].Tags;
                }
            }
        }

        private void AssignWorkingTagsIfMissing(FPMeshSurfaceEdgeEndpoint endpoint)
        {
            if (!tagsByEndpoint.ContainsKey(endpoint))
            {
                tagsByEndpoint[endpoint] = generatedPointTags;
            }
        }

        private FPMeshNavigationTags ResolveEndpointTags(FPMeshSurfaceEdgeEndpoint endpoint)
        {
            return tagsByEndpoint.TryGetValue(endpoint, out FPMeshNavigationTags tags)
                ? tags
                : generatedPointTags;
        }

        private void SyncSourceMeshesToAuthoring(string undoLabel)
        {
            if (authoring == null)
            {
                return;
            }

            Undo.RecordObject(authoring, undoLabel);
            authoring.SetSourceMeshes(sourceMeshes, activeMeshIndex);
            EditorUtility.SetDirty(authoring);
        }

        private void DrawPlaneTools()
        {
            if (mode != ToolMode.Plane)
            {
                return;
            }

            EditorGUILayout.Space(4);
            int selectedCount = CountSelectedVertices();
            EditorGUILayout.LabelField("Plane", GetCurrentGeneratedPlane(out _) ? "Ready" : $"Pick {planePicks.Count}/3");

            using (new EditorGUI.DisabledScope(selectedCount < 3))
            {
                if (GUILayout.Button("Build Plane From Selection"))
                {
                    BuildPlaneFromSelection();
                }
            }

            if (planePicks.Count > 0 && GUILayout.Button("Clear Plane Picks"))
            {
                planePicks.Clear();
                SceneView.RepaintAll();
                Repaint();
            }

            if (GetCurrentGeneratedPlane(out _) && GUILayout.Button("Clear Generated Plane"))
            {
                ClearGeneratedPlane();
            }
        }

        private void DrawPointTools()
        {
            if (mode != ToolMode.Point)
            {
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Generated Point", EditorStyles.boldLabel);
            if (authoring != null)
            {
                EditorGUILayout.LabelField("Next Surface Index", authoring.NextSurfaceIndex.ToString());
            }

            if (authoring != null && authoring.HasSelectedSurfacePoint)
            {
                EditorGUILayout.LabelField($"Selected: {FormatSurfaceEndpointLabel(authoring.SelectedSurfacePoint)}", EditorStyles.miniLabel);
            }
        }

        private void DrawEdgeTools()
        {
            if (mode != ToolMode.Edge)
            {
                return;
            }

            EditorGUILayout.Space(4);
            int generatedPointCount = authoring == null || authoring.GeneratedPoints == null ? 0 : authoring.GeneratedPoints.Count;
            int generatedEdgeCount = authoring == null || authoring.GeneratedEdges == null ? 0 : authoring.GeneratedEdges.Count;
            EditorGUILayout.LabelField("Guide Edges", $"{generatedEdgeCount} edges / {generatedPointCount} points");
            edgeInputMode = (EdgeInputMode)EditorGUILayout.EnumPopup("Edge Input", edgeInputMode);
            using (new EditorGUI.DisabledScope(authoring == null || generatedEdgeCount == 0))
            {
                if (GUILayout.Button("Clear Guide Edges"))
                {
                    Undo.RecordObject(authoring, "Clear Generated Guide Edges");
                    authoring.ClearGeneratedEdges();
                    ClearPendingGeneratedEdgeStart();
                    EditorUtility.SetDirty(authoring);
                    Repaint();
                }
            }
        }

        private void DrawTriangleTools()
        {
            if (mode != ToolMode.Triangle)
            {
                return;
            }

            EditorGUILayout.Space(4);
            int triangleCount = authoring == null || authoring.GeneratedTriangles == null ? 0 : authoring.GeneratedTriangles.Count;
            EditorGUILayout.LabelField("Guide Triangles", $"{triangleCount} selected");
            using (new EditorGUI.DisabledScope(authoring == null || triangleCount == 0))
            {
                if (GUILayout.Button("Clear Guide Triangles"))
                {
                    Undo.RecordObject(authoring, "Clear Generated Guide Triangles");
                    authoring.ClearGeneratedTriangles();
                    EditorUtility.SetDirty(authoring);
                    Repaint();
                }
            }
        }

        private void DrawSurfaceTools()
        {
            EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel);
            surfaceBuildMode = (SurfaceBuildMode)EditorGUILayout.EnumPopup("Surface Mode", surfaceBuildMode);
            flipGeneratedSurfaceNormals = EditorGUILayout.Toggle("Flip Surface Normals", flipGeneratedSurfaceNormals);
            if (surfaceBuildMode == SurfaceBuildMode.EdgeTriangles)
            {
                useSelectedTrianglesOnly = EditorGUILayout.Toggle("Selected Triangles Only", useSelectedTrianglesOnly);
            }

            if (surfaceBuildMode == SurfaceBuildMode.QuadPatch)
            {
                quadSurfaceColumns = EditorGUILayout.IntSlider("Quad Columns", quadSurfaceColumns, 1, 64);
                quadSurfaceRows = EditorGUILayout.IntSlider("Quad Rows", quadSurfaceRows, 1, 64);
            }

            FPMeshPreviewEditorUtility.DrawSectionDivider();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            generatedSurfaceMeshName = EditorGUILayout.TextField("Mesh Name", generatedSurfaceMeshName);
            generatedSurfaceOutputFolder = EditorGUILayout.TextField("Output Folder", generatedSurfaceOutputFolder);
            createGeneratedSurfaceSceneObject = EditorGUILayout.Toggle("Create Scene Object", createGeneratedSurfaceSceneObject);

            using (new EditorGUI.DisabledScope(!CanCreateSurfaceMesh()))
            {
                if (GUILayout.Button("Create Scene Surface Mesh"))
                {
                    CreateSurfaceMeshFromPoints();
                }

                if (GUILayout.Button("Save Surface Mesh Asset"))
                {
                    SaveSurfaceMeshAsset();
                }

                if (GUILayout.Button("Export Surface OBJ"))
                {
                    ExportSurfaceMeshObj();
                }
            }
        }

        private bool CanCreateSurfaceMesh()
        {
            return GetCurrentGeneratedPlane(out _) &&
                (CountSurfacePoints() >= 3 ||
                (surfaceBuildMode == SurfaceBuildMode.EdgeTriangles &&
                    authoring != null &&
                    authoring.GeneratedTriangles != null &&
                    authoring.GeneratedTriangles.Count > 0));
        }

        private int CountSelectedVertices()
        {
            int count = 0;
            foreach (KeyValuePair<int, HashSet<int>> pair in selectedByMesh)
            {
                if (pair.Value != null)
                {
                    count += pair.Value.Count;
                }
            }

            return count;
        }

        private int CountSurfacePoints()
        {
            int count = CountSelectedVertices();
            if (authoring != null && authoring.GeneratedPoints != null)
            {
                count += authoring.GeneratedPoints.Count;
            }

            if (authoring != null && authoring.GeneratedTriangles != null)
            {
                count += authoring.GeneratedTriangles.Count * 3;
            }

            return count;
        }

        private void DrawRightPane()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawPreviewToolbar();

                Rect previewRect = GUILayoutUtility.GetRect(
                    100f,
                    10000f,
                    220f,
                    10000f,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                DrawPreviewPanel(previewRect);

                using (new EditorGUILayout.VerticalScope(GUILayout.Height(76f)))
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    for (int i = 0; i < sourceMeshes.Count; i++)
                    {
                        MeshFilter meshFilter = sourceMeshes[i];
                        int selectedCount = selectedByMesh.TryGetValue(i, out HashSet<int> selected) ? selected.Count : 0;
                        EditorGUILayout.LabelField($"{i}: {(meshFilter == null ? "Missing Mesh" : meshFilter.name)}", $"Selected: {selectedCount}");
                    }

                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawPreviewToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(GetActiveMeshName(), EditorStyles.miniLabel, GUILayout.MinWidth(120f));

                GUILayout.FlexibleSpace();

                previewInteractionMode = (PreviewInteractionMode)GUILayout.Toolbar(
                    (int)previewInteractionMode,
                    new[] { "Select Vertices", "Orbit Camera" },
                    EditorStyles.toolbarButton,
                    GUILayout.Width(176f));

                if (previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Select)
                {
                    PreviewSelectionShape nextSelectionShape = (PreviewSelectionShape)GUILayout.Toolbar(
                        (int)previewSelectionShape,
                        new[] { "Brush", "Lasso" },
                        EditorStyles.toolbarButton,
                        GUILayout.Width(112f));
                    if (nextSelectionShape != previewSelectionShape)
                    {
                        SetPreviewSelectionShape(nextSelectionShape);
                    }
                }

                bool nextViewportOrbitHandlesEnabled = GUILayout.Toggle(viewportOrbitHandlesEnabled, "Orbit Handles", EditorStyles.toolbarButton, GUILayout.Width(94f));
                if (nextViewportOrbitHandlesEnabled != viewportOrbitHandlesEnabled)
                {
                    SetViewportOrbitHandlesEnabled(nextViewportOrbitHandlesEnabled);
                }

                if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                {
                    previewZoom = 1.35f;
                    previewPanOffset = Vector3.zero;
                    Repaint();
                }

                previewProjection = (FPMeshPreviewProjection)EditorGUILayout.EnumPopup(previewProjection, EditorStyles.toolbarPopup, GUILayout.Width(92f));
                invertPreviewOrbit = GUILayout.Toggle(invertPreviewOrbit, "Invert", EditorStyles.toolbarButton, GUILayout.Width(54f));
            }
        }

        private void DrawPreviewPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect previewRect = new Rect(rect.x + 8f, rect.y + 8f, Mathf.Max(1f, rect.width - 16f), Mathf.Max(1f, rect.height - 16f));

            MeshFilter activeMesh = GetActiveMesh();
            if (activeMesh == null || activeMesh.sharedMesh == null)
            {
                GUI.Label(previewRect, "Add or select a source mesh to preview.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewMaterials();

            Bounds bounds = CalculatePreviewBounds();
            ConfigurePreviewCamera(bounds, previewRect);
            HandlePreviewInput(previewRect);

            if (Event.current.type == EventType.Repaint)
            {
                previewUtility.BeginPreview(previewRect, GUIStyle.none);
                previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
                previewUtility.camera.clearFlags = CameraClearFlags.Color;
                previewUtility.lights[0].intensity = 1.15f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                previewUtility.lights[1].intensity = 0.65f;

                DrawPreviewMeshes();

                previewUtility.camera.Render();
                Texture result = previewUtility.EndPreview();
                GUI.DrawTexture(previewRect, result, ScaleMode.StretchToFill, false);
            }

            DrawPreviewOverlays(previewRect);
            DrawPreviewInfo(previewRect, activeMesh);
            DrawPreviewSelectionOverlay(previewRect);
            if (viewportOrbitHandlesEnabled)
            {
                DrawViewportOrbitHandles(previewRect, bounds);
            }
            FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(previewRect, previewUtility.camera, previewProjection);
            if (viewportOrbitHandlesEnabled)
            {
                FPMeshPreviewEditorUtility.DrawOrbitGizmo(previewRect, SetPreviewView);
            }
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            previewUtility.cameraFieldOfView = 30f;
        }

        private void EnsurePreviewMaterials()
        {
            if (activePreviewMaterial == null)
            {
                activePreviewMaterial = CreatePreviewMaterial(new Color(0.56f, 0.78f, 1f, 1f));
            }

            if (inactivePreviewMaterial == null)
            {
                inactivePreviewMaterial = CreatePreviewMaterial(new Color(0.34f, 0.37f, 0.42f, 0.45f));
            }

            if (dimPreviewMaterial == null)
            {
                dimPreviewMaterial = CreatePreviewMaterial(new Color(0.38f, 0.43f, 0.48f, 0.18f));
            }
        }

        private static Material CreatePreviewMaterial(Color color)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            Material material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            material.SetColor("_Color", color);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_ZWrite", 1);
            return material;
        }

        private void CleanupPreviewUtility()
        {
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }

            if (activePreviewMaterial != null)
            {
                DestroyImmediate(activePreviewMaterial);
                activePreviewMaterial = null;
            }

            if (inactivePreviewMaterial != null)
            {
                DestroyImmediate(inactivePreviewMaterial);
                inactivePreviewMaterial = null;
            }

            if (dimPreviewMaterial != null)
            {
                DestroyImmediate(dimPreviewMaterial);
                dimPreviewMaterial = null;
            }
        }

        private void ConfigurePreviewCamera(Bounds bounds, Rect previewRect)
        {
            Camera camera = previewUtility.camera;
            float zoomScale = Mathf.Max(PreviewZoomMin, previewZoom);
            float distance = FPMeshPreviewEditorUtility.CalculateFitDistance(bounds, previewRect) * zoomScale;
            Vector3 forward = previewRotation * Vector3.forward;
            Vector3 focus = bounds.center + previewPanOffset;
            camera.transform.position = focus - (forward * distance);
            camera.transform.rotation = previewRotation;
            camera.fieldOfView = 30f;
            camera.orthographic = previewProjection == FPMeshPreviewProjection.Orthographic;

            if (camera.orthographic)
            {
                camera.orthographicSize = FPMeshPreviewEditorUtility.CalculateOrthographicSize(bounds, previewRect, previewRotation) * zoomScale;
            }

            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            camera.nearClipPlane = Mathf.Max(0.001f, distance - (radius * 2.5f));
            camera.farClipPlane = distance + (radius * 3.5f);
        }

        private void DrawPreviewMeshes()
        {
            for (int i = 0; i < sourceMeshes.Count; i++)
            {
                MeshFilter meshFilter = sourceMeshes[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                bool active = i == activeMeshIndex;
                if (!active && !drawPreviewAllMeshes)
                {
                    continue;
                }

                bool graphMode = mode == ToolMode.Edge || mode == ToolMode.Triangle;
                Material material = graphMode ? dimPreviewMaterial : active ? activePreviewMaterial : inactivePreviewMaterial;
                Mesh mesh = meshFilter.sharedMesh;
                int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    previewUtility.DrawMesh(mesh, meshFilter.transform.localToWorldMatrix, material, subMesh);
                }
            }
        }

        private void DrawPreviewOverlays(Rect rect)
        {
            if (previewUtility == null || previewUtility.camera == null)
            {
                return;
            }

            DrawGeneratedPlanePreview(rect);
            DrawPlanePicksPreview(rect);
            DrawGeneratedTrianglesPreview(rect);
            DrawGeneratedEdgesPreview(rect);
            DrawGeneratedPointsPreview(rect);
            DrawSelectedSurfaceEndpointPreview(rect);

            for (int i = 0; i < sourceMeshes.Count; i++)
            {
                MeshFilter meshFilter = sourceMeshes[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                bool active = i == activeMeshIndex;
                if (!active && !drawPreviewAllMeshes)
                {
                    continue;
                }

                Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;
                if (drawEdges)
                {
                    bool graphMode = mode == ToolMode.Edge || mode == ToolMode.Triangle;
                    Color edgeColor = graphMode
                        ? new Color(1f, 1f, 1f, active ? 0.08f : 0.04f)
                        : active ? FPMeshGraphPreview.EdgeColor : new Color(1f, 1f, 1f, 0.18f);
                    FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, meshFilter.sharedMesh, matrix, edgeColor, graphMode ? 0.65f : active ? 1.4f : 0.8f);
                }

                if (drawPreviewVertices)
                {
                    bool graphMode = mode == ToolMode.Edge || mode == ToolMode.Triangle;
                    Color vertexColor = graphMode
                        ? new Color(1f, 1f, 1f, active ? 0.1f : 0.05f)
                        : new Color(1f, 1f, 1f, active ? 0.35f : 0.18f);
                    FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, meshFilter.sharedMesh, matrix, vertexColor, graphMode ? 1.2f : active ? 2.2f : 1.6f);
                }
            }

            DrawSelectedPreviewVertices(previewUtility.camera, rect);
        }

        private void DrawGeneratedPlanePreview(Rect rect)
        {
            if (!GetCurrentGeneratedPlane(out FPMeshGeneratedPlane plane) || Event.current.type != EventType.Repaint)
            {
                return;
            }

            float size = GetPlaneDisplaySize();
            Vector3 right = plane.Right.sqrMagnitude > 0.0001f ? plane.Right.normalized : Vector3.right;
            Vector3 forward = plane.Forward.sqrMagnitude > 0.0001f ? plane.Forward.normalized : Vector3.forward;
            Vector3[] worldCorners =
            {
                plane.Origin - (right * size) - (forward * size),
                plane.Origin - (right * size) + (forward * size),
                plane.Origin + (right * size) + (forward * size),
                plane.Origin + (right * size) - (forward * size)
            };

            Vector3[] guiCorners = new Vector3[worldCorners.Length];
            for (int i = 0; i < worldCorners.Length; i++)
            {
                if (!ProjectPreviewPointUnclipped(previewUtility.camera, rect, worldCorners[i], out Vector2 point))
                {
                    return;
                }

                guiCorners[i] = point;
            }

            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Color previous = Handles.color;
            Handles.color = FPMeshGraphPreview.PlaneFillColor;
            Handles.DrawAAConvexPolygon(guiCorners);
            Handles.color = FPMeshGraphPreview.PlaneEdgeColor;
            for (int i = 0; i < guiCorners.Length; i++)
            {
                Handles.DrawAAPolyLine(2f, guiCorners[i], guiCorners[(i + 1) % guiCorners.Length]);
            }

            Handles.color = previous;
            GUI.EndClip();
            Handles.EndGUI();
        }

        private void DrawPlanePicksPreview(Rect rect)
        {
            if (planePicks.Count == 0 || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Color previous = Handles.color;
            Handles.color = FPMeshGraphPreview.PlaneEdgeColor;

            Vector2 previousPoint = Vector2.zero;
            bool hasPreviousPoint = false;
            for (int i = 0; i < planePicks.Count; i++)
            {
                if (!TryProjectPreviewPoint(previewUtility.camera, rect, planePicks[i], out Vector2 point))
                {
                    continue;
                }

                Handles.DrawSolidDisc(point, Vector3.forward, 4.8f);
                if (hasPreviousPoint)
                {
                    Handles.DrawAAPolyLine(2f, previousPoint, point);
                }

                previousPoint = point;
                hasPreviousPoint = true;
            }

            Handles.color = previous;
            GUI.EndClip();
            Handles.EndGUI();
        }

        private void DrawGeneratedTrianglesPreview(Rect rect)
        {
            if (authoring == null || authoring.GeneratedTriangles == null || authoring.GeneratedTriangles.Count == 0 || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Color previous = Handles.color;
            for (int i = 0; i < authoring.GeneratedTriangles.Count; i++)
            {
                FPMeshGeneratedTriangleRecord triangle = authoring.GeneratedTriangles[i];
                if (!TryProjectEdgeEndpoint(triangle.A, rect, out Vector2 a) ||
                    !TryProjectEdgeEndpoint(triangle.B, rect, out Vector2 b) ||
                    !TryProjectEdgeEndpoint(triangle.C, rect, out Vector2 c))
                {
                    continue;
                }

                Handles.color = new Color(1f, 0.16f, 0.08f, 0.22f);
                Handles.DrawAAConvexPolygon(a, b, c);
                Handles.color = new Color(1f, 0.16f, 0.08f, 0.95f);
                Handles.DrawAAPolyLine(3.5f, a, b, c, a);
            }

            Handles.color = previous;
            GUI.EndClip();
            Handles.EndGUI();
        }

        private void DrawGeneratedEdgesPreview(Rect rect)
        {
            if (authoring == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            IReadOnlyList<FPMeshGeneratedEdgeRecord> edges = authoring.GeneratedEdges;
            if ((edges == null || edges.Count == 0) && !hasPendingGeneratedEdgeStart)
            {
                return;
            }

            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Color previous = Handles.color;
            if (edges != null)
            {
                Handles.color = new Color(1f, 0.86f, 0.18f, 0.92f);
                for (int i = 0; i < edges.Count; i++)
                {
                    FPMeshGeneratedEdgeRecord edge = edges[i];
                    if (!TryProjectEdgeEndpoint(authoring.ResolveStartEndpoint(edge), rect, out Vector2 a) ||
                        !TryProjectEdgeEndpoint(authoring.ResolveEndEndpoint(edge), rect, out Vector2 b))
                    {
                        continue;
                    }

                    Handles.DrawAAPolyLine(3f, a, b);
                }
            }

            if (hasPendingGeneratedEdgeStart && TryProjectEdgeEndpoint(pendingGeneratedEdgeStart, rect, out Vector2 pendingPoint))
            {
                Handles.color = new Color(1f, 1f, 1f, 0.95f);
                Handles.DrawWireDisc(pendingPoint, Vector3.forward, 9f);
            }

            Handles.color = previous;
            GUI.EndClip();
            Handles.EndGUI();
        }

        private bool TryProjectGeneratedPoint(IReadOnlyList<FPMeshGeneratedPointRecord> points, int index, Rect rect, out Vector2 point)
        {
            point = default;
            return points != null &&
                index >= 0 &&
                index < points.Count &&
                TryProjectPreviewPoint(previewUtility.camera, rect, points[index].WorldPosition, out point);
        }

        private void DrawGeneratedPointsPreview(Rect rect)
        {
            if (authoring == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            IReadOnlyList<FPMeshGeneratedPointRecord> points = authoring.GeneratedPoints;
            if (points == null || points.Count == 0)
            {
                return;
            }

            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Color previous = Handles.color;
            Handles.color = FPMeshGraphPreview.GeneratedVertexColor;

            for (int i = 0; i < points.Count; i++)
            {
                if (TryProjectPreviewPoint(previewUtility.camera, rect, points[i].WorldPosition, out Vector2 point))
                {
                    Handles.DrawSolidDisc(point, Vector3.forward, 4.8f);
                }
            }

            Handles.color = previous;
            GUI.EndClip();
            Handles.EndGUI();
        }

        private void DrawSelectedSurfaceEndpointPreview(Rect rect)
        {
            if (authoring == null || !authoring.HasSelectedSurfacePoint || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (!TryProjectEdgeEndpoint(authoring.SelectedSurfacePoint, rect, out Vector2 point))
            {
                return;
            }

            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Color previous = Handles.color;
            Handles.color = Color.white;
            Handles.DrawWireDisc(point, Vector3.forward, 8f);
            Handles.color = new Color(1f, 0.86f, 0.18f, 1f);
            Handles.DrawWireDisc(point, Vector3.forward, 10f);
            string label = FormatSurfaceEndpointLabel(authoring.SelectedSurfacePoint);
            Handles.Label(point + new Vector2(9f, -18f), label, EditorStyles.whiteMiniLabel);
            Handles.color = previous;
            GUI.EndClip();
            Handles.EndGUI();
        }

        private void DrawPreviewInfo(Rect rect, MeshFilter activeMesh)
        {
            Rect overlay = new Rect(rect.x + 8f, rect.y + 8f, 238f, 74f);
            DrawOpaquePreviewPanel(overlay);
            Rect line = new Rect(overlay.x + 6f, overlay.y + 5f, overlay.width - 12f, 16f);
            GUI.Label(line, $"Active: {activeMesh.name}", EditorStyles.whiteMiniLabel);
            line.y += 18f;
            GUI.Label(line, $"Vertices: {activeMesh.sharedMesh.vertexCount}", EditorStyles.whiteMiniLabel);
            line.y += 18f;
            int selectedCount = selectedByMesh.TryGetValue(activeMeshIndex, out HashSet<int> selected) ? selected.Count : 0;
            GUI.Label(line, $"Selected: {selectedCount}   Zoom: {previewZoom:0.##}x", EditorStyles.whiteMiniLabel);
        }

        private static void DrawOpaquePreviewPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.07f, 0.08f, 0.09f, 0.96f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.48f, 0.55f, 0.62f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.03f, 0.035f, 0.04f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), new Color(0.48f, 0.55f, 0.62f, 1f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(0.03f, 0.035f, 0.04f, 1f));
        }

        private void DrawPreviewSelectionOverlay(Rect rect)
        {
            Event current = Event.current;
            bool drawsSelectionBrush = mode == ToolMode.Select;
            bool drawsEdgeBrush = mode == ToolMode.Edge && edgeInputMode == EdgeInputMode.Paint;
            if (current == null || current.type != EventType.Repaint || previewInteractionMode != PreviewInteractionMode.SelectVertices || (!drawsSelectionBrush && !drawsEdgeBrush))
            {
                return;
            }

            Handles.BeginGUI();
            Color previous = Handles.color;

            if ((drawsEdgeBrush || previewSelectionShape == PreviewSelectionShape.Brush) && rect.Contains(current.mousePosition))
            {
                bool removes = drawsEdgeBrush ? edgePaintRemoves : previewSelectionRemoves;
                Color activeColor = removes
                    ? new Color(1f, 0.28f, 0.18f, 1f)
                    : drawsEdgeBrush ? new Color(1f, 0.86f, 0.18f, 1f) : FPMeshGraphPreview.ActiveVertexColor;
                Handles.color = new Color(1f, 1f, 1f, 0.78f);
                Vector3 center = new Vector3(current.mousePosition.x, current.mousePosition.y, 0f);
                Handles.DrawWireDisc(center, Vector3.forward, previewSelectionRadius);
                Handles.color = activeColor;
                Handles.DrawWireDisc(center, Vector3.forward, Mathf.Max(1f, previewSelectionRadius - 1.5f));
            }

            if (drawsSelectionBrush && previewSelectionShape == PreviewSelectionShape.Lasso && previewLassoPoints.Count > 0)
            {
                Handles.color = previewLassoRemoves ? new Color(1f, 0.28f, 0.18f, 0.95f) : new Color(1f, 1f, 1f, 0.9f);
                DrawLassoPolyline(previewLassoPoints, rect.position, isDrawingPreviewLasso);
            }

            Handles.color = previous;
            Handles.EndGUI();
        }

        private void DrawViewportOrbitHandles(Rect rect, Bounds bounds)
        {
            if (previewUtility == null || previewUtility.camera == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Vector3 center = bounds.center;
            float radius = GetViewportOrbitWorldRadius(rect, center);
            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Rect clipRect = new Rect(0f, 0f, rect.width, rect.height);
            DrawViewportOrbitRing(rect, clipRect, center, Vector3.right, radius * 0.92f, new Color(0.95f, 0.22f, 0.18f, 0.82f), 0);
            DrawViewportOrbitRing(rect, clipRect, center, Vector3.up, radius, new Color(0.28f, 0.9f, 0.38f, 0.82f), 1);
            DrawViewportOrbitRing(rect, clipRect, center, Vector3.forward, radius * 1.08f, new Color(0.25f, 0.5f, 1f, 0.82f), 2);
            GUI.EndClip();
            Handles.EndGUI();
        }

        private void DrawViewportOrbitRing(Rect rect, Rect clipRect, Vector3 center, Vector3 normal, float radius, Color color, int axis)
        {
            Vector3[] points = BuildCirclePoints(center, normal, radius, 72);
            bool active = activeOrbitAxis == axis;
            bool hover = hoverViewportOrbitAxis == axis;
            if (hover && !active)
            {
                DrawProjectedPolyline(rect, clipRect, points, new Color(1f, 1f, 1f, 0.38f), 9f);
            }

            DrawProjectedPolyline(rect, clipRect, points, active ? Color.white : color, active || hover ? 5.5f : 3.25f);
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.Ignore) && activeOrbitAxis >= 0)
            {
                activeOrbitAxis = -1;
                current.Use();
                return;
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.Ignore) && isDrawingPreviewLasso)
            {
                bool changed = CompletePreviewLassoSelection(rect);
                isDrawingPreviewLasso = false;
                previewLassoRemoves = false;
                previewLassoPoints.Clear();
                current.Use();
                Repaint();
                if (changed)
                {
                    WriteToAuthoring();
                    SceneView.RepaintAll();
                }

                return;
            }

            if (current.type == EventType.MouseUp || current.type == EventType.Ignore)
            {
                isPaintingPreviewSelection = false;
                previewSelectionRemoves = false;
                isPaintingPreviewEdges = false;
                edgePaintRemoves = false;
                isPaintingPreviewTriangles = false;
                trianglePaintRemoves = false;
                if (mode == ToolMode.Edge && edgeInputMode == EdgeInputMode.Paint)
                {
                    ClearPendingGeneratedEdgeStart();
                }
            }

            if (!rect.Contains(current.mousePosition))
            {
                if (current.rawType == EventType.MouseUp)
                {
                    isPaintingPreviewSelection = false;
                    previewSelectionRemoves = false;
                    isPaintingPreviewEdges = false;
                    edgePaintRemoves = false;
                    isPaintingPreviewTriangles = false;
                    trianglePaintRemoves = false;
                    if (mode == ToolMode.Edge && edgeInputMode == EdgeInputMode.Paint)
                    {
                        ClearPendingGeneratedEdgeStart();
                    }
                }

                if (hoverViewportOrbitAxis >= 0)
                {
                    hoverViewportOrbitAxis = -1;
                    Repaint();
                }

                if (isDrawingPreviewLasso && current.type == EventType.MouseDrag)
                {
                    AddPreviewLassoPoint(rect, ClampToRect(current.mousePosition, rect));
                    current.Use();
                    Repaint();
                }

                return;
            }

            if (current.type == EventType.ScrollWheel)
            {
                float zoomFactor = Mathf.Exp(current.delta.y * 0.08f);
                previewZoom = Mathf.Clamp(previewZoom * zoomFactor, PreviewZoomMin, PreviewZoomMax);
                current.Use();
                Repaint();
                return;
            }

            if (current.button == 2 && current.type == EventType.MouseDown)
            {
                current.Use();
                return;
            }

            if (current.button == 2 && current.type == EventType.MouseDrag)
            {
                ApplyPreviewPan(rect, current.delta);
                current.Use();
                Repaint();
                return;
            }

            if (current.button == 2 && current.type == EventType.MouseUp)
            {
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && IsSelectionMouseButton(current.button) && isDrawingPreviewLasso && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Select)
            {
                AddPreviewLassoPoint(rect, current.mousePosition);
                current.Use();
                Repaint();
                return;
            }

            if (current.type == EventType.MouseDrag && IsSelectionMouseButton(current.button) && isPaintingPreviewSelection && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Select)
            {
                if (TryPaintPreviewVertices(rect, current.mousePosition, previewSelectionRadius, previewSelectionRemoves))
                {
                    WriteToAuthoring();
                    Repaint();
                    SceneView.RepaintAll();
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && IsSelectionMouseButton(current.button) && isPaintingPreviewEdges && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Edge && edgeInputMode == EdgeInputMode.Paint)
            {
                bool changed = edgePaintRemoves
                    ? TryRemoveNearestGeneratedEdgePreview(rect, current.mousePosition)
                    : TryPaintGeneratedEdgePreview(rect, current.mousePosition);
                if (changed)
                {
                    Repaint();
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && IsSelectionMouseButton(current.button) && isPaintingPreviewTriangles && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Triangle)
            {
                bool changed = trianglePaintRemoves
                    ? TryRemoveGeneratedTrianglePreview(rect, current.mousePosition)
                    : TryAddGeneratedTrianglePreview(rect, current.mousePosition);
                if (changed)
                {
                    Repaint();
                }

                current.Use();
                return;
            }

            bool mouseOverPanelOrbit = viewportOrbitHandlesEnabled && FPMeshPreviewEditorUtility.IsOrbitGizmoPosition(rect, current.mousePosition);
            int currentViewportOrbitHover = viewportOrbitHandlesEnabled && !mouseOverPanelOrbit
                ? GetViewportOrbitAxisAtPosition(rect, current.mousePosition, hoverViewportOrbitAxis)
                : -1;
            if (activeOrbitAxis < 0 && hoverViewportOrbitAxis != currentViewportOrbitHover)
            {
                hoverViewportOrbitAxis = currentViewportOrbitHover;
                Repaint();
            }

            bool leftMouseToolStart = current.type == EventType.MouseDown &&
                current.button == 0 &&
                previewInteractionMode == PreviewInteractionMode.SelectVertices &&
                (mode == ToolMode.Select || mode == ToolMode.Point || mode == ToolMode.Edge || mode == ToolMode.Triangle);

            if (current.type == EventType.MouseDown && current.button == 0 && !leftMouseToolStart)
            {
                int axis = viewportOrbitHandlesEnabled ? FPMeshPreviewEditorUtility.GetOrbitAxisAtPosition(rect, current.mousePosition) : -1;
                if (axis >= 0)
                {
                    activeOrbitAxis = axis;
                    isPaintingPreviewSelection = false;
                    isDrawingPreviewLasso = false;
                    previewLassoPoints.Clear();
                    current.Use();
                    return;
                }
            }

            if (current.type == EventType.MouseDown && current.button == 0 && currentViewportOrbitHover >= 0 && !leftMouseToolStart)
            {
                activeOrbitAxis = currentViewportOrbitHover;
                hoverViewportOrbitAxis = currentViewportOrbitHover;
                isPaintingPreviewSelection = false;
                isDrawingPreviewLasso = false;
                previewLassoPoints.Clear();
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && current.button == 0 && activeOrbitAxis >= 0)
            {
                previewRotation = FPMeshPreviewEditorUtility.ApplyOrbitAxisDrag(previewRotation, activeOrbitAxis, current.delta);
                current.Use();
                Repaint();
                return;
            }

            bool rightMouseSelectionStart = current.type == EventType.MouseDown &&
                current.button == 1 &&
                previewInteractionMode == PreviewInteractionMode.SelectVertices &&
                (mode == ToolMode.Select || mode == ToolMode.Point || mode == ToolMode.Edge || mode == ToolMode.Triangle);
            if ((mouseOverPanelOrbit || currentViewportOrbitHover >= 0) && !rightMouseSelectionStart)
            {
                return;
            }

            bool canOrbitFromCanvas = previewInteractionMode == PreviewInteractionMode.OrbitCamera || current.alt;
            if (current.type == EventType.MouseDrag && current.button == 0 && canOrbitFromCanvas)
            {
                previewRotation = FPMeshPreviewEditorUtility.ApplyUnityStyleOrbit(previewRotation, current.delta, invertPreviewOrbit);
                current.Use();
                Repaint();
                return;
            }

            if (current.type == EventType.MouseDown && IsSelectionMouseButton(current.button) && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Point)
            {
                bool changed = current.button == 1
                    ? TryRemoveNearestGeneratedPreviewPoint(rect, current.mousePosition)
                    : TryAddGeneratedPreviewPoint(rect, current.mousePosition);
                if (changed)
                {
                    Repaint();
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && IsSelectionMouseButton(current.button) && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Edge)
            {
                if (edgeInputMode == EdgeInputMode.Paint)
                {
                    isPaintingPreviewEdges = true;
                    edgePaintRemoves = current.button == 1;
                    hoverViewportOrbitAxis = -1;
                    bool edgePaintChanged = edgePaintRemoves
                        ? TryRemoveNearestGeneratedEdgePreview(rect, current.mousePosition)
                        : TryPaintGeneratedEdgePreview(rect, current.mousePosition);
                    if (edgePaintChanged)
                    {
                        Repaint();
                    }

                    current.Use();
                    return;
                }

                bool changed = current.button == 1
                    ? TryRemoveNearestGeneratedEdgePreview(rect, current.mousePosition)
                    : TryAddGeneratedEdgePreview(rect, current.mousePosition);
                if (changed)
                {
                    Repaint();
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && IsSelectionMouseButton(current.button) && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Triangle)
            {
                isPaintingPreviewTriangles = true;
                trianglePaintRemoves = current.button == 1;
                hoverViewportOrbitAxis = -1;
                bool changed = current.button == 1
                    ? TryRemoveGeneratedTrianglePreview(rect, current.mousePosition)
                    : TryAddGeneratedTrianglePreview(rect, current.mousePosition);
                if (changed)
                {
                    Repaint();
                }

                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && IsSelectionMouseButton(current.button) && previewInteractionMode == PreviewInteractionMode.SelectVertices && mode == ToolMode.Select)
            {
                bool removeSelection = current.button == 1;
                if (previewSelectionShape == PreviewSelectionShape.Lasso)
                {
                    BeginPreviewLasso(rect, current.mousePosition, removeSelection);
                    current.Use();
                    Repaint();
                    return;
                }

                isPaintingPreviewSelection = true;
                previewSelectionRemoves = removeSelection;
                hoverViewportOrbitAxis = -1;
                if (TrySelectNearestPreviewVertex(rect, current.mousePosition, removeSelection))
                {
                    WriteToAuthoring();
                    Repaint();
                    SceneView.RepaintAll();
                }

                current.Use();
                return;
            }
        }

        private bool TrySelectNearestPreviewVertex(Rect previewRect, Vector2 mousePosition, bool removeSelection)
        {
            MeshFilter meshFilter = GetActiveMesh();
            if (meshFilter == null || meshFilter.sharedMesh == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            Vector2 localMousePosition = mousePosition - previewRect.position;
            int nearest = FindNearestPreviewVertex(meshFilter, previewUtility.camera, previewRect, localMousePosition, previewSelectionRadius);
            if (nearest < 0)
            {
                return false;
            }

            if (!selectedByMesh.TryGetValue(activeMeshIndex, out HashSet<int> selected))
            {
                selected = new HashSet<int>();
                selectedByMesh[activeMeshIndex] = selected;
            }

            FPMeshSurfaceEdgeEndpoint endpoint = FPMeshSurfaceEdgeEndpoint.Source(activeMeshIndex, nearest);
            bool changed = removeSelection ? selected.Remove(nearest) : selected.Add(nearest);
            if (changed && !removeSelection)
            {
                SelectSurfaceEndpoint(endpoint, "Select Source Mesh Vertex");
            }
            else if (changed)
            {
                tagsByEndpoint.Remove(endpoint);
            }

            return changed;
        }

        private bool TryPaintPreviewVertices(Rect previewRect, Vector2 mousePosition, float radius, bool removeSelection)
        {
            MeshFilter meshFilter = GetActiveMesh();
            if (meshFilter == null || meshFilter.sharedMesh == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            if (!selectedByMesh.TryGetValue(activeMeshIndex, out HashSet<int> selected))
            {
                selected = new HashSet<int>();
                selectedByMesh[activeMeshIndex] = selected;
            }

            Vector2 localMousePosition = mousePosition - previewRect.position;
            List<int> changedVertices = new();
            bool changed = AddPreviewVerticesInRadius(meshFilter, previewUtility.camera, previewRect, localMousePosition, radius, selected, removeSelection, changedVertices, out int lastChangedVertex);
            for (int i = 0; i < changedVertices.Count; i++)
            {
                FPMeshSurfaceEdgeEndpoint endpoint = FPMeshSurfaceEdgeEndpoint.Source(activeMeshIndex, changedVertices[i]);
                if (removeSelection)
                {
                    tagsByEndpoint.Remove(endpoint);
                }
                else
                {
                    AssignWorkingTagsIfMissing(endpoint);
                }
            }

            if (changed && !removeSelection && lastChangedVertex >= 0)
            {
                SelectSurfaceEndpoint(FPMeshSurfaceEdgeEndpoint.Source(activeMeshIndex, lastChangedVertex), "Select Source Mesh Vertex");
            }

            return changed;
        }

        private void BeginPreviewLasso(Rect previewRect, Vector2 mousePosition, bool removeSelection)
        {
            isPaintingPreviewSelection = false;
            isDrawingPreviewLasso = true;
            previewLassoRemoves = removeSelection;
            hoverViewportOrbitAxis = -1;
            previewLassoPoints.Clear();
            AddPreviewLassoPoint(previewRect, mousePosition);
        }

        private void AddPreviewLassoPoint(Rect previewRect, Vector2 mousePosition)
        {
            Vector2 localPoint = ClampToRect(mousePosition, previewRect) - previewRect.position;
            if (previewLassoPoints.Count > 0 && Vector2.Distance(previewLassoPoints[previewLassoPoints.Count - 1], localPoint) < PreviewLassoMinPointDistance)
            {
                return;
            }

            previewLassoPoints.Add(localPoint);
        }

        private bool CompletePreviewLassoSelection(Rect previewRect)
        {
            if (previewLassoPoints.Count < 3 || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < sourceMeshes.Count; i++)
            {
                MeshFilter meshFilter = sourceMeshes[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                bool active = i == activeMeshIndex;
                if (!active && !drawPreviewAllMeshes)
                {
                    continue;
                }

                if (!selectedByMesh.TryGetValue(i, out HashSet<int> selected))
                {
                    selected = new HashSet<int>();
                    selectedByMesh[i] = selected;
                }

                List<int> changedVertices = new();
                bool meshChanged = AddPreviewVerticesInLasso(meshFilter, previewUtility.camera, previewRect, previewLassoPoints, selected, previewLassoRemoves, changedVertices);
                for (int vertex = 0; vertex < changedVertices.Count; vertex++)
                {
                    FPMeshSurfaceEdgeEndpoint endpoint = FPMeshSurfaceEdgeEndpoint.Source(i, changedVertices[vertex]);
                    if (previewLassoRemoves)
                    {
                        tagsByEndpoint.Remove(endpoint);
                    }
                    else
                    {
                        AssignWorkingTagsIfMissing(endpoint);
                    }
                }

                changed |= meshChanged;
            }

            return changed;
        }

        private static int FindNearestPreviewVertex(MeshFilter meshFilter, Camera camera, Rect previewRect, Vector2 mousePosition, float maxDistance)
        {
            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            int nearest = -1;
            float nearestDistance = maxDistance;
            Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (!TryProjectPreviewPoint(camera, previewRect, matrix.MultiplyPoint3x4(vertices[i]), out Vector2 point))
                {
                    continue;
                }

                float distance = Vector2.Distance(mousePosition, point);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static bool AddPreviewVerticesInRadius(
            MeshFilter meshFilter,
            Camera camera,
            Rect previewRect,
            Vector2 mousePosition,
            float radius,
            ISet<int> selected,
            bool removeSelection,
            ICollection<int> changedVertices,
            out int lastChangedVertex)
        {
            lastChangedVertex = -1;
            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;
            bool changed = false;
            float radiusSqr = radius * radius;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (!TryProjectPreviewPoint(camera, previewRect, matrix.MultiplyPoint3x4(vertices[i]), out Vector2 point))
                {
                    continue;
                }

                if ((mousePosition - point).sqrMagnitude <= radiusSqr)
                {
                    bool vertexChanged = removeSelection ? selected.Remove(i) : selected.Add(i);
                    if (vertexChanged)
                    {
                        lastChangedVertex = i;
                        changedVertices?.Add(i);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool AddPreviewVerticesInLasso(
            MeshFilter meshFilter,
            Camera camera,
            Rect previewRect,
            IReadOnlyList<Vector2> lassoPoints,
            ISet<int> selected,
            bool removeSelection,
            ICollection<int> changedVertices)
        {
            if (lassoPoints == null || lassoPoints.Count < 3)
            {
                return false;
            }

            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;
            bool changed = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (!TryProjectPreviewPoint(camera, previewRect, matrix.MultiplyPoint3x4(vertices[i]), out Vector2 point))
                {
                    continue;
                }

                if (IsPointInPolygon(point, lassoPoints))
                {
                    bool vertexChanged = removeSelection ? selected.Remove(i) : selected.Add(i);
                    if (vertexChanged)
                    {
                        changedVertices?.Add(i);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static bool IsSelectionMouseButton(int button)
        {
            return button == 0 || button == 1;
        }

        private static bool IsPointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            int previous = polygon.Count - 1;
            for (int current = 0; current < polygon.Count; current++)
            {
                Vector2 a = polygon[current];
                Vector2 b = polygon[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses)
                {
                    float denominator = b.y - a.y;
                    if (Mathf.Abs(denominator) <= 0.000001f)
                    {
                        previous = current;
                        continue;
                    }

                    float x = ((b.x - a.x) * (point.y - a.y) / denominator) + a.x;
                    if (point.x < x)
                    {
                        inside = !inside;
                    }
                }

                previous = current;
            }

            return inside;
        }

        private static Vector2 ClampToRect(Vector2 point, Rect rect)
        {
            return new Vector2(
                Mathf.Clamp(point.x, rect.xMin, rect.xMax),
                Mathf.Clamp(point.y, rect.yMin, rect.yMax));
        }

        private static void DrawLassoPolyline(IReadOnlyList<Vector2> points, Vector2 offset, bool closed)
        {
            if (points == null || points.Count == 0)
            {
                return;
            }

            for (int i = 0; i < points.Count - 1; i++)
            {
                Handles.DrawAAPolyLine(2.5f, points[i] + offset, points[i + 1] + offset);
            }

            if (closed && points.Count > 2)
            {
                Handles.DrawAAPolyLine(1.5f, points[points.Count - 1] + offset, points[0] + offset);
            }

            Handles.DrawSolidDisc(points[0] + offset, Vector3.forward, 3.5f);
            Handles.DrawSolidDisc(points[points.Count - 1] + offset, Vector3.forward, 3.5f);
        }

        private Bounds CalculatePreviewBounds()
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

            for (int i = 0; i < sourceMeshes.Count; i++)
            {
                MeshFilter meshFilter = sourceMeshes[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                bool active = i == activeMeshIndex;
                if (!active && !drawPreviewAllMeshes)
                {
                    continue;
                }

                Bounds meshBounds = TransformBounds(meshFilter.sharedMesh.bounds, meshFilter.transform.localToWorldMatrix);
                if (!hasBounds)
                {
                    bounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(meshBounds);
                }
            }

            if (!hasBounds)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            if (bounds.size.sqrMagnitude <= 0.000001f)
            {
                bounds.Expand(0.25f);
            }

            return bounds;
        }

        private float GetPlaneDisplaySize()
        {
            Bounds bounds = CalculatePreviewBounds();
            float size = Mathf.Max(0.25f, bounds.extents.magnitude * 0.55f);
            if (authoring != null && authoring.GeneratedPoints != null)
            {
                for (int i = 0; i < authoring.GeneratedPoints.Count; i++)
                {
                    size = Mathf.Max(size, Vector3.Distance(bounds.center, authoring.GeneratedPoints[i].WorldPosition));
                }
            }

            return size;
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            return new Bounds(center, extents * 2f);
        }

        private void DrawSelectedPreviewVertices(Camera camera, Rect previewRect)
        {
            if (camera == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            List<PreviewOverlayPoint> points = new();
            for (int meshIndex = 0; meshIndex < sourceMeshes.Count; meshIndex++)
            {
                MeshFilter meshFilter = sourceMeshes[meshIndex];
                if (meshFilter == null || meshFilter.sharedMesh == null || !selectedByMesh.TryGetValue(meshIndex, out HashSet<int> selectedVertices) || selectedVertices.Count == 0)
                {
                    continue;
                }

                bool active = meshIndex == activeMeshIndex;
                if (!active && !drawPreviewAllMeshes)
                {
                    continue;
                }

                Color selectedColor = active ? FPMeshGraphPreview.ActiveVertexColor : FPMeshGraphPreview.StoredVertexColor;
                Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;
                Vector3[] vertices = meshFilter.sharedMesh.vertices;
                foreach (int vertexIndex in selectedVertices)
                {
                    if (vertexIndex < 0 || vertexIndex >= vertices.Length)
                    {
                        continue;
                    }

                    Vector3 world = matrix.MultiplyPoint3x4(vertices[vertexIndex]);
                    Vector3 viewport = camera.WorldToViewportPoint(world);
                    if (viewport.z <= 0.001f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                    {
                        continue;
                    }

                    Vector2 point = new Vector2(viewport.x * previewRect.width, (1f - viewport.y) * previewRect.height);
                    points.Add(new PreviewOverlayPoint(point, viewport.z, selectedColor));
                }
            }

            if (points.Count == 0)
            {
                return;
            }

            points.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            Handles.BeginGUI();
            GUI.BeginClip(previewRect);
            Color previous = Handles.color;

            for (int i = 0; i < points.Count; i++)
            {
                PreviewOverlayPoint point = points[i];
                Handles.color = point.Color;
                Handles.DrawSolidDisc(point.Point, Vector3.forward, 4.6f);
                Handles.color = Color.black;
                Handles.DrawWireDisc(point.Point, Vector3.forward, 5.2f);
            }

            Handles.color = previous;
            GUI.EndClip();
            Handles.EndGUI();
        }

        private static bool TryProjectPreviewPoint(Camera camera, Rect previewRect, Vector3 world, out Vector2 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0.001f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            {
                point = Vector2.zero;
                return false;
            }

            point = new Vector2(viewport.x * previewRect.width, (1f - viewport.y) * previewRect.height);
            return true;
        }

        private static bool ProjectPreviewPointUnclipped(Camera camera, Rect previewRect, Vector3 world, out Vector2 point)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z <= 0.001f)
            {
                point = Vector2.zero;
                return false;
            }

            point = new Vector2(viewport.x * previewRect.width, (1f - viewport.y) * previewRect.height);
            return true;
        }

        private int GetViewportOrbitAxisAtPosition(Rect rect, Vector2 mousePosition, int preferredAxis)
        {
            if (preferredAxis >= 0 && IsViewportOrbitAxisAtPosition(rect, mousePosition, preferredAxis))
            {
                return preferredAxis;
            }

            for (int axis = 0; axis < 3; axis++)
            {
                if (IsViewportOrbitAxisAtPosition(rect, mousePosition, axis))
                {
                    return axis;
                }
            }

            return -1;
        }

        private bool IsViewportOrbitAxisAtPosition(Rect rect, Vector2 mousePosition, int axis)
        {
            if (previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            Bounds bounds = CalculatePreviewBounds();
            Vector3 center = bounds.center;
            float radius = GetViewportOrbitWorldRadius(rect, center);
            Vector3 normal = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            float scale = axis == 0 ? 0.92f : axis == 1 ? 1f : 1.08f;
            return DistanceToProjectedRing(rect, mousePosition, center, normal, radius * scale) <= ViewportOrbitPickRadius;
        }

        private float DistanceToProjectedRing(Rect rect, Vector2 mousePosition, Vector3 center, Vector3 normal, float radius)
        {
            Vector3[] points = BuildCirclePoints(center, normal, radius, 72);
            float best = float.PositiveInfinity;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = WorldToPreviewGuiPoint(rect, points[i]);
                Vector2 b = WorldToPreviewGuiPoint(rect, points[i + 1]);
                if (!IsFinite(a) || !IsFinite(b))
                {
                    continue;
                }

                best = Mathf.Min(best, DistancePointToSegment(mousePosition, a, b));
            }

            return best;
        }

        private float GetViewportOrbitWorldRadius(Rect rect, Vector3 center)
        {
            return Mathf.Max(0.05f, GetWorldUnitsPerPixel(rect, center) * ViewportOrbitScreenRadius);
        }

        private float GetWorldUnitsPerPixel(Rect rect, Vector3 center)
        {
            Camera camera = previewUtility.camera;
            if (camera.orthographic)
            {
                return (camera.orthographicSize * 2f) / Mathf.Max(1f, rect.height);
            }

            float distance = Mathf.Abs(Vector3.Dot(center - camera.transform.position, camera.transform.forward));
            if (distance <= 0.0001f)
            {
                distance = Vector3.Distance(center, camera.transform.position);
            }

            float worldHeight = 2f * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            return worldHeight / Mathf.Max(1f, rect.height);
        }

        private void ApplyPreviewPan(Rect rect, Vector2 delta)
        {
            if (previewUtility == null || previewUtility.camera == null)
            {
                return;
            }

            Bounds bounds = CalculatePreviewBounds();
            Camera camera = previewUtility.camera;
            float unitsPerPixel = GetWorldUnitsPerPixel(rect, bounds.center + previewPanOffset);
            previewPanOffset += ((-camera.transform.right * delta.x) + (camera.transform.up * delta.y)) * unitsPerPixel;
        }

        private Vector2 WorldToPreviewGuiPoint(Rect rect, Vector3 world)
        {
            Vector3 viewport = previewUtility.camera.WorldToViewportPoint(world);
            if (viewport.z <= 0.001f)
            {
                return new Vector2(float.NaN, float.NaN);
            }

            return new Vector2(rect.x + (viewport.x * rect.width), rect.y + ((1f - viewport.y) * rect.height));
        }

        private void DrawProjectedPolyline(Rect rect, Rect clipRect, IReadOnlyList<Vector3> worldPoints, Color color, float thickness)
        {
            if (worldPoints == null || worldPoints.Count < 2)
            {
                return;
            }

            Handles.color = color;
            for (int i = 0; i < worldPoints.Count - 1; i++)
            {
                Vector2 a = WorldToPreviewGuiPoint(rect, worldPoints[i]);
                Vector2 b = WorldToPreviewGuiPoint(rect, worldPoints[i + 1]);
                if (!IsFinite(a) || !IsFinite(b))
                {
                    continue;
                }

                a -= rect.position;
                b -= rect.position;
                if (!ClipLineToRect(ref a, ref b, clipRect))
                {
                    continue;
                }

                Handles.DrawAAPolyLine(thickness, new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
            }
        }

        private static Vector3[] BuildCirclePoints(Vector3 center, Vector3 normal, float radius, int segments)
        {
            Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(safeNormal, Vector3.up);
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                tangent = Vector3.Cross(safeNormal, Vector3.right);
            }

            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(safeNormal, tangent).normalized;
            Vector3[] points = new Vector3[Mathf.Max(8, segments) + 1];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (i / (float)(points.Length - 1)) * Mathf.PI * 2f;
                points[i] = center + (tangent * Mathf.Cos(angle) * radius) + (bitangent * Mathf.Sin(angle) * radius);
            }

            return points;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denominator = Vector2.Dot(ab, ab);
            if (denominator <= 0.00001f)
            {
                return Vector2.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / denominator);
            return Vector2.Distance(point, a + (ab * t));
        }

        private static bool ClipLineToRect(ref Vector2 a, ref Vector2 b, Rect rect)
        {
            float t0 = 0f;
            float t1 = 1f;
            Vector2 delta = b - a;

            if (!ClipTest(-delta.x, a.x - rect.xMin, ref t0, ref t1) ||
                !ClipTest(delta.x, rect.xMax - a.x, ref t0, ref t1) ||
                !ClipTest(-delta.y, a.y - rect.yMin, ref t0, ref t1) ||
                !ClipTest(delta.y, rect.yMax - a.y, ref t0, ref t1))
            {
                return false;
            }

            Vector2 originalA = a;
            if (t1 < 1f)
            {
                b = originalA + (delta * t1);
            }

            if (t0 > 0f)
            {
                a = originalA + (delta * t0);
            }

            return true;
        }

        private static bool ClipTest(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Approximately(p, 0f))
            {
                return q >= 0f;
            }

            float r = q / p;
            if (p < 0f)
            {
                if (r > t1)
                {
                    return false;
                }

                if (r > t0)
                {
                    t0 = r;
                }
            }
            else
            {
                if (r < t0)
                {
                    return false;
                }

                if (r < t1)
                {
                    t1 = r;
                }
            }

            return true;
        }

        private static bool IsFinite(Vector2 point)
        {
            return !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.x) && !float.IsInfinity(point.y);
        }

        private string GetActiveMeshName()
        {
            MeshFilter activeMesh = GetActiveMesh();
            return activeMesh == null ? "No Active Mesh" : activeMesh.name;
        }

        private void SetPreviewView(Vector3 viewDirection, Vector3 up)
        {
            previewRotation = Quaternion.LookRotation(viewDirection, up);
            Repaint();
        }

        private void DrawMeshList()
        {
            EditorGUILayout.LabelField("Source Meshes", EditorStyles.boldLabel);

            bool sourceListChanged = false;
            for (int i = 0; i < sourceMeshes.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool active = i == activeMeshIndex;
                    if (GUILayout.Toggle(active, "A", "Button", GUILayout.Width(28)) && !active)
                    {
                        activeMeshIndex = i;
                        sourceListChanged = true;
                        SceneView.RepaintAll();
                    }

                    EditorGUI.BeginChangeCheck();
                    MeshFilter nextMesh = (MeshFilter)EditorGUILayout.ObjectField(sourceMeshes[i], typeof(MeshFilter), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        sourceMeshes[i] = nextMesh;
                        sourceListChanged = true;
                    }

                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        sourceMeshes.RemoveAt(i);
                        selectedByMesh.Remove(i);
                        activeMeshIndex = Mathf.Clamp(activeMeshIndex, 0, Mathf.Max(0, sourceMeshes.Count - 1));
                        sourceListChanged = true;
                        i--;
                    }
                }
            }

            if (GUILayout.Button("Add Mesh Slot"))
            {
                sourceMeshes.Add(null);
                sourceListChanged = true;
            }

            if (sourceListChanged)
            {
                SyncSourceMeshesToAuthoring("Update Mesh Vertex Painter Sources");
            }
        }

        private void DuringSceneGui(SceneView sceneView)
        {
        }

        private void DrawScenePreview()
        {
            for (int i = 0; i < sourceMeshes.Count; i++)
            {
                MeshFilter meshFilter = sourceMeshes[i];
                selectedByMesh.TryGetValue(i, out HashSet<int> selected);
                Color color = i == activeMeshIndex ? FPMeshGraphPreview.ActiveVertexColor : FPMeshGraphPreview.StoredVertexColor;
                FPMeshGraphPreview.DrawMeshPreview(meshFilter, selected, color, drawEdges && i == activeMeshIndex, vertexSize);
            }

            if (authoring != null)
            {
                FPMeshGraphPreview.DrawPaintedRecords(authoring.PaintedVertices, vertexSize * 1.2f);
                FPMeshGraphPreview.DrawGeneratedPoints(authoring.GeneratedPoints, vertexSize * 1.4f);
            }

            if (GetCurrentGeneratedPlane(out FPMeshGeneratedPlane plane))
            {
                FPMeshGraphPreview.DrawGeneratedPlane(plane, GetPlaneDisplaySize());
            }

            FPMeshGraphPreview.DrawPlanePicks(planePicks, vertexSize * 1.4f);
        }

        private void HandleSceneClick(Vector2 mousePosition)
        {
            if (mode == ToolMode.Select)
            {
                TrySelectNearestVertex(mousePosition);
                return;
            }

            if (mode == ToolMode.Plane)
            {
                TryAddPlanePick(mousePosition);
                return;
            }

            if (mode == ToolMode.Point)
            {
                TryAddGeneratedPoint(mousePosition);
            }
        }

        private void TrySelectNearestVertex(Vector2 mousePosition)
        {
            MeshFilter meshFilter = GetActiveMesh();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            int nearest = FindNearestVertex(meshFilter, mousePosition, 18f);
            if (nearest < 0)
            {
                return;
            }

            if (!selectedByMesh.TryGetValue(activeMeshIndex, out HashSet<int> selected))
            {
                selected = new HashSet<int>();
                selectedByMesh[activeMeshIndex] = selected;
            }

            if (!selected.Add(nearest))
            {
                selected.Remove(nearest);
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void BuildPlaneFromSelection()
        {
            List<Vector3> selectedPoints = new();
            CollectSelectedWorldPoints(selectedPoints);
            if (!TryBuildPlaneFromPoints(selectedPoints, out FPMeshGeneratedPlane plane))
            {
                return;
            }

            planePicks.Clear();
            SetGeneratedPlane(plane, "Build Mesh Plane From Selection");
            Repaint();
            SceneView.RepaintAll();
        }

        private void SetGeneratedPlane(FPMeshGeneratedPlane plane, string undoLabel)
        {
            generatedPlane = plane;
            if (authoring == null)
            {
                return;
            }

            Undo.RecordObject(authoring, undoLabel);
            authoring.SetGeneratedPlane(plane);
            EditorUtility.SetDirty(authoring);
        }

        private void ClearGeneratedPlane()
        {
            generatedPlane = default;
            planePicks.Clear();
            if (authoring != null)
            {
                Undo.RecordObject(authoring, "Clear Generated Mesh Plane");
                authoring.SetGeneratedPlane(default);
                EditorUtility.SetDirty(authoring);
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private bool GetCurrentGeneratedPlane(out FPMeshGeneratedPlane plane)
        {
            if (authoring != null && authoring.GeneratedPlane.IsValid)
            {
                plane = authoring.GeneratedPlane;
                return true;
            }

            plane = generatedPlane;
            return plane.IsValid;
        }

        private bool TryBuildPlaneFromPoints(IReadOnlyList<Vector3> points, out FPMeshGeneratedPlane plane)
        {
            plane = default;
            if (points == null || points.Count < 3)
            {
                return false;
            }

            for (int a = 0; a < points.Count - 2; a++)
            {
                for (int b = a + 1; b < points.Count - 1; b++)
                {
                    for (int c = b + 1; c < points.Count; c++)
                    {
                        if (TryBuildPlane(points[a], points[b], points[c], out plane))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryBuildPlane(Vector3 a, Vector3 b, Vector3 c, out FPMeshGeneratedPlane plane)
        {
            plane = default;
            Vector3 right = b - a;
            Vector3 normal = Vector3.Cross(right, c - a);
            if (right.sqrMagnitude <= 0.000001f || normal.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            right.Normalize();
            normal.Normalize();
            Vector3 forward = Vector3.Cross(normal, right).normalized;
            plane = new FPMeshGeneratedPlane
            {
                IsValid = true,
                Origin = a,
                Right = right,
                Forward = forward,
                Normal = normal
            };

            return true;
        }

        private void TryAddPlanePick(Vector2 mousePosition)
        {
            if (!TryHitActiveMesh(mousePosition, out RaycastHit hit))
            {
                return;
            }

            planePicks.Add(hit.point);
            if (planePicks.Count < 3)
            {
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (TryBuildPlaneFromPoints(planePicks, out FPMeshGeneratedPlane plane))
            {
                SetGeneratedPlane(plane, "Set Mesh Generated Plane");
            }

            planePicks.Clear();
            Repaint();
            SceneView.RepaintAll();
        }

        private void TryAddGeneratedPoint(Vector2 mousePosition)
        {
            if (authoring == null)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (TryRaycastGeneratedPlane(ray, out Vector3 planePoint, out Vector3 planeNormal))
            {
                AddGeneratedPoint(planePoint, planeNormal);
                return;
            }

            if (TryHitActiveMesh(mousePosition, out RaycastHit hit))
            {
                AddGeneratedPoint(hit.point, hit.normal);
            }
        }

        private bool TryAddGeneratedPreviewPoint(Rect previewRect, Vector2 mousePosition)
        {
            if (authoring == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            if (TryFindNearestGeneratedPointIndex(previewRect, mousePosition, Mathf.Max(12f, previewSelectionRadius), out int existingPointIndex))
            {
                SelectSurfaceEndpoint(FPMeshSurfaceEdgeEndpoint.Generated(existingPointIndex), "Select Generated Mesh Point");
                return true;
            }

            Vector2 localMousePosition = mousePosition - previewRect.position;
            Vector3 viewport = new Vector3(
                Mathf.Clamp01(localMousePosition.x / Mathf.Max(1f, previewRect.width)),
                Mathf.Clamp01(1f - (localMousePosition.y / Mathf.Max(1f, previewRect.height))),
                0f);
            Ray ray = previewUtility.camera.ViewportPointToRay(viewport);
            if (!TryRaycastGeneratedPlane(ray, out Vector3 point, out Vector3 normal))
            {
                return false;
            }

            AddGeneratedPoint(point, normal);
            return true;
        }

        private bool TryRemoveNearestGeneratedPreviewPoint(Rect previewRect, Vector2 mousePosition)
        {
            if (authoring == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            IReadOnlyList<FPMeshGeneratedPointRecord> points = authoring.GeneratedPoints;
            if (points == null || points.Count == 0)
            {
                return false;
            }

            Vector2 localMousePosition = mousePosition - previewRect.position;
            int nearest = -1;
            float nearestDistance = Mathf.Max(12f, previewSelectionRadius);
            for (int i = 0; i < points.Count; i++)
            {
                if (!TryProjectPreviewPoint(previewUtility.camera, previewRect, points[i].WorldPosition, out Vector2 point))
                {
                    continue;
                }

                float distance = Vector2.Distance(localMousePosition, point);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            if (nearest < 0)
            {
                return false;
            }

            Undo.RecordObject(authoring, "Remove Generated Mesh Point");
            bool removed = authoring.RemoveGeneratedPointAt(nearest);
            if (removed)
            {
                ClearPendingGeneratedEdgeStart();
                RefreshSurfaceIndexMapFromAuthoring();
                EditorUtility.SetDirty(authoring);
            }

            return removed;
        }

        private bool TryAddGeneratedEdgePreview(Rect previewRect, Vector2 mousePosition)
        {
            if (authoring == null)
            {
                return false;
            }

            if (!TryFindNearestEdgeEndpoint(previewRect, mousePosition, Mathf.Max(12f, previewSelectionRadius), out FPMeshSurfaceEdgeEndpoint endpoint))
            {
                return false;
            }
            SelectSurfaceEndpoint(endpoint, "Select Mesh Graph Endpoint");

            if (!hasPendingGeneratedEdgeStart || !TryGetEdgeEndpointWorldPosition(pendingGeneratedEdgeStart, out _))
            {
                pendingGeneratedEdgeStart = endpoint;
                hasPendingGeneratedEdgeStart = true;
                return true;
            }

            if (pendingGeneratedEdgeStart == endpoint)
            {
                ClearPendingGeneratedEdgeStart();
                return true;
            }

            Undo.RecordObject(authoring, "Add Generated Guide Edge");
            authoring.SetSourceMeshes(sourceMeshes, activeMeshIndex);
            bool added = authoring.AddGeneratedEdge(FPMeshGeneratedEdgeRecord.Create(pendingGeneratedEdgeStart, endpoint));
            ClearPendingGeneratedEdgeStart();
            if (added)
            {
                EditorUtility.SetDirty(authoring);
            }

            return true;
        }

        private bool TryPaintGeneratedEdgePreview(Rect previewRect, Vector2 mousePosition)
        {
            if (authoring == null)
            {
                return false;
            }

            if (!TryFindNearestEdgeEndpoint(previewRect, mousePosition, Mathf.Max(12f, previewSelectionRadius), out FPMeshSurfaceEdgeEndpoint endpoint))
            {
                return false;
            }
            SelectSurfaceEndpoint(endpoint, "Select Mesh Graph Endpoint");

            if (!hasPendingGeneratedEdgeStart || !TryGetEdgeEndpointWorldPosition(pendingGeneratedEdgeStart, out _))
            {
                pendingGeneratedEdgeStart = endpoint;
                hasPendingGeneratedEdgeStart = true;
                return true;
            }

            if (pendingGeneratedEdgeStart == endpoint)
            {
                return false;
            }

            FPMeshSurfaceEdgeEndpoint start = pendingGeneratedEdgeStart;
            pendingGeneratedEdgeStart = endpoint;
            hasPendingGeneratedEdgeStart = true;

            Undo.RecordObject(authoring, "Paint Generated Guide Edge");
            authoring.SetSourceMeshes(sourceMeshes, activeMeshIndex);
            bool added = authoring.AddGeneratedEdge(FPMeshGeneratedEdgeRecord.Create(start, endpoint));
            if (added)
            {
                EditorUtility.SetDirty(authoring);
            }

            return true;
        }

        private bool TryRemoveNearestGeneratedEdgePreview(Rect previewRect, Vector2 mousePosition)
        {
            if (authoring == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            IReadOnlyList<FPMeshGeneratedEdgeRecord> edges = authoring.GeneratedEdges;
            if (edges == null || edges.Count == 0)
            {
                ClearPendingGeneratedEdgeStart();
                return false;
            }

            Vector2 localMousePosition = mousePosition - previewRect.position;
            int nearest = -1;
            float nearestDistance = Mathf.Max(10f, previewSelectionRadius * 0.65f);
            for (int i = 0; i < edges.Count; i++)
            {
                FPMeshGeneratedEdgeRecord edge = edges[i];
                if (!TryProjectEdgeEndpoint(authoring.ResolveStartEndpoint(edge), previewRect, out Vector2 a) ||
                    !TryProjectEdgeEndpoint(authoring.ResolveEndEndpoint(edge), previewRect, out Vector2 b))
                {
                    continue;
                }

                float distance = DistancePointToSegment(localMousePosition, a, b);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            ClearPendingGeneratedEdgeStart();
            if (nearest < 0)
            {
                return false;
            }

            Undo.RecordObject(authoring, "Remove Generated Guide Edge");
            bool removed = authoring.RemoveGeneratedEdgeAt(nearest);
            if (removed)
            {
                EditorUtility.SetDirty(authoring);
            }

            return removed;
        }

        private bool TryAddGeneratedTrianglePreview(Rect previewRect, Vector2 mousePosition)
        {
            if (authoring == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            if (!TryFindGeneratedEdgeTriangleAtPosition(previewRect, mousePosition, false, out PreviewTrianglePick pick, out _))
            {
                return false;
            }

            Undo.RecordObject(authoring, "Add Generated Guide Triangle");
            authoring.SetSourceMeshes(sourceMeshes, activeMeshIndex);
            bool added = authoring.AddGeneratedTriangle(pick.Triangle);
            if (added)
            {
                EditorUtility.SetDirty(authoring);
            }

            return added;
        }

        private bool TryRemoveGeneratedTrianglePreview(Rect previewRect, Vector2 mousePosition)
        {
            if (authoring == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            if (!TryFindGeneratedEdgeTriangleAtPosition(previewRect, mousePosition, true, out _, out int triangleIndex))
            {
                return false;
            }

            Undo.RecordObject(authoring, "Remove Generated Guide Triangle");
            bool removed = authoring.RemoveGeneratedTriangleAt(triangleIndex);
            if (removed)
            {
                EditorUtility.SetDirty(authoring);
            }

            return removed;
        }

        private bool TryFindGeneratedEdgeTriangleAtPosition(
            Rect previewRect,
            Vector2 mousePosition,
            bool existingOnly,
            out PreviewTrianglePick pick,
            out int triangleIndex)
        {
            pick = default;
            triangleIndex = -1;
            Vector2 localMousePosition = mousePosition - previewRect.position;
            float bestArea = float.PositiveInfinity;

            if (existingOnly)
            {
                if (authoring.GeneratedTriangles == null)
                {
                    return false;
                }

                for (int i = 0; i < authoring.GeneratedTriangles.Count; i++)
                {
                    if (!TryBuildPreviewTrianglePick(authoring.GeneratedTriangles[i], previewRect, out PreviewTrianglePick candidate) ||
                        !IsPointInsideTriangle(localMousePosition, candidate.A, candidate.B, candidate.C) ||
                        candidate.Area >= bestArea)
                    {
                        continue;
                    }

                    pick = candidate;
                    triangleIndex = i;
                    bestArea = candidate.Area;
                }

                return triangleIndex >= 0;
            }

            foreach (PreviewTrianglePick candidate in BuildPreviewEdgeTriangleCandidates(previewRect))
            {
                if (!IsPointInsideTriangle(localMousePosition, candidate.A, candidate.B, candidate.C) ||
                    candidate.Area >= bestArea)
                {
                    continue;
                }

                pick = candidate;
                bestArea = candidate.Area;
            }

            return bestArea < float.PositiveInfinity;
        }

        private List<PreviewTrianglePick> BuildPreviewEdgeTriangleCandidates(Rect previewRect)
        {
            List<PreviewTrianglePick> picks = new();
            if (authoring == null || authoring.GeneratedEdges == null || authoring.GeneratedEdges.Count < 3)
            {
                return picks;
            }

            Dictionary<FPMeshSurfaceEdgeEndpoint, int> indexByEndpoint = new();
            List<FPMeshSurfaceEdgeEndpoint> endpoints = new();
            List<HashSet<int>> adjacency = new();
            IReadOnlyList<FPMeshGeneratedEdgeRecord> edges = authoring.GeneratedEdges;
            for (int i = 0; i < edges.Count; i++)
            {
                FPMeshSurfaceEdgeEndpoint start = authoring.ResolveStartEndpoint(edges[i]);
                FPMeshSurfaceEdgeEndpoint end = authoring.ResolveEndEndpoint(edges[i]);
                int startIndex = GetOrAddPreviewEndpointIndex(start, indexByEndpoint, endpoints, adjacency);
                int endIndex = GetOrAddPreviewEndpointIndex(end, indexByEndpoint, endpoints, adjacency);
                if (startIndex == endIndex)
                {
                    continue;
                }

                adjacency[startIndex].Add(endIndex);
                adjacency[endIndex].Add(startIndex);
            }

            for (int a = 0; a < endpoints.Count - 2; a++)
            {
                for (int b = a + 1; b < endpoints.Count - 1; b++)
                {
                    if (!adjacency[a].Contains(b))
                    {
                        continue;
                    }

                    for (int c = b + 1; c < endpoints.Count; c++)
                    {
                        if (!adjacency[a].Contains(c) || !adjacency[b].Contains(c))
                        {
                            continue;
                        }

                        FPMeshGeneratedTriangleRecord triangle = FPMeshGeneratedTriangleRecord.Create(endpoints[a], endpoints[b], endpoints[c]);
                        if (TryBuildPreviewTrianglePick(triangle, previewRect, out PreviewTrianglePick pick))
                        {
                            picks.Add(pick);
                        }
                    }
                }
            }

            return picks;
        }

        private static int GetOrAddPreviewEndpointIndex(
            FPMeshSurfaceEdgeEndpoint endpoint,
            Dictionary<FPMeshSurfaceEdgeEndpoint, int> indexByEndpoint,
            List<FPMeshSurfaceEdgeEndpoint> endpoints,
            List<HashSet<int>> adjacency)
        {
            if (indexByEndpoint.TryGetValue(endpoint, out int index))
            {
                return index;
            }

            index = endpoints.Count;
            indexByEndpoint[endpoint] = index;
            endpoints.Add(endpoint);
            adjacency.Add(new HashSet<int>());
            return index;
        }

        private bool TryBuildPreviewTrianglePick(FPMeshGeneratedTriangleRecord triangle, Rect previewRect, out PreviewTrianglePick pick)
        {
            pick = default;
            if (!TryProjectEdgeEndpoint(triangle.A, previewRect, out Vector2 a) ||
                !TryProjectEdgeEndpoint(triangle.B, previewRect, out Vector2 b) ||
                !TryProjectEdgeEndpoint(triangle.C, previewRect, out Vector2 c))
            {
                return false;
            }

            float area = Mathf.Abs(Cross(a, b, c));
            if (area <= 0.001f)
            {
                return false;
            }

            pick = new PreviewTrianglePick(triangle, a, b, c, area);
            return true;
        }

        private bool TryFindNearestEdgeEndpoint(Rect previewRect, Vector2 mousePosition, float maxDistance, out FPMeshSurfaceEdgeEndpoint endpoint)
        {
            endpoint = default;
            if (previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            Vector2 localMousePosition = mousePosition - previewRect.position;
            float nearestDistance = maxDistance;
            bool found = false;

            if (authoring != null && authoring.GeneratedPoints != null)
            {
                IReadOnlyList<FPMeshGeneratedPointRecord> points = authoring.GeneratedPoints;
                for (int i = 0; i < points.Count; i++)
                {
                    if (!TryProjectPreviewPoint(previewUtility.camera, previewRect, points[i].WorldPosition, out Vector2 point))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(localMousePosition, point);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        endpoint = FPMeshSurfaceEdgeEndpoint.Generated(i);
                        found = true;
                    }
                }
            }

            foreach (KeyValuePair<int, HashSet<int>> pair in selectedByMesh)
            {
                int meshIndex = pair.Key;
                if (meshIndex < 0 || meshIndex >= sourceMeshes.Count || pair.Value == null)
                {
                    continue;
                }

                MeshFilter meshFilter = sourceMeshes[meshIndex];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Vector3[] vertices = meshFilter.sharedMesh.vertices;
                Matrix4x4 matrix = meshFilter.transform.localToWorldMatrix;
                foreach (int vertexIndex in pair.Value)
                {
                    if (vertexIndex < 0 || vertexIndex >= vertices.Length)
                    {
                        continue;
                    }

                    Vector3 world = matrix.MultiplyPoint3x4(vertices[vertexIndex]);
                    if (!TryProjectPreviewPoint(previewUtility.camera, previewRect, world, out Vector2 point))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(localMousePosition, point);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        endpoint = FPMeshSurfaceEdgeEndpoint.Source(meshIndex, vertexIndex);
                        found = true;
                    }
                }
            }

            return found;
        }

        private bool TryProjectEdgeEndpoint(FPMeshSurfaceEdgeEndpoint endpoint, Rect rect, out Vector2 point)
        {
            point = default;
            return TryGetEdgeEndpointWorldPosition(endpoint, out Vector3 world) &&
                TryProjectPreviewPoint(previewUtility.camera, rect, world, out point);
        }

        private bool TryGetEdgeEndpointWorldPosition(FPMeshSurfaceEdgeEndpoint endpoint, out Vector3 world)
        {
            world = default;
            if (endpoint.Kind == FPMeshSurfacePointKind.GeneratedPoint)
            {
                if (authoring == null || authoring.GeneratedPoints == null ||
                    endpoint.GeneratedPointIndex < 0 || endpoint.GeneratedPointIndex >= authoring.GeneratedPoints.Count)
                {
                    return false;
                }

                world = authoring.GeneratedPoints[endpoint.GeneratedPointIndex].WorldPosition;
                return true;
            }

            if (endpoint.SourceMeshIndex < 0 || endpoint.SourceMeshIndex >= sourceMeshes.Count)
            {
                return false;
            }

            MeshFilter meshFilter = sourceMeshes[endpoint.SourceMeshIndex];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            if (endpoint.VertexIndex < 0 || endpoint.VertexIndex >= vertices.Length)
            {
                return false;
            }

            world = meshFilter.transform.TransformPoint(vertices[endpoint.VertexIndex]);
            return true;
        }

        private bool TryFindNearestGeneratedPointIndex(Rect previewRect, Vector2 mousePosition, float maxDistance, out int pointIndex)
        {
            pointIndex = -1;
            if (authoring == null || previewUtility == null || previewUtility.camera == null)
            {
                return false;
            }

            IReadOnlyList<FPMeshGeneratedPointRecord> points = authoring.GeneratedPoints;
            if (points == null || points.Count == 0)
            {
                return false;
            }

            Vector2 localMousePosition = mousePosition - previewRect.position;
            float nearestDistance = maxDistance;
            for (int i = 0; i < points.Count; i++)
            {
                if (!TryProjectPreviewPoint(previewUtility.camera, previewRect, points[i].WorldPosition, out Vector2 point))
                {
                    continue;
                }

                float distance = Vector2.Distance(localMousePosition, point);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    pointIndex = i;
                }
            }

            return pointIndex >= 0;
        }

        private void AddGeneratedPoint(Vector3 point, Vector3 normal)
        {
            if (authoring == null)
            {
                return;
            }

            Undo.RecordObject(authoring, "Add Generated Mesh Point");
            int generatedPointIndex = authoring.GeneratedPoints == null ? 0 : authoring.GeneratedPoints.Count;
            authoring.AddGeneratedPoint(new FPMeshGeneratedPointRecord
            {
                SurfaceIndex = -1,
                WorldPosition = point,
                Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up,
                Tags = generatedPointTags
            });

            if (generatedPointIndex < authoring.GeneratedPoints.Count)
            {
                FPMeshSurfaceEdgeEndpoint endpoint = FPMeshSurfaceEdgeEndpoint.Generated(generatedPointIndex);
                surfaceIndexByEndpoint[endpoint] = authoring.GeneratedPoints[generatedPointIndex].SurfaceIndex;
                tagsByEndpoint[endpoint] = authoring.GeneratedPoints[generatedPointIndex].Tags;
            }

            EditorUtility.SetDirty(authoring);
            SceneView.RepaintAll();
        }

        private bool TryRaycastGeneratedPlane(Ray ray, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;
            if (!GetCurrentGeneratedPlane(out FPMeshGeneratedPlane plane))
            {
                return false;
            }

            Plane unityPlane = new Plane(plane.Normal.normalized, plane.Origin);
            if (!unityPlane.Raycast(ray, out float distance))
            {
                return false;
            }

            point = ray.GetPoint(distance);
            normal = plane.Normal.normalized;
            return true;
        }

        private void CreateSurfaceMeshFromPoints()
        {
            if (!TryCreateGeneratedSurfaceMesh(out Mesh mesh, out Vector3 meshOrigin, out List<FPMeshGeneratedSurfaceVertexLookupRecord> lookupRecords))
            {
                return;
            }

            if (!createGeneratedSurfaceSceneObject)
            {
                if (!EditorUtility.IsPersistent(mesh))
                {
                    DestroyImmediate(mesh);
                }

                return;
            }

            CreateGeneratedSurfaceSceneObject(mesh, meshOrigin, lookupRecords);
        }

        private void SaveSurfaceMeshAsset()
        {
            if (!TryCreateGeneratedSurfaceMesh(out Mesh mesh, out _, out _))
            {
                return;
            }

            Mesh savedMesh = FPSVGMeshAssetUtility.SaveMeshAsset(mesh, generatedSurfaceOutputFolder, ResolveGeneratedSurfaceMeshName(), out string message);
            if (savedMesh == null)
            {
                if (mesh != null && !EditorUtility.IsPersistent(mesh))
                {
                    DestroyImmediate(mesh);
                }

                EditorUtility.DisplayDialog("Save Surface Mesh Asset", string.IsNullOrWhiteSpace(message) ? "The mesh asset could not be saved." : message, "OK");
                return;
            }

            Selection.activeObject = savedMesh;
            EditorGUIUtility.PingObject(savedMesh);
            EditorUtility.DisplayDialog("Save Surface Mesh Asset", message, "OK");
        }

        private void ExportSurfaceMeshObj()
        {
            if (!TryCreateGeneratedSurfaceMesh(out Mesh mesh, out _, out _))
            {
                return;
            }

            FPMeshObjExport.ExportMeshWithDialog(mesh, ResolveGeneratedSurfaceMeshName(), null, true);
        }

        private bool TryCreateGeneratedSurfaceMesh(
            out Mesh mesh,
            out Vector3 meshOrigin,
            out List<FPMeshGeneratedSurfaceVertexLookupRecord> lookupRecords)
        {
            mesh = null;
            meshOrigin = Vector3.zero;
            lookupRecords = null;
            if (!GetCurrentGeneratedPlane(out FPMeshGeneratedPlane plane))
            {
                Debug.LogWarning("Create a generated plane before creating a surface mesh.");
                return false;
            }

            List<Vector3> worldPoints = new();
            CollectSurfaceWorldPoints(worldPoints);
            bool canBuildFromExplicitTriangles = surfaceBuildMode == SurfaceBuildMode.EdgeTriangles &&
                authoring != null &&
                authoring.GeneratedTriangles != null &&
                authoring.GeneratedTriangles.Count > 0;
            if (worldPoints.Count < 3 && !canBuildFromExplicitTriangles)
            {
                Debug.LogWarning("Select or generate at least 3 points before creating a surface mesh.");
                return false;
            }

            Vector3 right = plane.Right.sqrMagnitude > 0.0001f ? plane.Right.normalized : Vector3.right;
            Vector3 forward = plane.Forward.sqrMagnitude > 0.0001f ? plane.Forward.normalized : Vector3.forward;
            Vector3 planeNormal = plane.Normal.sqrMagnitude > 0.0001f ? plane.Normal.normalized : Vector3.up;
            Vector3 normal = flipGeneratedSurfaceNormals ? -planeNormal : planeNormal;
            List<Vector2> projectedPoints = BuildProjectedPlanePoints(worldPoints, plane.Origin, right, forward);
            if (!TryBuildSurfaceMesh(projectedPoints, plane.Origin, right, forward, normal, flipGeneratedSurfaceNormals, out mesh, out meshOrigin, out lookupRecords))
            {
                return false;
            }

            mesh.name = ResolveGeneratedSurfaceMeshName();
            return true;
        }

        private void CreateGeneratedSurfaceSceneObject(
            Mesh mesh,
            Vector3 meshOrigin,
            IReadOnlyList<FPMeshGeneratedSurfaceVertexLookupRecord> lookupRecords)
        {
            if (mesh == null)
            {
                return;
            }

            GameObject surface = new GameObject(ResolveGeneratedSurfaceMeshName());
            surface.transform.position = meshOrigin;
            MeshFilter filter = surface.AddComponent<MeshFilter>();
            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;

            Material material = CreateGeneratedSurfaceMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            FPMeshGeneratedSurfaceLookup lookup = surface.AddComponent<FPMeshGeneratedSurfaceLookup>();
            lookup.SetLookup(authoring, mesh, lookupRecords);

            Undo.RegisterCreatedObjectUndo(surface, "Create Scene Surface Mesh");
            Selection.activeGameObject = surface;
        }

        private static Material CreateGeneratedSurfaceMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            return new Material(shader)
            {
                name = "FP_GeneratedSurfaceMaterial",
                color = new Color(0.25f, 0.8f, 1f, 0.65f)
            };
        }

        private string ResolveGeneratedSurfaceMeshName()
        {
            return string.IsNullOrWhiteSpace(generatedSurfaceMeshName)
                ? "FP_GeneratedVertexSurface"
                : generatedSurfaceMeshName.Trim();
        }

        private bool TryBuildSurfaceMesh(
            IReadOnlyList<Vector2> projectedPoints,
            Vector3 planeOrigin,
            Vector3 right,
            Vector3 forward,
            Vector3 normal,
            bool flipWinding,
            out Mesh mesh,
            out Vector3 meshOrigin,
            out List<FPMeshGeneratedSurfaceVertexLookupRecord> lookupRecords)
        {
            mesh = null;
            meshOrigin = Vector3.zero;
            lookupRecords = null;
            if (surfaceBuildMode == SurfaceBuildMode.EdgeTriangles)
            {
                return TryBuildEdgeGraphSurfaceMesh(planeOrigin, right, forward, normal, flipWinding, out mesh, out meshOrigin, out lookupRecords);
            }

            if (projectedPoints == null || projectedPoints.Count < 3)
            {
                Debug.LogWarning("The current points do not form a valid surface.");
                return false;
            }

            if (surfaceBuildMode == SurfaceBuildMode.QuadPatch)
            {
                return TryBuildQuadPatchSurfaceMesh(projectedPoints, planeOrigin, right, forward, normal, flipWinding, out mesh, out meshOrigin, out lookupRecords);
            }

            return TryBuildBoundarySurfaceMesh(projectedPoints, planeOrigin, right, forward, normal, flipWinding, out mesh, out meshOrigin, out lookupRecords);
        }

        private bool TryBuildBoundarySurfaceMesh(
            IReadOnlyList<Vector2> projectedPoints,
            Vector3 planeOrigin,
            Vector3 right,
            Vector3 forward,
            Vector3 normal,
            bool flipWinding,
            out Mesh mesh,
            out Vector3 meshOrigin,
            out List<FPMeshGeneratedSurfaceVertexLookupRecord> lookupRecords)
        {
            mesh = null;
            meshOrigin = Vector3.zero;
            lookupRecords = null;
            List<Vector2> boundary = TryBuildGeneratedEdgeLoop(planeOrigin, right, forward, out List<Vector2> edgeLoop)
                ? edgeLoop
                : BuildConvexHull(projectedPoints);
            if (boundary.Count < 3)
            {
                Debug.LogWarning("The current points do not produce a valid boundary surface.");
                return false;
            }

            List<string> warnings = new();
            FPSVGTriangulation triangulation = FPSVGPolygonTriangulator.Triangulate(boundary, null, warnings, "FP Mesh Vertex Surface", true, 8);
            if (triangulation.Triangles.Count < 3)
            {
                Debug.LogWarning(warnings.Count > 0 ? warnings[0] : "The current boundary could not be triangulated.");
                return false;
            }

            mesh = BuildPlaneSurfaceMesh(triangulation.Vertices, triangulation.Triangles, planeOrigin, right, forward, normal, flipWinding, out meshOrigin);
            lookupRecords = BuildUnmappedLookupRecords(mesh, meshOrigin);
            return mesh != null;
        }

        private bool TryBuildEdgeGraphSurfaceMesh(
            Vector3 planeOrigin,
            Vector3 right,
            Vector3 forward,
            Vector3 normal,
            bool flipWinding,
            out Mesh mesh,
            out Vector3 meshOrigin,
            out List<FPMeshGeneratedSurfaceVertexLookupRecord> lookupRecords)
        {
            mesh = null;
            meshOrigin = Vector3.zero;
            lookupRecords = null;
            if (authoring == null || authoring.GeneratedEdges == null || authoring.GeneratedEdges.Count < 3)
            {
                Debug.LogWarning("Paint at least 3 guide edges before creating an edge triangle surface.");
                return false;
            }

            Dictionary<FPMeshSurfaceEdgeEndpoint, int> indexByEndpoint = new();
            List<Vector2> vertices2D = new();
            List<FPMeshSurfaceEdgeEndpoint> endpointsByVertex = new();
            List<HashSet<int>> adjacency = new();
            IReadOnlyList<FPMeshGeneratedEdgeRecord> edges = authoring.GeneratedEdges;
            for (int i = 0; i < edges.Count; i++)
            {
                FPMeshSurfaceEdgeEndpoint start = authoring.ResolveStartEndpoint(edges[i]);
                FPMeshSurfaceEdgeEndpoint end = authoring.ResolveEndEndpoint(edges[i]);
                if (start == end ||
                    !TryGetOrAddGraphEndpoint(start, planeOrigin, right, forward, indexByEndpoint, vertices2D, endpointsByVertex, adjacency, out int startIndex) ||
                    !TryGetOrAddGraphEndpoint(end, planeOrigin, right, forward, indexByEndpoint, vertices2D, endpointsByVertex, adjacency, out int endIndex) ||
                    startIndex == endIndex)
                {
                    continue;
                }

                adjacency[startIndex].Add(endIndex);
                adjacency[endIndex].Add(startIndex);
            }

            List<int> explicitTriangles = authoring.GeneratedTriangles != null && authoring.GeneratedTriangles.Count > 0
                ? BuildExplicitGeneratedTriangles(planeOrigin, right, forward, indexByEndpoint, vertices2D, endpointsByVertex, adjacency)
                : new List<int>();
            List<int> triangles = useSelectedTrianglesOnly
                ? explicitTriangles
                : MergeTriangleLists(BuildEdgeGraphTriangles(vertices2D, adjacency), explicitTriangles);
            if (triangles.Count < 3)
            {
                Debug.LogWarning(useSelectedTrianglesOnly
                    ? "Select at least one valid guide triangle before creating a selected-triangle-only surface."
                    : "The current guide edges do not contain any closed triangle faces.");
                return false;
            }

            mesh = BuildPlaneSurfaceMesh(vertices2D, triangles, planeOrigin, right, forward, normal, flipWinding, out meshOrigin);
            lookupRecords = BuildEndpointLookupRecords(mesh, meshOrigin, endpointsByVertex);
            return mesh != null;
        }

        private List<int> BuildExplicitGeneratedTriangles(
            Vector3 planeOrigin,
            Vector3 right,
            Vector3 forward,
            Dictionary<FPMeshSurfaceEdgeEndpoint, int> indexByEndpoint,
            List<Vector2> vertices2D,
            List<FPMeshSurfaceEdgeEndpoint> endpointsByVertex,
            List<HashSet<int>> adjacency)
        {
            List<int> triangles = new();
            if (authoring == null || authoring.GeneratedTriangles == null)
            {
                return triangles;
            }

            for (int i = 0; i < authoring.GeneratedTriangles.Count; i++)
            {
                FPMeshGeneratedTriangleRecord triangle = authoring.GeneratedTriangles[i];
                if (!TryGetOrAddGraphEndpoint(triangle.A, planeOrigin, right, forward, indexByEndpoint, vertices2D, endpointsByVertex, adjacency, out int a) ||
                    !TryGetOrAddGraphEndpoint(triangle.B, planeOrigin, right, forward, indexByEndpoint, vertices2D, endpointsByVertex, adjacency, out int b) ||
                    !TryGetOrAddGraphEndpoint(triangle.C, planeOrigin, right, forward, indexByEndpoint, vertices2D, endpointsByVertex, adjacency, out int c) ||
                    a == b || b == c || c == a)
                {
                    continue;
                }

                float area = Cross(vertices2D[a], vertices2D[b], vertices2D[c]);
                if (Mathf.Abs(area) <= 0.00001f)
                {
                    continue;
                }

                if (area > 0f)
                {
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                }
                else
                {
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                }
            }

            return triangles;
        }

        private bool TryBuildQuadPatchSurfaceMesh(
            IReadOnlyList<Vector2> projectedPoints,
            Vector3 planeOrigin,
            Vector3 right,
            Vector3 forward,
            Vector3 normal,
            bool flipWinding,
            out Mesh mesh,
            out Vector3 meshOrigin,
            out List<FPMeshGeneratedSurfaceVertexLookupRecord> lookupRecords)
        {
            mesh = null;
            meshOrigin = Vector3.zero;
            lookupRecords = null;
            if (!TryGetProjectedBounds(projectedPoints, out Vector2 min, out Vector2 max))
            {
                Debug.LogWarning("The current points do not produce a valid quad patch.");
                return false;
            }

            if (Mathf.Abs(max.x - min.x) <= 0.0001f)
            {
                min.x -= 0.5f;
                max.x += 0.5f;
            }

            if (Mathf.Abs(max.y - min.y) <= 0.0001f)
            {
                min.y -= 0.5f;
                max.y += 0.5f;
            }

            int columns = Mathf.Max(1, quadSurfaceColumns);
            int rows = Mathf.Max(1, quadSurfaceRows);
            List<Vector2> vertices2D = new((columns + 1) * (rows + 1));
            for (int y = 0; y <= rows; y++)
            {
                float v = y / (float)rows;
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns;
                    vertices2D.Add(new Vector2(Mathf.Lerp(min.x, max.x, u), Mathf.Lerp(min.y, max.y, v)));
                }
            }

            List<int> triangles = new(columns * rows * 6);
            int stride = columns + 1;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int a = (y * stride) + x;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(a);
                    triangles.Add(d);
                    triangles.Add(c);
                }
            }

            mesh = BuildPlaneSurfaceMesh(vertices2D, triangles, planeOrigin, right, forward, normal, flipWinding, out meshOrigin);
            lookupRecords = BuildUnmappedLookupRecords(mesh, meshOrigin);
            return mesh != null;
        }

        private static Mesh BuildPlaneSurfaceMesh(
            IReadOnlyList<Vector2> vertices2D,
            IReadOnlyList<int> triangles,
            Vector3 planeOrigin,
            Vector3 right,
            Vector3 forward,
            Vector3 normal,
            bool flipWinding,
            out Vector3 meshOrigin)
        {
            meshOrigin = Vector3.zero;
            if (vertices2D == null || vertices2D.Count < 3 || triangles == null || triangles.Count < 3)
            {
                return null;
            }

            for (int i = 0; i < vertices2D.Count; i++)
            {
                Vector2 point = vertices2D[i];
                meshOrigin += planeOrigin + (right * point.x) + (forward * point.y);
            }

            meshOrigin /= Mathf.Max(1, vertices2D.Count);

            List<Vector3> vertices = new(vertices2D.Count);
            List<Vector3> normals = new(vertices2D.Count);
            List<Vector2> uvs = new(vertices2D.Count);
            for (int i = 0; i < vertices2D.Count; i++)
            {
                Vector2 point = vertices2D[i];
                Vector3 world = planeOrigin + (right * point.x) + (forward * point.y);
                vertices.Add(world - meshOrigin);
                normals.Add(normal);
                uvs.Add(point);
            }

            Mesh mesh = new Mesh
            {
                name = "FP_GeneratedSurfaceMesh"
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(flipWinding ? BuildFlippedTriangles(triangles) : new List<int>(triangles), 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int> BuildFlippedTriangles(IReadOnlyList<int> triangles)
        {
            List<int> flipped = new(triangles.Count);
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                flipped.Add(triangles[i]);
                flipped.Add(triangles[i + 2]);
                flipped.Add(triangles[i + 1]);
            }

            return flipped;
        }

        private static List<FPMeshGeneratedSurfaceVertexLookupRecord> BuildUnmappedLookupRecords(Mesh mesh, Vector3 meshOrigin)
        {
            List<FPMeshGeneratedSurfaceVertexLookupRecord> records = new();
            if (mesh == null)
            {
                return records;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                records.Add(new FPMeshGeneratedSurfaceVertexLookupRecord
                {
                    MeshVertexIndex = i,
                    SurfaceIndex = -1,
                    HasEndpoint = false,
                    Endpoint = default,
                    LocalPosition = vertices[i],
                    WorldPosition = meshOrigin + vertices[i]
                });
            }

            return records;
        }

        private List<FPMeshGeneratedSurfaceVertexLookupRecord> BuildEndpointLookupRecords(
            Mesh mesh,
            Vector3 meshOrigin,
            IReadOnlyList<FPMeshSurfaceEdgeEndpoint> endpointsByVertex)
        {
            List<FPMeshGeneratedSurfaceVertexLookupRecord> records = new();
            if (mesh == null)
            {
                return records;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                bool hasEndpoint = endpointsByVertex != null && i < endpointsByVertex.Count;
                FPMeshSurfaceEdgeEndpoint endpoint = hasEndpoint ? endpointsByVertex[i] : default;
                int surfaceIndex = hasEndpoint ? GetOrAssignSurfaceIndex(endpoint) : -1;
                records.Add(new FPMeshGeneratedSurfaceVertexLookupRecord
                {
                    MeshVertexIndex = i,
                    SurfaceIndex = surfaceIndex,
                    HasEndpoint = hasEndpoint,
                    Endpoint = endpoint,
                    LocalPosition = vertices[i],
                    WorldPosition = meshOrigin + vertices[i]
                });
            }

            return records;
        }

        private bool TryGetOrAddGraphEndpoint(
            FPMeshSurfaceEdgeEndpoint endpoint,
            Vector3 origin,
            Vector3 right,
            Vector3 forward,
            Dictionary<FPMeshSurfaceEdgeEndpoint, int> indexByEndpoint,
            List<Vector2> vertices2D,
            List<FPMeshSurfaceEdgeEndpoint> endpointsByVertex,
            List<HashSet<int>> adjacency,
            out int index)
        {
            if (indexByEndpoint.TryGetValue(endpoint, out index))
            {
                return true;
            }

            if (!TryGetEdgeEndpointWorldPosition(endpoint, out Vector3 world))
            {
                index = -1;
                return false;
            }

            Vector3 relative = world - origin;
            index = vertices2D.Count;
            indexByEndpoint[endpoint] = index;
            vertices2D.Add(new Vector2(Vector3.Dot(relative, right), Vector3.Dot(relative, forward)));
            endpointsByVertex.Add(endpoint);
            adjacency.Add(new HashSet<int>());
            return true;
        }

        private static List<int> BuildEdgeGraphTriangles(IReadOnlyList<Vector2> vertices2D, IReadOnlyList<HashSet<int>> adjacency)
        {
            List<int> triangles = new();
            if (vertices2D == null || adjacency == null || vertices2D.Count < 3)
            {
                return triangles;
            }

            for (int a = 0; a < vertices2D.Count - 2; a++)
            {
                for (int b = a + 1; b < vertices2D.Count - 1; b++)
                {
                    if (!adjacency[a].Contains(b))
                    {
                        continue;
                    }

                    for (int c = b + 1; c < vertices2D.Count; c++)
                    {
                        if (!adjacency[a].Contains(c) || !adjacency[b].Contains(c))
                        {
                            continue;
                        }

                        Vector2 pa = vertices2D[a];
                        Vector2 pb = vertices2D[b];
                        Vector2 pc = vertices2D[c];
                        float area = Cross(pa, pb, pc);
                        if (Mathf.Abs(area) <= 0.00001f || TriangleContainsOtherPoint(vertices2D, a, b, c))
                        {
                            continue;
                        }

                        if (area > 0f)
                        {
                            triangles.Add(a);
                            triangles.Add(b);
                            triangles.Add(c);
                        }
                        else
                        {
                            triangles.Add(a);
                            triangles.Add(c);
                            triangles.Add(b);
                        }
                    }
                }
            }

            return triangles;
        }

        private static List<int> MergeTriangleLists(IReadOnlyList<int> primary, IReadOnlyList<int> secondary)
        {
            List<int> merged = new();
            AppendUniqueTriangles(merged, primary);
            AppendUniqueTriangles(merged, secondary);
            return merged;
        }

        private static void AppendUniqueTriangles(List<int> destination, IReadOnlyList<int> source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            for (int i = 0; i + 2 < source.Count; i += 3)
            {
                int a = source[i];
                int b = source[i + 1];
                int c = source[i + 2];
                if (ContainsTriangle(destination, a, b, c))
                {
                    continue;
                }

                destination.Add(a);
                destination.Add(b);
                destination.Add(c);
            }
        }

        private static bool ContainsTriangle(IReadOnlyList<int> triangles, int a, int b, int c)
        {
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                if (TriangleIndicesMatch(triangles[i], triangles[i + 1], triangles[i + 2], a, b, c))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TriangleIndicesMatch(int leftA, int leftB, int leftC, int rightA, int rightB, int rightC)
        {
            return (leftA == rightA || leftA == rightB || leftA == rightC) &&
                (leftB == rightA || leftB == rightB || leftB == rightC) &&
                (leftC == rightA || leftC == rightB || leftC == rightC);
        }

        private static bool TriangleContainsOtherPoint(IReadOnlyList<Vector2> points, int a, int b, int c)
        {
            Vector2 pa = points[a];
            Vector2 pb = points[b];
            Vector2 pc = points[c];
            for (int i = 0; i < points.Count; i++)
            {
                if (i == a || i == b || i == c)
                {
                    continue;
                }

                if (IsPointStrictlyInsideTriangle(points[i], pa, pb, pc))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointStrictlyInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            const float edgeEpsilon = 0.00001f;
            float area = Cross(a, b, c);
            if (Mathf.Abs(area) <= edgeEpsilon)
            {
                return false;
            }

            float ab = Cross(a, b, point);
            float bc = Cross(b, c, point);
            float ca = Cross(c, a, point);
            if (area < 0f)
            {
                ab = -ab;
                bc = -bc;
                ca = -ca;
            }

            return ab > edgeEpsilon && bc > edgeEpsilon && ca > edgeEpsilon;
        }

        private static bool IsPointInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            const float edgeEpsilon = -0.001f;
            float area = Cross(a, b, c);
            if (Mathf.Abs(area) <= 0.00001f)
            {
                return false;
            }

            float ab = Cross(a, b, point);
            float bc = Cross(b, c, point);
            float ca = Cross(c, a, point);
            if (area < 0f)
            {
                ab = -ab;
                bc = -bc;
                ca = -ca;
            }

            return ab >= edgeEpsilon && bc >= edgeEpsilon && ca >= edgeEpsilon;
        }

        private static List<Vector2> BuildProjectedPlanePoints(IReadOnlyList<Vector3> worldPoints, Vector3 origin, Vector3 right, Vector3 forward)
        {
            List<Vector2> projected = new(worldPoints.Count);
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector3 relative = worldPoints[i] - origin;
                AddUniqueProjectedPoint(projected, new Vector2(Vector3.Dot(relative, right), Vector3.Dot(relative, forward)));
            }

            return projected;
        }

        private bool TryBuildGeneratedEdgeLoop(Vector3 origin, Vector3 right, Vector3 forward, out List<Vector2> loop)
        {
            loop = null;
            if (authoring == null || authoring.GeneratedEdges == null || authoring.GeneratedEdges.Count < 3)
            {
                return false;
            }

            IReadOnlyList<FPMeshGeneratedEdgeRecord> edges = authoring.GeneratedEdges;
            Dictionary<FPMeshSurfaceEdgeEndpoint, List<FPMeshSurfaceEdgeEndpoint>> adjacency = new();
            for (int i = 0; i < edges.Count; i++)
            {
                FPMeshGeneratedEdgeRecord edge = edges[i];
                FPMeshSurfaceEdgeEndpoint start = authoring.ResolveStartEndpoint(edge);
                FPMeshSurfaceEdgeEndpoint end = authoring.ResolveEndEndpoint(edge);
                if (start == end ||
                    !TryGetEdgeEndpointWorldPosition(start, out _) ||
                    !TryGetEdgeEndpointWorldPosition(end, out _))
                {
                    return false;
                }

                AddEdgeNeighbor(adjacency, start, end);
                AddEdgeNeighbor(adjacency, end, start);
            }

            foreach (KeyValuePair<FPMeshSurfaceEdgeEndpoint, List<FPMeshSurfaceEdgeEndpoint>> pair in adjacency)
            {
                if (pair.Value.Count != 2)
                {
                    return false;
                }
            }

            FPMeshSurfaceEdgeEndpoint startEndpoint = authoring.ResolveStartEndpoint(edges[0]);
            bool hasPrevious = false;
            FPMeshSurfaceEdgeEndpoint previous = default;
            FPMeshSurfaceEdgeEndpoint current = startEndpoint;
            List<FPMeshSurfaceEdgeEndpoint> ordered = new(adjacency.Count);
            HashSet<FPMeshSurfaceEdgeEndpoint> visited = new();
            int guard = adjacency.Count + 1;
            while (guard-- > 0)
            {
                ordered.Add(current);
                visited.Add(current);
                List<FPMeshSurfaceEdgeEndpoint> neighbors = adjacency[current];
                FPMeshSurfaceEdgeEndpoint next = hasPrevious && neighbors[0] == previous ? neighbors[1] : neighbors[0];
                if (next == startEndpoint)
                {
                    break;
                }

                if (visited.Contains(next))
                {
                    return false;
                }

                previous = current;
                hasPrevious = true;
                current = next;
            }

            if (ordered.Count != adjacency.Count)
            {
                return false;
            }

            loop = new List<Vector2>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                if (!TryGetEdgeEndpointWorldPosition(ordered[i], out Vector3 world))
                {
                    return false;
                }

                Vector3 relative = world - origin;
                loop.Add(new Vector2(Vector3.Dot(relative, right), Vector3.Dot(relative, forward)));
            }

            return loop.Count >= 3;
        }

        private static void AddEdgeNeighbor(
            Dictionary<FPMeshSurfaceEdgeEndpoint, List<FPMeshSurfaceEdgeEndpoint>> adjacency,
            FPMeshSurfaceEdgeEndpoint a,
            FPMeshSurfaceEdgeEndpoint b)
        {
            if (!adjacency.TryGetValue(a, out List<FPMeshSurfaceEdgeEndpoint> neighbors))
            {
                neighbors = new List<FPMeshSurfaceEdgeEndpoint>(2);
                adjacency[a] = neighbors;
            }

            if (!neighbors.Contains(b))
            {
                neighbors.Add(b);
            }
        }

        private static void AddUniqueProjectedPoint(List<Vector2> points, Vector2 point)
        {
            const float duplicateDistanceSqr = 0.000001f;
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - point).sqrMagnitude <= duplicateDistanceSqr)
                {
                    return;
                }
            }

            points.Add(point);
        }

        private static List<Vector2> BuildConvexHull(IReadOnlyList<Vector2> points)
        {
            List<Vector2> sorted = new(points);
            sorted.Sort((a, b) =>
            {
                int x = a.x.CompareTo(b.x);
                return x != 0 ? x : a.y.CompareTo(b.y);
            });

            List<Vector2> unique = new(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                AddUniqueProjectedPoint(unique, sorted[i]);
            }

            if (unique.Count <= 3)
            {
                return unique;
            }

            List<Vector2> lower = new();
            for (int i = 0; i < unique.Count; i++)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], unique[i]) <= 0f)
                {
                    lower.RemoveAt(lower.Count - 1);
                }

                lower.Add(unique[i]);
            }

            List<Vector2> upper = new();
            for (int i = unique.Count - 1; i >= 0; i--)
            {
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], unique[i]) <= 0f)
                {
                    upper.RemoveAt(upper.Count - 1);
                }

                upper.Add(unique[i]);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static bool TryGetProjectedBounds(IReadOnlyList<Vector2> points, out Vector2 min, out Vector2 max)
        {
            min = Vector2.zero;
            max = Vector2.zero;
            if (points == null || points.Count == 0)
            {
                return false;
            }

            min = points[0];
            max = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            return true;
        }

        private static float Cross(Vector2 origin, Vector2 a, Vector2 b)
        {
            return ((a.x - origin.x) * (b.y - origin.y)) - ((a.y - origin.y) * (b.x - origin.x));
        }

        private void CollectSurfaceWorldPoints(List<Vector3> points)
        {
            CollectSelectedWorldPoints(points);
            if (authoring == null || authoring.GeneratedPoints == null)
            {
                return;
            }

            IReadOnlyList<FPMeshGeneratedPointRecord> generatedPoints = authoring.GeneratedPoints;
            for (int i = 0; i < generatedPoints.Count; i++)
            {
                AddUniqueWorldPoint(points, generatedPoints[i].WorldPosition);
            }

            if (authoring.GeneratedTriangles == null)
            {
                return;
            }

            for (int i = 0; i < authoring.GeneratedTriangles.Count; i++)
            {
                FPMeshGeneratedTriangleRecord triangle = authoring.GeneratedTriangles[i];
                AddTriangleEndpointWorldPoint(points, triangle.A);
                AddTriangleEndpointWorldPoint(points, triangle.B);
                AddTriangleEndpointWorldPoint(points, triangle.C);
            }
        }

        private void AddTriangleEndpointWorldPoint(List<Vector3> points, FPMeshSurfaceEdgeEndpoint endpoint)
        {
            if (TryGetEdgeEndpointWorldPosition(endpoint, out Vector3 world))
            {
                AddUniqueWorldPoint(points, world);
            }
        }

        private void CollectSelectedWorldPoints(List<Vector3> points)
        {
            foreach (KeyValuePair<int, HashSet<int>> pair in selectedByMesh)
            {
                int meshIndex = pair.Key;
                if (meshIndex < 0 || meshIndex >= sourceMeshes.Count)
                {
                    continue;
                }

                MeshFilter meshFilter = sourceMeshes[meshIndex];
                if (meshFilter == null || meshFilter.sharedMesh == null || pair.Value == null)
                {
                    continue;
                }

                Vector3[] vertices = meshFilter.sharedMesh.vertices;
                foreach (int vertexIndex in pair.Value)
                {
                    if (vertexIndex < 0 || vertexIndex >= vertices.Length)
                    {
                        continue;
                    }

                    AddUniqueWorldPoint(points, meshFilter.transform.TransformPoint(vertices[vertexIndex]));
                }
            }
        }

        private static void AddUniqueWorldPoint(List<Vector3> points, Vector3 point)
        {
            const float duplicateDistanceSqr = 0.000001f;
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - point).sqrMagnitude <= duplicateDistanceSqr)
                {
                    return;
                }
            }

            points.Add(point);
        }

        private static List<Vector2> BuildSortedPlaneLoop(IReadOnlyList<Vector3> worldPoints, Vector3 origin, Vector3 right, Vector3 forward)
        {
            List<Vector2> projected = new(worldPoints.Count);
            Vector2 center = Vector2.zero;
            for (int i = 0; i < worldPoints.Count; i++)
            {
                Vector3 relative = worldPoints[i] - origin;
                Vector2 point = new Vector2(Vector3.Dot(relative, right), Vector3.Dot(relative, forward));
                projected.Add(point);
                center += point;
            }

            center /= Mathf.Max(1, projected.Count);
            projected.Sort((a, b) =>
            {
                float angleA = Mathf.Atan2(a.y - center.y, a.x - center.x);
                float angleB = Mathf.Atan2(b.y - center.y, b.x - center.x);
                return angleA.CompareTo(angleB);
            });

            return projected;
        }

        private int FindNearestVertex(MeshFilter meshFilter, Vector2 mousePosition, float maxDistance)
        {
            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int nearest = -1;
            float nearestDistance = maxDistance;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 guiPoint = HandleUtility.WorldToGUIPoint(meshFilter.transform.TransformPoint(vertices[i]));
                float distance = Vector2.Distance(mousePosition, guiPoint);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            return nearest;
        }

        private bool TryHitActiveMesh(Vector2 mousePosition, out RaycastHit hit)
        {
            hit = default;
            MeshFilter meshFilter = GetActiveMesh();
            if (meshFilter == null)
            {
                return false;
            }

            Collider collider = meshFilter.GetComponent<Collider>();
            if (collider == null)
            {
                return false;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            return collider.Raycast(ray, out hit, 5000f);
        }

        private MeshFilter GetActiveMesh()
        {
            if (activeMeshIndex < 0 || activeMeshIndex >= sourceMeshes.Count)
            {
                return null;
            }

            return sourceMeshes[activeMeshIndex];
        }

        private void PullFromSelection()
        {
            sourceMeshes.Clear();
            selectedByMesh.Clear();

            foreach (GameObject selected in Selection.gameObjects)
            {
                if (selected == null)
                {
                    continue;
                }

                if (authoring == null)
                {
                    authoring = selected.GetComponent<FPMeshVertexPaintAuthoring>();
                }

                MeshFilter[] filters = selected.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i] != null && !sourceMeshes.Contains(filters[i]))
                    {
                        sourceMeshes.Add(filters[i]);
                    }
                }
            }

            activeMeshIndex = Mathf.Clamp(activeMeshIndex, 0, Mathf.Max(0, sourceMeshes.Count - 1));
            if (authoring != null)
            {
                generatedPointTags = authoring.DefaultTags;
            }

            SyncSourceMeshesToAuthoring("Use Mesh Vertex Painter Selection");
            Repaint();
            SceneView.RepaintAll();
        }

        private void ReadFromAuthoring()
        {
            Undo.RecordObject(authoring, "Read Mesh Vertex Painter Authoring");
            authoring.NormalizeSurfaceIndices();
            EditorUtility.SetDirty(authoring);
            RefreshSurfaceIndexMapFromAuthoring();
            sourceMeshes.Clear();
            IReadOnlyList<MeshFilter> meshes = authoring.SourceMeshes;
            for (int i = 0; i < meshes.Count; i++)
            {
                sourceMeshes.Add(meshes[i]);
            }

            activeMeshIndex = authoring.ActiveMeshIndex;
            generatedPlane = authoring.GeneratedPlane;
            generatedPointTags = authoring.DefaultTags;
            planePicks.Clear();
            selectedByMesh.Clear();

            IReadOnlyList<FPMeshPaintedVertexRecord> records = authoring.PaintedVertices;
            for (int i = 0; i < records.Count; i++)
            {
                FPMeshPaintedVertexRecord record = records[i];
                if (!selectedByMesh.TryGetValue(record.SourceMeshIndex, out HashSet<int> selected))
                {
                    selected = new HashSet<int>();
                    selectedByMesh[record.SourceMeshIndex] = selected;
                }

                selected.Add(record.VertexIndex);
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void WriteToAuthoring()
        {
            if (authoring == null)
            {
                return;
            }

            Undo.RecordObject(authoring, "Write Painted Mesh Vertices");
            List<FPMeshPaintedVertexRecord> records = new();
            for (int meshIndex = 0; meshIndex < sourceMeshes.Count; meshIndex++)
            {
                MeshFilter meshFilter = sourceMeshes[meshIndex];
                if (meshFilter == null || meshFilter.sharedMesh == null || !selectedByMesh.TryGetValue(meshIndex, out HashSet<int> selected))
                {
                    continue;
                }

                Vector3[] vertices = meshFilter.sharedMesh.vertices;
                Vector3[] normals = meshFilter.sharedMesh.normals;

                foreach (int vertexIndex in selected)
                {
                    if (vertexIndex < 0 || vertexIndex >= vertices.Length)
                    {
                        continue;
                    }

                    FPMeshSurfaceEdgeEndpoint endpoint = FPMeshSurfaceEdgeEndpoint.Source(meshIndex, vertexIndex);
                    Vector3 normal = normals != null && vertexIndex < normals.Length ? normals[vertexIndex] : Vector3.up;
                    records.Add(new FPMeshPaintedVertexRecord
                    {
                        SurfaceIndex = GetOrAssignSurfaceIndex(endpoint),
                        SourceMeshIndex = meshIndex,
                        VertexIndex = vertexIndex,
                        LocalPosition = vertices[vertexIndex],
                        WorldPosition = meshFilter.transform.TransformPoint(vertices[vertexIndex]),
                        Normal = meshFilter.transform.TransformDirection(normal).normalized,
                        Tags = ResolveEndpointTags(endpoint)
                    });
                }
            }

            records.Sort((a, b) => a.SurfaceIndex.CompareTo(b.SurfaceIndex));
            authoring.SetSourceMeshes(sourceMeshes, activeMeshIndex);
            authoring.SetPaintedVertices(records);
            RefreshSurfaceIndexMapFromAuthoring();
            EditorUtility.SetDirty(authoring);
            SceneView.RepaintAll();
        }
    }

}
