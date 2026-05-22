namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Generates a simplified convex mesh collider asset from an existing mesh.
    /// </summary>
    public class FPSimpleConvexGeneratorWindow : EditorWindow
    {
        [SerializeField] private Object sourceObject;
        [SerializeField] private bool includeChildren = false;
        [SerializeField] private string outputMeshName = "FP_SimpleConvexCollider";
        [SerializeField] private string outputFolder = "Assets/_FPUtility";
        [SerializeField] private int decimatedPointTarget = 32;
        [SerializeField] private int maxSupportDirections = 48;
        [SerializeField] private float directionMergeAngle = 8f;
        [SerializeField] private float surfacePadding = 0.001f;
        [SerializeField] private bool supportOriginalVertices = true;
        [SerializeField] private FPMeshPreviewProjection cameraProjection = FPMeshPreviewProjection.Perspective;
        [SerializeField] private bool invertCameraOrbit = false;
        [SerializeField] private bool showVertices = false;
        [SerializeField] private bool showEdges = false;
        [SerializeField] private bool autoUpdatePreview = true;
        [SerializeField] private Transform targetParent;
        [SerializeField] private bool createSceneObject = true;
        [SerializeField] private bool addMeshCollider = true;

        private Mesh previewMesh;
        private Mesh lastSavedMesh;
        private PreviewRenderUtility previewUtility;
        private Material generatedPreviewMaterial;
        private Material sourcePreviewMaterial;
        private readonly List<SourcePreviewMesh> sourcePreviewMeshes = new List<SourcePreviewMesh>();
        private readonly List<Vector3> sourceBuildPoints = new List<Vector3>();
        private Quaternion previewRotation = Quaternion.Euler(22f, -35f, 0f);
        private float previewZoom = 1.45f;
        private int activeOrbitAxis = -1;
        private Vector2 parameterScrollPosition;
        private Vector2 messageScrollPosition;
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> errors = new List<string>();
        private FPSimpleConvexBuildReport lastReport;
        private bool previewDirty = true;

        private const float ParameterPanelWidth = 352f;
        private const float BottomDebugHeight = 112f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;

        private struct SourcePreviewMesh
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public Material[] Materials;

            public SourcePreviewMesh(Mesh mesh, Matrix4x4 matrix, Material[] materials)
            {
                Mesh = mesh;
                Matrix = matrix;
                Materials = materials;
            }
        }

        private struct SourceSummary
        {
            public bool HasMesh;
            public bool HasUnreadableMesh;
            public int MeshCount;
            public int VertexCount;
            public int TriangleCount;
            public Bounds Bounds;
        }

        [MenuItem("FuzzPhyte/Utility/Mesh/Convex Generator", priority = FP_UtilityData.MENU_UTILITY_MESH + 2)]
        public static void ShowWindow()
        {
            FPSimpleConvexGeneratorWindow window = GetWindow<FPSimpleConvexGeneratorWindow>("Convex Generator");
            window.minSize = new Vector2(760f, 520f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            EnsurePreviewUtility();
            SyncSelectionDefaults();
            previewDirty = true;
        }

        private void OnDisable()
        {
            CleanupPreviewMesh();

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }

            if (generatedPreviewMaterial != null)
            {
                DestroyImmediate(generatedPreviewMaterial);
                generatedPreviewMaterial = null;
            }

            if (sourcePreviewMaterial != null)
            {
                DestroyImmediate(sourcePreviewMaterial);
                sourcePreviewMaterial = null;
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Convex Generator", EditorStyles.boldLabel);
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
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, 760f);

            parameterScrollPosition = GUI.BeginScrollView(innerRect, parameterScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawParameterPanel();
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawParameterPanel()
        {
            EditorGUI.BeginChangeCheck();

            DrawSourceSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawCameraSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawConvexSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawOutputSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawActions();

            if (EditorGUI.EndChangeCheck())
            {
                previewDirty = true;
                if (autoUpdatePreview)
                {
                    RebuildPreview();
                }
            }
        }

        private void DrawSourceSettings()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            sourceObject = EditorGUILayout.ObjectField("Object / Mesh", sourceObject, typeof(Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyDefaultOutputName();
                previewDirty = true;
            }

            includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection"))
                {
                    SyncSelectedSource();
                    previewDirty = true;
                }

                if (GUILayout.Button("Use Parent From Selection"))
                {
                    SyncSelectionDefaults();
                }
            }

            SourceSummary summary = ResolveSource();
            if (!summary.HasMesh)
            {
                EditorGUILayout.HelpBox("Assign a GameObject, MeshFilter, SkinnedMeshRenderer, or Mesh asset. GameObjects use their renderer materials in the preview and bake mesh transforms into the generated collider space.", MessageType.None);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Meshes Found", summary.MeshCount.ToString());
            EditorGUILayout.LabelField("Source Vertices", summary.VertexCount.ToString());
            EditorGUILayout.LabelField("Source Triangles", summary.TriangleCount.ToString());
            EditorGUILayout.LabelField("Source Bounds", FormatVector3(summary.Bounds.size));

            if (summary.HasUnreadableMesh)
            {
                EditorGUILayout.HelpBox("One or more source meshes are not readable. Enable Read/Write on their import settings before generating a convex asset.", MessageType.Warning);
            }
        }

        private void DrawCameraSettings()
        {
            EditorGUILayout.LabelField("Camera Properties", EditorStyles.boldLabel);
            cameraProjection = FPMeshPreviewEditorUtility.DrawProjectionPopup(cameraProjection);
            invertCameraOrbit = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Invert Camera Orbit", invertCameraOrbit);
            showVertices = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Show Vertices", showVertices);
            showEdges = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Show Edges", showEdges);
        }

        private void DrawConvexSettings()
        {
            EditorGUILayout.LabelField("Convex Settings", EditorStyles.boldLabel);

            decimatedPointTarget = EditorGUILayout.IntSlider("Decimated Points", decimatedPointTarget, 8, 160);
            maxSupportDirections = EditorGUILayout.IntSlider("Surface Planes", maxSupportDirections, 12, 160);
            directionMergeAngle = EditorGUILayout.Slider("Merge Angle", directionMergeAngle, 1f, 30f);
            surfacePadding = EditorGUILayout.FloatField("Surface Padding", Mathf.Max(0f, surfacePadding));
            supportOriginalVertices = EditorGUILayout.Toggle("Contain Source Mesh", supportOriginalVertices);
            autoUpdatePreview = EditorGUILayout.Toggle("Auto Update Preview", autoUpdatePreview);

            EditorGUILayout.HelpBox(
                supportOriginalVertices
                    ? "Surface planes are fitted against the original vertices so the generated convex volume contains the source mesh."
                    : "Surface planes are fitted against the decimated points for a tighter but less conservative collider.",
                MessageType.None);
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            outputMeshName = EditorGUILayout.TextField("Mesh Name", outputMeshName);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            targetParent = (Transform)EditorGUILayout.ObjectField("Parent", targetParent, typeof(Transform), true);
            createSceneObject = EditorGUILayout.Toggle("Create Collider Child", createSceneObject);

            using (new EditorGUI.DisabledScope(!createSceneObject))
            {
                addMeshCollider = EditorGUILayout.Toggle("Add MeshCollider", addMeshCollider);
            }

            EditorGUILayout.HelpBox("Generated scene children are collider-only and use a convex MeshCollider automatically. If Parent is empty, the source object is used.", MessageType.None);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(sourceObject == null))
            {
                if (GUILayout.Button("Refresh Preview", GUILayout.Height(28f)))
                {
                    RebuildPreview();
                }

                Color originalColor = GUI.color;
                GUI.color = FP_Utility_Editor.OkayColor;

                using (new EditorGUI.DisabledScope(previewMesh == null))
                {
                    if (GUILayout.Button("Generate and Save Mesh", GUILayout.Height(32f)))
                    {
                        GenerateAndSave();
                    }
                }

                GUI.color = originalColor;
            }
        }

        private void DrawPreviewPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect previewRect = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);

            if (Event.current.type == EventType.Repaint && autoUpdatePreview && previewDirty && sourceObject != null)
            {
                RebuildPreview();
            }

            DrawMeshPreview(previewRect);
        }

        private void DrawMeshPreview(Rect rect)
        {
            HandlePreviewInput(rect);

            if (previewMesh == null)
            {
                GUI.Label(rect, sourceObject == null ? "Assign a source object to preview." : "Click Refresh Preview to build the convex mesh.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewMaterials();

            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
            previewUtility.camera.fieldOfView = FPMeshPreviewEditorUtility.DefaultFieldOfView;
            previewUtility.lights[0].intensity = 1.1f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            previewUtility.lights[1].intensity = 0.55f;

            Bounds bounds = CalculatePreviewBounds();
            float distance = FPMeshPreviewEditorUtility.CalculateFitDistance(bounds, rect) * Mathf.Max(0.5f, previewZoom);
            Vector3 forward = previewRotation * Vector3.forward;
            previewUtility.camera.transform.position = bounds.center - (forward * distance);
            previewUtility.camera.transform.rotation = previewRotation;
            previewUtility.camera.orthographic = cameraProjection == FPMeshPreviewProjection.Orthographic;
            if (previewUtility.camera.orthographic)
            {
                previewUtility.camera.orthographicSize = FPMeshPreviewEditorUtility.CalculateOrthographicSize(bounds, rect, previewRotation) * Mathf.Max(0.5f, previewZoom);
            }

            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            previewUtility.camera.nearClipPlane = Mathf.Max(0.001f, distance - (radius * 2.4f));
            previewUtility.camera.farClipPlane = distance + (radius * 3.4f);

            DrawSourcePreviewMeshes();

            DrawPreviewMesh(previewMesh, generatedPreviewMaterial);
            previewUtility.camera.Render();
            Texture result = previewUtility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);

            DrawMeshOverlays(rect);
            DrawPreviewOverlay(rect);
            FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(rect, previewUtility.camera, cameraProjection);
            DrawOrbitGizmo(rect);
        }

        private void DrawMeshOverlays(Rect rect)
        {
            if ((!showVertices && !showEdges) || previewUtility == null || previewUtility.camera == null || previewMesh == null)
            {
                return;
            }

            if (showEdges)
            {
                FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, previewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.EdgeOverlayColor, 1.5f);
            }

            if (showVertices)
            {
                FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, previewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.VertexOverlayColor, 2.5f);
            }
        }

        private Bounds CalculatePreviewBounds()
        {
            Bounds bounds = previewMesh != null
                ? previewMesh.bounds
                : new Bounds(Vector3.zero, Vector3.one);

            bool hasBounds = previewMesh != null;
            for (int i = 0; i < sourceBuildPoints.Count; i++)
            {
                if (!hasBounds)
                {
                    bounds = new Bounds(sourceBuildPoints[i], Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(sourceBuildPoints[i]);
                }
            }

            if (bounds.size.sqrMagnitude <= 0.0000001f)
            {
                bounds.Expand(0.1f);
            }

            return bounds;
        }

        private void DrawPreviewMesh(Mesh mesh, Material material)
        {
            if (mesh == null || material == null)
            {
                return;
            }

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int i = 0; i < subMeshCount; i++)
            {
                previewUtility.DrawMesh(mesh, Matrix4x4.identity, material, i);
            }
        }

        private void DrawSourcePreviewMeshes()
        {
            for (int s = 0; s < sourcePreviewMeshes.Count; s++)
            {
                SourcePreviewMesh source = sourcePreviewMeshes[s];
                if (source.Mesh == null)
                {
                    continue;
                }

                int subMeshCount = Mathf.Max(1, source.Mesh.subMeshCount);
                for (int i = 0; i < subMeshCount; i++)
                {
                    Material material = ResolvePreviewSourceMaterial(source.Materials, i);
                    previewUtility.DrawMesh(source.Mesh, source.Matrix, material, i);
                }
            }
        }

        private Material ResolvePreviewSourceMaterial(Material[] materials, int subMeshIndex)
        {
            if (materials != null && materials.Length > 0)
            {
                Material material = materials[Mathf.Clamp(subMeshIndex, 0, materials.Length - 1)];
                if (material != null)
                {
                    return material;
                }
            }

            return sourcePreviewMaterial;
        }

        private void DrawPreviewOverlay(Rect rect)
        {
            Rect overlayRect = new Rect(rect.x + 8f, rect.y + 8f, 232f, 108f);
            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(overlayRect.x + 6f, overlayRect.y + 5f, overlayRect.width - 12f, 18f);
            GUI.Label(lineRect, $"Preview Vertices: {previewMesh.vertexCount}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Triangles: {lastReport.TriangleCount}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Vertex Ratio: {CalculatePreviewVertexRatio():0.##}%", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Support Points: {lastReport.DecimatedPointCount}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Planes Used: {lastReport.SupportDirectionCount}/{maxSupportDirections}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Zoom: {previewZoom:0.##}x", EditorStyles.miniLabel);
        }

        private float CalculatePreviewVertexRatio()
        {
            if (lastReport.SourcePointCount <= 0 || previewMesh == null)
            {
                return 0f;
            }

            return (previewMesh.vertexCount / (float)lastReport.SourcePointCount) * 100f;
        }

        private void DrawOrbitGizmo(Rect previewRect)
        {
            Rect gizmoRect = GetOrbitGizmoRect(previewRect);
            GUI.Box(gizmoRect, GUIContent.none, EditorStyles.helpBox);

            GUI.Label(new Rect(gizmoRect.x + 6f, gizmoRect.y + 4f, gizmoRect.width - 12f, 16f), "Orbit", EditorStyles.centeredGreyMiniLabel);

            DrawAxisHandle(GetOrbitAxisRect(gizmoRect, 0), "X", new Color(0.9f, 0.22f, 0.18f, 1f));
            DrawAxisHandle(GetOrbitAxisRect(gizmoRect, 1), "Y", new Color(0.3f, 0.78f, 0.28f, 1f));
            DrawAxisHandle(GetOrbitAxisRect(gizmoRect, 2), "Z", new Color(0.22f, 0.48f, 0.95f, 1f));

            Rect buttonsRect = new Rect(gizmoRect.x + 6f, gizmoRect.y + 88f, gizmoRect.width - 12f, 52f);
            DrawSnapButtons(buttonsRect);
        }

        private static void DrawAxisHandle(Rect rect, string label, Color color)
        {
            EditorGUI.DrawRect(rect, color * new Color(1f, 1f, 1f, 0.78f));
            GUI.Label(rect, label, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSnapButtons(Rect rect)
        {
            const float gap = 3f;
            float width = (rect.width - (gap * 2f)) / 3f;
            float height = 22f;

            if (GUI.Button(new Rect(rect.x, rect.y, width, height), "+X", EditorStyles.miniButtonLeft))
            {
                SetPreviewView(Vector3.right, Vector3.up);
            }

            if (GUI.Button(new Rect(rect.x + width + gap, rect.y, width, height), "+Y", EditorStyles.miniButtonMid))
            {
                SetPreviewView(Vector3.up, Vector3.back);
            }

            if (GUI.Button(new Rect(rect.x + ((width + gap) * 2f), rect.y, width, height), "+Z", EditorStyles.miniButtonRight))
            {
                SetPreviewView(Vector3.forward, Vector3.up);
            }

            float y = rect.y + height + 4f;
            if (GUI.Button(new Rect(rect.x, y, width, height), "-X", EditorStyles.miniButtonLeft))
            {
                SetPreviewView(Vector3.left, Vector3.up);
            }

            if (GUI.Button(new Rect(rect.x + width + gap, y, width, height), "-Y", EditorStyles.miniButtonMid))
            {
                SetPreviewView(Vector3.down, Vector3.forward);
            }

            if (GUI.Button(new Rect(rect.x + ((width + gap) * 2f), y, width, height), "-Z", EditorStyles.miniButtonRight))
            {
                SetPreviewView(Vector3.back, Vector3.up);
            }
        }

        private void SetPreviewView(Vector3 viewDirection, Vector3 up)
        {
            previewRotation = Quaternion.LookRotation(viewDirection, up);
            Repaint();
        }

        private static Rect GetOrbitGizmoRect(Rect previewRect)
        {
            return new Rect(previewRect.xMax - 128f, previewRect.y + 104f, 120f, 148f);
        }

        private static Rect GetOrbitAxisRect(Rect gizmoRect, int axis)
        {
            return new Rect(gizmoRect.x + 8f, gizmoRect.y + 24f + (axis * 20f), gizmoRect.width - 16f, 16f);
        }

        private static int GetOrbitAxisAtPosition(Rect previewRect, Vector2 mousePosition)
        {
            Rect gizmoRect = GetOrbitGizmoRect(previewRect);
            for (int i = 0; i < 3; i++)
            {
                if (GetOrbitAxisRect(gizmoRect, i).Contains(mousePosition))
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawDebugPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, Mathf.Max(innerRect.height, 36f + ((errors.Count + warnings.Count + 1) * 38f)));

            messageScrollPosition = GUI.BeginScrollView(innerRect, messageScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawMessages();
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawMessages()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(previewMesh == null ? "No Preview" : "Preview Ready", GUILayout.Width(92f));
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("No convex generator warnings or errors.", MessageType.None);
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

        private void HandlePreviewInput(Rect rect)
        {
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.ScrollWheel)
            {
                float zoomFactor = Mathf.Exp(current.delta.y * 0.08f);
                previewZoom = Mathf.Clamp(previewZoom * zoomFactor, 0.5f, 12f);
                current.Use();
                Repaint();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                int axis = GetOrbitAxisAtPosition(rect, current.mousePosition);
                if (axis >= 0)
                {
                    activeOrbitAxis = axis;
                    current.Use();
                    return;
                }
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.Ignore) && activeOrbitAxis >= 0)
            {
                activeOrbitAxis = -1;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && current.button == 0 && activeOrbitAxis >= 0)
            {
                float degrees = (current.delta.x + current.delta.y) * 0.5f;
                Vector3 axis = activeOrbitAxis == 0
                    ? Vector3.right
                    : activeOrbitAxis == 1
                        ? Vector3.up
                        : Vector3.forward;
                previewRotation = Quaternion.AngleAxis(degrees, axis) * previewRotation;
                current.Use();
                Repaint();
                return;
            }

            if (GetOrbitGizmoRect(rect).Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.MouseDrag && current.button == 0)
            {
                previewRotation = FPMeshPreviewEditorUtility.ApplyUnityStyleOrbit(previewRotation, current.delta, invertCameraOrbit);
                current.Use();
                Repaint();
            }
        }

        private void RebuildPreview()
        {
            warnings.Clear();
            errors.Clear();
            previewDirty = false;
            CleanupPreviewMesh();

            FPSimpleConvexBuildSettings settings = new FPSimpleConvexBuildSettings
            {
                MeshName = string.IsNullOrWhiteSpace(outputMeshName) ? "FP_SimpleConvexCollider" : outputMeshName,
                DecimatedPointTarget = decimatedPointTarget,
                MaxSupportDirections = maxSupportDirections,
                DirectionMergeAngle = directionMergeAngle,
                SurfacePadding = surfacePadding,
                SupportOriginalVertices = supportOriginalVertices
            };

            SourceSummary summary = ResolveSource();
            if (!summary.HasMesh)
            {
                errors.Add("Assign a source object with at least one mesh before building a convex preview.");
                Repaint();
                return;
            }

            if (summary.HasUnreadableMesh)
            {
                errors.Add("One or more source meshes are not readable. Enable Read/Write on their import settings and rebuild the preview.");
                Repaint();
                return;
            }

            previewMesh = FPSimpleConvexMeshBuilder.Build(sourceBuildPoints, summary.Bounds, settings, out lastReport);
            warnings.AddRange(lastReport.Warnings);
            errors.AddRange(lastReport.Errors);
            Repaint();
        }

        private void GenerateAndSave()
        {
            if (previewMesh == null)
            {
                RebuildPreview();
            }

            if (previewMesh == null)
            {
                return;
            }

            Mesh meshToSave = Object.Instantiate(previewMesh);
            meshToSave.name = string.IsNullOrWhiteSpace(outputMeshName) ? "FP_SimpleConvexCollider" : outputMeshName.Trim();

            Mesh savedMesh = SaveMeshAsset(meshToSave, outputFolder, meshToSave.name, out string saveMessage);
            if (savedMesh == null)
            {
                errors.Add(string.IsNullOrWhiteSpace(saveMessage) ? "Mesh generation succeeded, but the mesh asset could not be saved." : saveMessage);
                if (!EditorUtility.IsPersistent(meshToSave))
                {
                    DestroyImmediate(meshToSave);
                }

                Repaint();
                return;
            }

            lastSavedMesh = savedMesh;
            if (!string.IsNullOrWhiteSpace(saveMessage))
            {
                warnings.Add(saveMessage);
            }

            if (createSceneObject)
            {
                CreateSceneObject(savedMesh);
            }

            Repaint();
        }

        private void CreateSceneObject(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            GameObject go = new GameObject(mesh.name);
            Undo.RegisterCreatedObjectUndo(go, "Create Simple Convex Mesh");

            Transform parent = ResolveColliderParent();
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
                go.transform.SetParent(parent, false);
            }

            if (addMeshCollider)
            {
                MeshCollider meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                meshCollider.convex = true;
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            warnings.Add($"Scene object created: {go.name}");
        }

        private Transform ResolveColliderParent()
        {
            if (targetParent != null)
            {
                return targetParent;
            }

            if (sourceObject is GameObject gameObject)
            {
                return gameObject.transform;
            }

            if (sourceObject is Component component)
            {
                return component.transform;
            }

            return null;
        }

        private void SyncSelectionDefaults()
        {
            if (Selection.activeTransform != null)
            {
                targetParent = Selection.activeTransform;
            }

            SyncSelectedSource();
        }

        private void SyncSelectedSource()
        {
            if (Selection.activeObject is Mesh || Selection.activeObject is GameObject)
            {
                sourceObject = Selection.activeObject;
                ApplyDefaultOutputName();
                return;
            }

            if (Selection.activeObject is Component component)
            {
                sourceObject = component;
                ApplyDefaultOutputName();
                return;
            }

            if (Selection.activeGameObject != null)
            {
                sourceObject = Selection.activeGameObject;
                ApplyDefaultOutputName();
            }
        }

        private void ApplyDefaultOutputName()
        {
            if (sourceObject == null)
            {
                return;
            }

            string safeName = string.IsNullOrWhiteSpace(sourceObject.name) ? "Mesh" : sourceObject.name;
            outputMeshName = $"{safeName}_SimpleConvex";
        }

        private SourceSummary ResolveSource()
        {
            sourcePreviewMeshes.Clear();
            sourceBuildPoints.Clear();

            var summary = new SourceSummary();
            if (sourceObject == null)
            {
                return summary;
            }

            if (sourceObject is Mesh mesh)
            {
                AddSourceMesh(mesh, Matrix4x4.identity, null, ref summary);
            }
            else if (sourceObject is GameObject gameObject)
            {
                AddGameObjectSources(gameObject, includeChildren, ref summary);
            }
            else if (sourceObject is MeshFilter meshFilter)
            {
                if (includeChildren)
                {
                    AddGameObjectSources(meshFilter.gameObject, true, ref summary);
                }
                else
                {
                    Material[] materials = ResolveRendererMaterials(meshFilter.GetComponent<MeshRenderer>());
                    AddSourceMesh(meshFilter.sharedMesh, Matrix4x4.identity, materials, ref summary);
                }
            }
            else if (sourceObject is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                if (includeChildren)
                {
                    AddGameObjectSources(skinnedMeshRenderer.gameObject, true, ref summary);
                }
                else
                {
                    AddSourceMesh(skinnedMeshRenderer.sharedMesh, Matrix4x4.identity, skinnedMeshRenderer.sharedMaterials, ref summary);
                }
            }
            else if (sourceObject is Component component)
            {
                AddGameObjectSources(component.gameObject, includeChildren, ref summary);
            }

            return summary;
        }

        private void AddGameObjectSources(GameObject gameObject, bool includeChildMeshes, ref SourceSummary summary)
        {
            if (gameObject == null)
            {
                return;
            }

            Matrix4x4 rootToLocal = gameObject.transform.worldToLocalMatrix;

            if (!includeChildMeshes)
            {
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    Material[] materials = ResolveRendererMaterials(meshFilter.GetComponent<MeshRenderer>());
                    AddSourceMesh(meshFilter.sharedMesh, rootToLocal * meshFilter.transform.localToWorldMatrix, materials, ref summary);
                }

                SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    AddSourceMesh(skinnedMeshRenderer.sharedMesh, rootToLocal * skinnedMeshRenderer.transform.localToWorldMatrix, skinnedMeshRenderer.sharedMaterials, ref summary);
                }

                return;
            }

            MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                Material[] materials = ResolveRendererMaterials(meshFilter.GetComponent<MeshRenderer>());
                AddSourceMesh(meshFilter.sharedMesh, rootToLocal * meshFilter.transform.localToWorldMatrix, materials, ref summary);
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedMeshRenderers[i];
                AddSourceMesh(renderer.sharedMesh, rootToLocal * renderer.transform.localToWorldMatrix, renderer.sharedMaterials, ref summary);
            }
        }

        private void AddSourceMesh(Mesh mesh, Matrix4x4 matrix, Material[] materials, ref SourceSummary summary)
        {
            if (mesh == null)
            {
                return;
            }

            sourcePreviewMeshes.Add(new SourcePreviewMesh(mesh, matrix, materials));
            summary.HasMesh = true;
            summary.MeshCount++;
            summary.VertexCount += mesh.vertexCount;
            summary.TriangleCount += EstimateTriangleCount(mesh);

            if (!mesh.isReadable)
            {
                summary.HasUnreadableMesh = true;
                return;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(vertices[i]);
                if (sourceBuildPoints.Count == 0)
                {
                    summary.Bounds = new Bounds(point, Vector3.zero);
                }
                else
                {
                    summary.Bounds.Encapsulate(point);
                }

                sourceBuildPoints.Add(point);
            }
        }

        private static Material[] ResolveRendererMaterials(Renderer renderer)
        {
            return renderer == null ? null : renderer.sharedMaterials;
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
        }

        private void EnsurePreviewMaterials()
        {
            if (generatedPreviewMaterial == null)
            {
                Shader shader = FindPreviewShader();
                generatedPreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = FPMeshPreviewEditorUtility.PreviewMeshColor
                };
                ConfigureTransparentPreviewMaterial(generatedPreviewMaterial, 0.48f);
            }

            if (sourcePreviewMaterial == null)
            {
                Shader shader = FindPreviewShader();
                sourcePreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(0.86f, 0.86f, 0.86f, 1f)
                };
            }
        }

        private static Shader FindPreviewShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Diffuse")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static void ConfigureTransparentPreviewMaterial(Material material, float alpha)
        {
            if (material == null)
            {
                return;
            }

            Color color = material.color;
            color.a = Mathf.Clamp01(alpha);
            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void CleanupPreviewMesh()
        {
            if (previewMesh != null && !EditorUtility.IsPersistent(previewMesh) && previewMesh != lastSavedMesh)
            {
                DestroyImmediate(previewMesh);
            }

            previewMesh = null;
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string folder, string meshName, out string message)
        {
            message = string.Empty;
            if (mesh == null)
            {
                message = "No mesh was generated to save.";
                return null;
            }

            string safeFolder = string.IsNullOrWhiteSpace(folder) ? "Assets/_FPUtility" : folder.Trim().Replace("\\", "/");
            if (!safeFolder.StartsWith("Assets"))
            {
                safeFolder = "Assets/" + safeFolder.TrimStart('/');
            }

            EnsureAssetFolder(safeFolder);

            string safeName = string.IsNullOrWhiteSpace(meshName) ? "FP_SimpleConvexCollider" : meshName.Trim();
            mesh.name = safeName;
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(safeFolder, safeName + ".asset").Replace("\\", "/"));

            try
            {
                AssetDatabase.CreateAsset(mesh, path);
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                message = $"Mesh asset could not be saved: {ex.Message}";
                return null;
            }

            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (savedMesh == null)
            {
                message = $"Mesh asset could not be loaded after save: {path}";
                return null;
            }

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = savedMesh;
            message = $"Mesh saved to {path}";
            return savedMesh;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static string FormatVector3(Vector3 value)
        {
            return $"{value.x:0.###}, {value.y:0.###}, {value.z:0.###}";
        }

        private static int EstimateTriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            int triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                triangles += (int)(mesh.GetIndexCount(i) / 3);
            }

            return triangles;
        }
    }

    internal struct FPSimpleConvexBuildSettings
    {
        public string MeshName;
        public int DecimatedPointTarget;
        public int MaxSupportDirections;
        public float DirectionMergeAngle;
        public float SurfacePadding;
        public bool SupportOriginalVertices;

        public FPSimpleConvexBuildSettings Sanitized()
        {
            return new FPSimpleConvexBuildSettings
            {
                MeshName = string.IsNullOrWhiteSpace(MeshName) ? "FP_SimpleConvexCollider" : MeshName.Trim(),
                DecimatedPointTarget = Mathf.Clamp(DecimatedPointTarget, 8, 512),
                MaxSupportDirections = Mathf.Clamp(MaxSupportDirections, 12, 256),
                DirectionMergeAngle = Mathf.Clamp(DirectionMergeAngle, 0.5f, 45f),
                SurfacePadding = Mathf.Max(0f, SurfacePadding),
                SupportOriginalVertices = SupportOriginalVertices
            };
        }
    }

    internal struct FPSimpleConvexBuildReport
    {
        public int SourcePointCount;
        public int DecimatedPointCount;
        public int SupportDirectionCount;
        public int FaceCount;
        public int TriangleCount;
        public readonly List<string> Warnings;
        public readonly List<string> Errors;

        public FPSimpleConvexBuildReport(int sourcePointCount)
        {
            SourcePointCount = sourcePointCount;
            DecimatedPointCount = 0;
            SupportDirectionCount = 0;
            FaceCount = 0;
            TriangleCount = 0;
            Warnings = new List<string>();
            Errors = new List<string>();
        }
    }

    internal static class FPSimpleConvexMeshBuilder
    {
        private const float Epsilon = 0.00001f;

        private sealed class Face
        {
            public readonly List<Vector3> Points;

            public Face(IEnumerable<Vector3> points)
            {
                Points = new List<Vector3>(points);
            }
        }

        private struct PlaneData
        {
            public Vector3 Normal;
            public float Distance;

            public PlaneData(Vector3 normal, float distance)
            {
                Normal = normal;
                Distance = distance;
            }
        }

        public static Mesh Build(Mesh sourceMesh, FPSimpleConvexBuildSettings settings, out FPSimpleConvexBuildReport report)
        {
            if (sourceMesh == null)
            {
                report = new FPSimpleConvexBuildReport(0);
                report.Errors.Add("Assign a source mesh before building a convex preview.");
                return null;
            }

            if (!sourceMesh.isReadable)
            {
                report = new FPSimpleConvexBuildReport(sourceMesh.vertexCount);
                report.Errors.Add("The source mesh is not readable. Enable Read/Write on the mesh import settings and rebuild the preview.");
                return null;
            }

            return Build(sourceMesh.vertices, sourceMesh.bounds, settings, out report);
        }

        public static Mesh Build(IReadOnlyList<Vector3> sourceVertices, Bounds sourceBounds, FPSimpleConvexBuildSettings settings, out FPSimpleConvexBuildReport report)
        {
            settings = settings.Sanitized();
            report = new FPSimpleConvexBuildReport(sourceVertices == null ? 0 : sourceVertices.Count);

            if (sourceVertices == null || sourceVertices.Count == 0)
            {
                report.Errors.Add("No readable source mesh vertices were found.");
                return null;
            }

            List<Vector3> sourcePoints = RemoveDuplicatePoints(sourceVertices, Mathf.Max(Epsilon, sourceBounds.size.magnitude * 0.00001f));
            report.SourcePointCount = sourcePoints.Count;

            if (sourcePoints.Count < 4)
            {
                report.Warnings.Add("The source mesh has fewer than four unique points, so a small bounds-based convex mesh was generated.");
                Mesh fallbackMesh = BuildBoundsFallback(sourceBounds, settings.MeshName, settings.SurfacePadding);
                report.TriangleCount = fallbackMesh == null ? 0 : fallbackMesh.triangles.Length / 3;
                return fallbackMesh;
            }

            sourceBounds = BuildBounds(sourcePoints);
            if (IsDegenerate(sourceBounds))
            {
                report.Warnings.Add("Source bounds are nearly flat, so a small bounds-based convex mesh was generated.");
                Mesh fallbackMesh = BuildBoundsFallback(sourceBounds, settings.MeshName, Mathf.Max(settings.SurfacePadding, 0.005f));
                report.TriangleCount = fallbackMesh == null ? 0 : fallbackMesh.triangles.Length / 3;
                return fallbackMesh;
            }

            List<Vector3> decimatedPoints = DecimatePoints(sourcePoints, sourceBounds, settings.DecimatedPointTarget);
            report.DecimatedPointCount = decimatedPoints.Count;

            List<Vector3> directions = BuildSupportDirections(decimatedPoints, sourceBounds.center, settings.MaxSupportDirections, settings.DirectionMergeAngle);
            report.SupportDirectionCount = directions.Count;

            IReadOnlyList<Vector3> supportPoints = settings.SupportOriginalVertices ? sourcePoints : decimatedPoints;
            List<PlaneData> planes = BuildSupportPlanes(directions, supportPoints, settings.SurfacePadding);
            List<Face> faces = BuildClippedPolyhedron(sourceBounds, planes, settings.SurfacePadding);
            Mesh mesh = BuildMeshFromFaces(faces, settings.MeshName, out int faceCount);
            report.FaceCount = faceCount;

            if (mesh == null || mesh.vertexCount == 0)
            {
                report.Warnings.Add("Convex clipping produced an empty mesh, so a bounds fallback was generated.");
                mesh = BuildBoundsFallback(sourceBounds, settings.MeshName, settings.SurfacePadding);
                report.TriangleCount = mesh == null ? 0 : mesh.triangles.Length / 3;
                return mesh;
            }

            report.TriangleCount = mesh.triangles.Length / 3;
            if (report.TriangleCount > 255)
            {
                report.Warnings.Add("The generated mesh is over 255 triangles. Reduce Surface Planes for a safer convex MeshCollider.");
            }

            float boundsRatio = sourceBounds.size.sqrMagnitude <= Epsilon
                ? 1f
                : mesh.bounds.size.magnitude / sourceBounds.size.magnitude;
            if (boundsRatio > 1.35f)
            {
                report.Warnings.Add("Generated bounds are much larger than the source bounds. Lower Merge Angle or increase Surface Planes for a tighter collider.");
            }

            return mesh;
        }

        private static List<Vector3> DecimatePoints(IReadOnlyList<Vector3> points, Bounds bounds, int targetCount)
        {
            var result = new List<Vector3>();
            if (points.Count <= targetCount)
            {
                result.AddRange(points);
                return result;
            }

            int gridResolution = Mathf.Clamp(Mathf.CeilToInt(Mathf.Pow(targetCount, 1f / 3f)) + 1, 2, 32);
            Vector3 size = bounds.size;
            Vector3 min = bounds.min;
            var cells = new Dictionary<Vector3Int, Vector3>();
            var cellScores = new Dictionary<Vector3Int, float>();
            Vector3 center = bounds.center;

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                Vector3 normalized = new Vector3(
                    size.x <= Epsilon ? 0f : Mathf.Clamp01((point.x - min.x) / size.x),
                    size.y <= Epsilon ? 0f : Mathf.Clamp01((point.y - min.y) / size.y),
                    size.z <= Epsilon ? 0f : Mathf.Clamp01((point.z - min.z) / size.z));

                Vector3Int key = new Vector3Int(
                    Mathf.Clamp(Mathf.FloorToInt(normalized.x * gridResolution), 0, gridResolution - 1),
                    Mathf.Clamp(Mathf.FloorToInt(normalized.y * gridResolution), 0, gridResolution - 1),
                    Mathf.Clamp(Mathf.FloorToInt(normalized.z * gridResolution), 0, gridResolution - 1));

                float score = (point - center).sqrMagnitude;
                if (!cellScores.TryGetValue(key, out float currentScore) || score > currentScore)
                {
                    cells[key] = point;
                    cellScores[key] = score;
                }
            }

            result.AddRange(cells.Values);
            AddAxisExtremes(points, result);
            RemoveDuplicateInPlace(result, Mathf.Max(Epsilon, bounds.size.magnitude * 0.00001f));
            result.Sort((a, b) => (b - center).sqrMagnitude.CompareTo((a - center).sqrMagnitude));

            if (result.Count > targetCount)
            {
                result.RemoveRange(targetCount, result.Count - targetCount);
            }

            return result;
        }

        private static void AddAxisExtremes(IReadOnlyList<Vector3> points, List<Vector3> result)
        {
            Vector3[] directions =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
            };

            for (int i = 0; i < directions.Length; i++)
            {
                result.Add(FindSupportPoint(points, directions[i]));
            }
        }

        private static List<Vector3> BuildSupportDirections(IReadOnlyList<Vector3> decimatedPoints, Vector3 center, int maxDirections, float mergeAngle)
        {
            var directions = new List<Vector3>();
            float mergeDot = Mathf.Cos(mergeAngle * Mathf.Deg2Rad);

            AddDirection(directions, Vector3.right, mergeDot);
            AddDirection(directions, Vector3.left, mergeDot);
            AddDirection(directions, Vector3.up, mergeDot);
            AddDirection(directions, Vector3.down, mergeDot);
            AddDirection(directions, Vector3.forward, mergeDot);
            AddDirection(directions, Vector3.back, mergeDot);

            AddHullNormalDirections(directions, decimatedPoints, center, maxDirections, mergeDot);

            for (int i = 0; i < decimatedPoints.Count && directions.Count < maxDirections; i++)
            {
                Vector3 direction = decimatedPoints[i] - center;
                if (direction.sqrMagnitude <= Epsilon)
                {
                    continue;
                }

                AddDirection(directions, direction.normalized, mergeDot);
            }

            return directions;
        }

        private static void AddHullNormalDirections(
            List<Vector3> directions,
            IReadOnlyList<Vector3> points,
            Vector3 center,
            int maxDirections,
            float mergeDot)
        {
            int pointCount = Mathf.Min(points.Count, 64);
            float tolerance = Mathf.Max(Epsilon * 10f, EstimatePointCloudSize(points) * 0.0001f);

            for (int a = 0; a < pointCount && directions.Count < maxDirections; a++)
            {
                for (int b = a + 1; b < pointCount && directions.Count < maxDirections; b++)
                {
                    for (int c = b + 1; c < pointCount && directions.Count < maxDirections; c++)
                    {
                        Vector3 normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                        if (normal.sqrMagnitude <= Epsilon)
                        {
                            continue;
                        }

                        normal.Normalize();
                        bool hasPositive = false;
                        bool hasNegative = false;
                        for (int p = 0; p < pointCount; p++)
                        {
                            if (p == a || p == b || p == c)
                            {
                                continue;
                            }

                            float distance = Vector3.Dot(normal, points[p] - points[a]);
                            if (distance > tolerance)
                            {
                                hasPositive = true;
                            }
                            else if (distance < -tolerance)
                            {
                                hasNegative = true;
                            }

                            if (hasPositive && hasNegative)
                            {
                                break;
                            }
                        }

                        if (hasPositive && hasNegative)
                        {
                            continue;
                        }

                        Vector3 triangleCenter = (points[a] + points[b] + points[c]) / 3f;
                        if (Vector3.Dot(normal, triangleCenter - center) < 0f)
                        {
                            normal = -normal;
                        }

                        AddDirection(directions, normal, mergeDot);
                    }
                }
            }
        }

        private static void AddDirection(List<Vector3> directions, Vector3 direction, float mergeDot)
        {
            if (direction.sqrMagnitude <= Epsilon)
            {
                return;
            }

            direction.Normalize();
            for (int i = 0; i < directions.Count; i++)
            {
                if (Vector3.Dot(directions[i], direction) >= mergeDot)
                {
                    return;
                }
            }

            directions.Add(direction);
        }

        private static List<PlaneData> BuildSupportPlanes(IReadOnlyList<Vector3> directions, IReadOnlyList<Vector3> points, float padding)
        {
            var planes = new List<PlaneData>(directions.Count);
            for (int i = 0; i < directions.Count; i++)
            {
                Vector3 normal = directions[i].normalized;
                float maxDistance = float.NegativeInfinity;

                for (int p = 0; p < points.Count; p++)
                {
                    maxDistance = Mathf.Max(maxDistance, Vector3.Dot(normal, points[p]));
                }

                planes.Add(new PlaneData(normal, maxDistance + padding));
            }

            return planes;
        }

        private static List<Face> BuildClippedPolyhedron(Bounds sourceBounds, IReadOnlyList<PlaneData> planes, float padding)
        {
            float expansion = Mathf.Max(padding * 2f, sourceBounds.size.magnitude * 0.001f, 0.001f);
            Bounds workingBounds = sourceBounds;
            workingBounds.Expand(expansion * 2f);

            List<Face> faces = CreateBoxFaces(workingBounds);
            for (int i = 0; i < planes.Count; i++)
            {
                ClipFaces(faces, planes[i]);
                if (faces.Count == 0)
                {
                    break;
                }
            }

            return faces;
        }

        private static void ClipFaces(List<Face> faces, PlaneData plane)
        {
            var nextFaces = new List<Face>();
            var capPoints = new List<Vector3>();

            for (int i = 0; i < faces.Count; i++)
            {
                List<Vector3> clipped = ClipPolygon(faces[i].Points, plane, capPoints);
                if (clipped.Count >= 3 && PolygonArea(clipped) > Epsilon)
                {
                    nextFaces.Add(new Face(clipped));
                }
            }

            RemoveDuplicateInPlace(capPoints, Epsilon * 10f);
            if (capPoints.Count >= 3)
            {
                List<Vector3> cap = SortCapPoints(capPoints, plane.Normal);
                if (cap.Count >= 3 && PolygonArea(cap) > Epsilon)
                {
                    nextFaces.Add(new Face(cap));
                }
            }

            faces.Clear();
            faces.AddRange(nextFaces);
        }

        private static List<Vector3> ClipPolygon(IReadOnlyList<Vector3> polygon, PlaneData plane, List<Vector3> capPoints)
        {
            var result = new List<Vector3>();
            if (polygon.Count == 0)
            {
                return result;
            }

            Vector3 previous = polygon[polygon.Count - 1];
            float previousDistance = SignedDistance(previous, plane);
            bool previousInside = previousDistance <= Epsilon;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 current = polygon[i];
                float currentDistance = SignedDistance(current, plane);
                bool currentInside = currentDistance <= Epsilon;

                if (previousInside && currentInside)
                {
                    result.Add(current);
                }
                else if (previousInside && !currentInside)
                {
                    Vector3 intersection = IntersectSegment(previous, current, previousDistance, currentDistance);
                    result.Add(intersection);
                    capPoints.Add(intersection);
                }
                else if (!previousInside && currentInside)
                {
                    Vector3 intersection = IntersectSegment(previous, current, previousDistance, currentDistance);
                    result.Add(intersection);
                    result.Add(current);
                    capPoints.Add(intersection);
                }

                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }

            RemoveDuplicateInPlace(result, Epsilon * 10f);
            return result;
        }

        private static float SignedDistance(Vector3 point, PlaneData plane)
        {
            return Vector3.Dot(plane.Normal, point) - plane.Distance;
        }

        private static Vector3 IntersectSegment(Vector3 a, Vector3 b, float distanceA, float distanceB)
        {
            float denominator = distanceA - distanceB;
            if (Mathf.Abs(denominator) <= Epsilon)
            {
                return a;
            }

            float t = Mathf.Clamp01(distanceA / denominator);
            return Vector3.LerpUnclamped(a, b, t);
        }

        private static List<Vector3> SortCapPoints(IReadOnlyList<Vector3> points, Vector3 normal)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                center += points[i];
            }

            center /= points.Count;

            Vector3 axisA = Vector3.Cross(normal, Vector3.up);
            if (axisA.sqrMagnitude <= Epsilon)
            {
                axisA = Vector3.Cross(normal, Vector3.right);
            }

            axisA.Normalize();
            Vector3 axisB = Vector3.Cross(normal, axisA).normalized;

            var sorted = new List<Vector3>(points);
            sorted.Sort((a, b) =>
            {
                Vector3 da = a - center;
                Vector3 db = b - center;
                float angleA = Mathf.Atan2(Vector3.Dot(axisB, da), Vector3.Dot(axisA, da));
                float angleB = Mathf.Atan2(Vector3.Dot(axisB, db), Vector3.Dot(axisA, db));
                return angleA.CompareTo(angleB);
            });

            return sorted;
        }

        private static Mesh BuildMeshFromFaces(IReadOnlyList<Face> faces, string meshName, out int faceCount)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            faceCount = 0;

            Vector3 center = CalculateFaceCenter(faces);

            for (int f = 0; f < faces.Count; f++)
            {
                List<Vector3> facePoints = faces[f].Points;
                if (facePoints.Count < 3 || PolygonArea(facePoints) <= Epsilon)
                {
                    continue;
                }

                OrientFaceOutward(facePoints, center);
                int startIndex = vertices.Count;
                vertices.AddRange(facePoints);

                for (int i = 1; i < facePoints.Count - 1; i++)
                {
                    triangles.Add(startIndex);
                    triangles.Add(startIndex + i);
                    triangles.Add(startIndex + i + 1);
                }

                faceCount++;
            }

            if (vertices.Count < 4 || triangles.Count < 12)
            {
                return null;
            }

            var mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildBoundsFallback(Bounds bounds, string meshName, float padding)
        {
            float safePadding = Mathf.Max(padding, 0.001f);
            if (bounds.size.sqrMagnitude <= Epsilon)
            {
                bounds = new Bounds(bounds.center, Vector3.one * safePadding);
            }
            else
            {
                Vector3 size = bounds.size;
                size.x = Mathf.Max(size.x, safePadding);
                size.y = Mathf.Max(size.y, safePadding);
                size.z = Mathf.Max(size.z, safePadding);
                bounds.size = size + (Vector3.one * safePadding * 2f);
            }

            List<Face> faces = CreateBoxFaces(bounds);
            return BuildMeshFromFaces(faces, meshName, out _);
        }

        private static List<Face> CreateBoxFaces(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3 p000 = new Vector3(min.x, min.y, min.z);
            Vector3 p001 = new Vector3(min.x, min.y, max.z);
            Vector3 p010 = new Vector3(min.x, max.y, min.z);
            Vector3 p011 = new Vector3(min.x, max.y, max.z);
            Vector3 p100 = new Vector3(max.x, min.y, min.z);
            Vector3 p101 = new Vector3(max.x, min.y, max.z);
            Vector3 p110 = new Vector3(max.x, max.y, min.z);
            Vector3 p111 = new Vector3(max.x, max.y, max.z);

            return new List<Face>
            {
                new Face(new[] { p000, p001, p011, p010 }),
                new Face(new[] { p100, p110, p111, p101 }),
                new Face(new[] { p000, p100, p101, p001 }),
                new Face(new[] { p010, p011, p111, p110 }),
                new Face(new[] { p000, p010, p110, p100 }),
                new Face(new[] { p001, p101, p111, p011 })
            };
        }

        private static Bounds BuildBounds(IReadOnlyList<Vector3> points)
        {
            Bounds bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return bounds;
        }

        private static bool IsDegenerate(Bounds bounds)
        {
            Vector3 size = bounds.size;
            float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (max <= Epsilon)
            {
                return true;
            }

            return size.x / max <= 0.0001f || size.y / max <= 0.0001f || size.z / max <= 0.0001f;
        }

        private static Vector3 FindSupportPoint(IReadOnlyList<Vector3> points, Vector3 direction)
        {
            int bestIndex = 0;
            float bestScore = Vector3.Dot(points[0], direction);

            for (int i = 1; i < points.Count; i++)
            {
                float score = Vector3.Dot(points[i], direction);
                if (score > bestScore)
                {
                    bestIndex = i;
                    bestScore = score;
                }
            }

            return points[bestIndex];
        }

        private static float EstimatePointCloudSize(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                return 1f;
            }

            Bounds bounds = BuildBounds(points);
            return Mathf.Max(bounds.size.magnitude, 1f);
        }

        private static List<Vector3> RemoveDuplicatePoints(IReadOnlyList<Vector3> points, float tolerance)
        {
            var result = new List<Vector3>();
            if (points == null)
            {
                return result;
            }

            float safeTolerance = Mathf.Max(tolerance, Epsilon);
            var seen = new HashSet<Vector3Int>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(points[i].x / safeTolerance),
                    Mathf.RoundToInt(points[i].y / safeTolerance),
                    Mathf.RoundToInt(points[i].z / safeTolerance));

                if (seen.Add(key))
                {
                    result.Add(points[i]);
                }
            }

            return result;
        }

        private static void RemoveDuplicateInPlace(List<Vector3> points, float tolerance)
        {
            float sqrTolerance = tolerance * tolerance;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if ((points[i] - points[j]).sqrMagnitude <= sqrTolerance)
                    {
                        points.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private static float PolygonArea(IReadOnlyList<Vector3> points)
        {
            if (points.Count < 3)
            {
                return 0f;
            }

            Vector3 normal = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 current = points[i];
                Vector3 next = points[(i + 1) % points.Count];
                normal += Vector3.Cross(current, next);
            }

            return normal.magnitude * 0.5f;
        }

        private static Vector3 CalculateFaceCenter(IReadOnlyList<Face> faces)
        {
            Vector3 center = Vector3.zero;
            int count = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                for (int p = 0; p < faces[i].Points.Count; p++)
                {
                    center += faces[i].Points[p];
                    count++;
                }
            }

            return count == 0 ? Vector3.zero : center / count;
        }

        private static void OrientFaceOutward(List<Vector3> points, Vector3 polyhedronCenter)
        {
            if (points.Count < 3)
            {
                return;
            }

            Vector3 faceCenter = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                faceCenter += points[i];
            }

            faceCenter /= points.Count;
            Vector3 normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
            if (Vector3.Dot(normal, faceCenter - polyhedronCenter) < 0f)
            {
                points.Reverse();
            }
        }
    }
}
