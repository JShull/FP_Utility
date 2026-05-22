namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Creates a flat rectangular grid mesh that can later be used for heightmap deformation.
    /// </summary>
    public class FPMeshGeneratorWindow : EditorWindow
    {
        [SerializeField]
        private FPMeshGridBuildSettings gridSettings = FPMeshGridBuildSettings.Default;
        [SerializeField]
        private FPMeshGridData meshDataAsset;
        [SerializeField]
        private GameObject lastGeneratedObject;
        [SerializeField]
        private Transform targetParent;
        [SerializeField]
        private Material previewMaterial;
        [SerializeField]
        private bool addMeshCollider;
        [SerializeField]
        private FPMeshHeightmapSettings heightmapSettings = FPMeshHeightmapSettings.Default;
        [SerializeField]
        private FPMeshHeightProcessSettings heightProcessSettings = FPMeshHeightProcessSettings.Default;
        [SerializeField]
        private bool autoUpdatePreviewObject = true;
        [SerializeField]
        private FPMeshPreviewProjection cameraProjection = FPMeshPreviewProjection.Perspective;
        [SerializeField]
        private bool invertCameraOrbit = false;
        [SerializeField]
        private bool showVertices = false;
        [SerializeField]
        private bool showEdges = false;

        private Vector2 scrollPosition;
        private Mesh previewMesh;
        private PreviewRenderUtility previewUtility;
        private Material generatedPreviewMaterial;
        private Quaternion previewRotation = Quaternion.Euler(38f, -35f, 0f);
        private float previewZoom = 1.35f;
        private int activeOrbitAxis = -1;
        private bool previewDirty = true;

        private const float ParameterPanelWidth = 352f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private const float ActionPanelHeight = 90f;

        [MenuItem("FuzzPhyte/Utility/Mesh/Mesh Generator", priority = FP_UtilityData.MENU_UTILITY_MESH + 4)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPMeshGeneratorWindow>("Mesh Generator");
            window.minSize = new Vector2(760f, 520f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(gridSettings.MeshName))
            {
                gridSettings = FPMeshGridBuildSettings.Default;
            }

            SyncSelectionDefaults();
            EnsurePreviewUtility();
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
        }

        private void OnGUI()
        {
            GUILayout.Label("Mesh Generator", EditorStyles.boldLabel);
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

            float leftWidth = Mathf.Clamp(ParameterPanelWidth, 260f, Mathf.Max(260f, workspaceRect.width - 280f - PanelGap));
            Rect parameterRect = new Rect(workspaceRect.x, workspaceRect.y, leftWidth, workspaceRect.height);
            Rect previewRect = new Rect(parameterRect.xMax + PanelGap, workspaceRect.y, Mathf.Max(100f, workspaceRect.xMax - parameterRect.xMax - PanelGap), workspaceRect.height);

            DrawParameterPanelContainer(parameterRect);
            DrawPreviewPanelContainer(previewRect);
        }

        private void DrawParameterPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect actionRect = new Rect(innerRect.x, innerRect.yMax - ActionPanelHeight, innerRect.width, ActionPanelHeight);
            Rect scrollRect = new Rect(innerRect.x, innerRect.y, innerRect.width, Mathf.Max(40f, innerRect.height - ActionPanelHeight - 6f));
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, 820f);

            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawParameterPanel();
            GUILayout.EndArea();
            GUI.EndScrollView();

            GUILayout.BeginArea(actionRect);
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawActions();
            GUILayout.EndArea();
        }

        private void DrawParameterPanel()
        {
            EditorGUI.BeginChangeCheck();

            DrawHeader();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawDataAssetSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawCameraSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawGridSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawHeightmapSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawHeightProcessSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawSceneSettings();

            if (EditorGUI.EndChangeCheck())
            {
                previewDirty = true;
                if (autoUpdatePreviewObject)
                {
                    RefreshLastGeneratedPreview();
                }

                Repaint();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("Mesh Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build a flat rectangular grid mesh on the XZ plane. " +
                "This first pass is intended as the base surface for future heightmap deformation.",
                MessageType.Info);
        }

        private void DrawCameraSettings()
        {
            EditorGUILayout.LabelField("Camera Properties", EditorStyles.boldLabel);
            cameraProjection = FPMeshPreviewEditorUtility.DrawProjectionPopup(cameraProjection);
            invertCameraOrbit = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Invert Camera Orbit", invertCameraOrbit);
            showVertices = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Show Vertices", showVertices);
            showEdges = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Show Edges", showEdges);
        }

        private void DrawGridSettings()
        {
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

            gridSettings.MeshName = EditorGUILayout.TextField("Mesh Name", gridSettings.MeshName);
            gridSettings.Width = EditorGUILayout.FloatField("Width", gridSettings.Width);
            gridSettings.Length = EditorGUILayout.FloatField("Length", gridSettings.Length);
            gridSettings.XSegments = EditorGUILayout.IntField("X Segments", gridSettings.XSegments);
            gridSettings.YSegments = EditorGUILayout.IntField("Y Segments", gridSettings.YSegments);
            gridSettings.CenterPivot = EditorGUILayout.Toggle("Center Pivot", gridSettings.CenterPivot);

            var safeSettings = gridSettings.Sanitized();
            int vertexCount = (safeSettings.XSegments + 1) * (safeSettings.YSegments + 1);
            int quadCount = safeSettings.XSegments * safeSettings.YSegments;
            int triangleCount = quadCount * 2;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Stats", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Vertices", vertexCount.ToString());
            EditorGUILayout.LabelField("Quads", quadCount.ToString());
            EditorGUILayout.LabelField("Triangles", triangleCount.ToString());
        }

        private void DrawDataAssetSettings()
        {
            EditorGUILayout.LabelField("Mesh Data", EditorStyles.boldLabel);

            meshDataAsset = (FPMeshGridData)EditorGUILayout.ObjectField("Data Asset", meshDataAsset, typeof(FPMeshGridData), false);

            using (new EditorGUI.DisabledScope(meshDataAsset == null))
            {
                if (GUILayout.Button("Load Settings From Data Asset"))
                {
                    LoadSettingsFromDataAsset();
                }

                if (GUILayout.Button("Save Current Settings To Data Asset"))
                {
                    SaveSettingsToDataAsset();
                }
            }

            if (meshDataAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an FPMeshGridData asset if you want to store and regenerate a mesh recipe from a reusable data file.",
                    MessageType.None);
            }
        }

        private void DrawSceneSettings()
        {
            EditorGUILayout.LabelField("Scene Output", EditorStyles.boldLabel);

            targetParent = (Transform)EditorGUILayout.ObjectField("Parent", targetParent, typeof(Transform), true);
            previewMaterial = (Material)EditorGUILayout.ObjectField("Material", previewMaterial, typeof(Material), false);
            addMeshCollider = EditorGUILayout.Toggle("Add MeshCollider", addMeshCollider);
            autoUpdatePreviewObject = EditorGUILayout.Toggle("Auto Update Preview", autoUpdatePreviewObject);

            if (GUILayout.Button("Use Current Selection As Parent"))
            {
                SyncSelectionDefaults();
            }
        }

        private void DrawHeightmapSettings()
        {
            EditorGUILayout.LabelField("Heightmap", EditorStyles.boldLabel);

            heightmapSettings.Heightmap = (Texture2D)EditorGUILayout.ObjectField("Heightmap", heightmapSettings.Heightmap, typeof(Texture2D), false);
            heightmapSettings.HeightScale = EditorGUILayout.FloatField("Height Scale", heightmapSettings.HeightScale);
            heightmapSettings.HeightOffset = EditorGUILayout.FloatField("Height Offset", heightmapSettings.HeightOffset);
            heightmapSettings.Channel = (FPMeshHeightmapChannel)EditorGUILayout.EnumPopup("Channel", heightmapSettings.Channel);
            heightmapSettings.Invert = EditorGUILayout.Toggle("Invert", heightmapSettings.Invert);
            heightmapSettings.FlipX = EditorGUILayout.Toggle("Flip X", heightmapSettings.FlipX);
            heightmapSettings.FlipY = EditorGUILayout.Toggle("Flip Y", heightmapSettings.FlipY);

            if (heightmapSettings.Heightmap == null)
            {
                EditorGUILayout.HelpBox(
                    "Leave the heightmap empty to generate a flat grid. Assign a texture to displace the grid using UV0 sampling.",
                    MessageType.None);
            }

            if (GUILayout.Button("Open Heightmap Editor"))
            {
                FPHeightmapEditorWindow.OpenWindow(meshDataAsset, heightmapSettings.Heightmap);
            }
        }

        private void DrawHeightProcessSettings()
        {
            EditorGUILayout.LabelField("Height Processing", EditorStyles.boldLabel);

            heightProcessSettings.UseRemap = EditorGUILayout.Toggle("Use Remap", heightProcessSettings.UseRemap);
            using (new EditorGUI.DisabledScope(!heightProcessSettings.UseRemap))
            {
                heightProcessSettings.RemapMin = EditorGUILayout.Slider("Remap Min", heightProcessSettings.RemapMin, 0f, 1f);
                heightProcessSettings.RemapMax = EditorGUILayout.Slider("Remap Max", heightProcessSettings.RemapMax, 0f, 1f);
            }

            heightProcessSettings.EdgeFalloffMode = (FPMeshEdgeFalloffMode)EditorGUILayout.EnumPopup("Falloff Mode", heightProcessSettings.EdgeFalloffMode);
            using (new EditorGUI.DisabledScope(heightProcessSettings.EdgeFalloffMode == FPMeshEdgeFalloffMode.None))
            {
                heightProcessSettings.EdgeFalloffStart = EditorGUILayout.Slider("Falloff Start", heightProcessSettings.EdgeFalloffStart, 0f, 1f);
                heightProcessSettings.EdgeFalloffStrength = EditorGUILayout.Slider("Falloff Strength", heightProcessSettings.EdgeFalloffStrength, 0f, 8f);
            }

            heightProcessSettings.UseTerracing = EditorGUILayout.Toggle("Use Terracing", heightProcessSettings.UseTerracing);
            using (new EditorGUI.DisabledScope(!heightProcessSettings.UseTerracing))
            {
                heightProcessSettings.TerraceSteps = EditorGUILayout.IntSlider("Terrace Steps", heightProcessSettings.TerraceSteps, 2, 64);
            }

            EditorGUILayout.HelpBox(
                "Use remap to isolate the useful height range, falloff to soften edges or create island shapes, and terracing for stepped surfaces.",
                MessageType.None);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space();

            Color originalColor = GUI.color;
            GUI.color = FP_Utility_Editor.OkayColor;

            if (GUILayout.Button("Create Scene Object", GUILayout.Height(32f)))
            {
                CreateSceneObject();
            }

            if (GUILayout.Button("Save Mesh Asset", GUILayout.Height(32f)))
            {
                SaveMeshAsset();
            }

            GUI.color = originalColor;
        }

        private void SyncSelectionDefaults()
        {
            if (Selection.activeTransform != null)
            {
                targetParent = Selection.activeTransform;
            }
        }

        private void LoadSettingsFromDataAsset()
        {
            if (meshDataAsset == null)
            {
                return;
            }

            gridSettings = meshDataAsset.GridSettings.Sanitized();
            heightmapSettings = meshDataAsset.HeightmapSettings.Sanitized();
            heightProcessSettings = meshDataAsset.HeightProcessSettings.Sanitized();
            Repaint();
        }

        private void SaveSettingsToDataAsset()
        {
            if (meshDataAsset == null)
            {
                return;
            }

            Undo.RecordObject(meshDataAsset, "Update Mesh Grid Data");
            meshDataAsset.Capture(gridSettings, heightmapSettings, heightProcessSettings);
            EditorUtility.SetDirty(meshDataAsset);
            AssetDatabase.SaveAssets();
        }

        private Mesh BuildMesh()
        {
            Mesh mesh = FPMeshGridBuilder.Build(gridSettings);
            FPMeshHeightmapUtility.ApplyHeightmap(mesh, heightmapSettings, heightProcessSettings);
            return mesh;
        }

        private void DrawPreviewPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect previewRect = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);

            if (Event.current.type == EventType.Repaint && previewDirty)
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
                GUI.Label(rect, "Adjust grid settings to preview the generated mesh.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewMaterials();

            if (Event.current.type != EventType.Repaint)
            {
                DrawOrbitGizmo(rect);
                return;
            }

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

            DrawPreviewMesh(previewMesh, generatedPreviewMaterial);
            previewUtility.camera.Render();
            Texture result = previewUtility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);

            if (showEdges)
            {
                FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, previewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.EdgeOverlayColor, 1.5f);
            }

            if (showVertices)
            {
                FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, previewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.VertexOverlayColor, 2.5f);
            }

            DrawPreviewOverlay(rect);
            FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(rect, previewUtility.camera, cameraProjection);
            DrawOrbitGizmo(rect);
        }

        private void RebuildPreview()
        {
            previewDirty = false;
            CleanupPreviewMesh();
            previewMesh = BuildMesh();
            if (previewMesh != null)
            {
                previewMesh.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private Bounds CalculatePreviewBounds()
        {
            if (previewMesh == null)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = previewMesh.bounds;
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

        private void DrawPreviewOverlay(Rect rect)
        {
            Rect overlayRect = new Rect(rect.x + 8f, rect.y + 8f, 226f, 90f);
            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(overlayRect.x + 6f, overlayRect.y + 5f, overlayRect.width - 12f, 18f);
            GUI.Label(lineRect, $"Preview Vertices: {GetVertexCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Triangles: {GetTriangleCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            FPMeshGridBuildSettings safeSettings = gridSettings.Sanitized();
            GUI.Label(lineRect, $"Grid: {safeSettings.XSegments} x {safeSettings.YSegments}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Zoom: {previewZoom:0.##}x", EditorStyles.miniLabel);
        }

        private static int GetVertexCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.vertexCount;
        }

        private static int GetTriangleCount(Mesh mesh)
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

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.nearClipPlane = 0.01f;
            previewUtility.camera.farClipPlane = 5000f;
        }

        private void EnsurePreviewMaterials()
        {
            if (generatedPreviewMaterial != null)
            {
                return;
            }

            generatedPreviewMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            generatedPreviewMaterial.SetColor("_Color", FPMeshPreviewEditorUtility.PreviewMeshColor);
            generatedPreviewMaterial.SetInt("_Cull", (int)CullMode.Off);
            generatedPreviewMaterial.SetInt("_ZWrite", 1);
            generatedPreviewMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        }

        private void CleanupPreviewMesh()
        {
            if (previewMesh == null)
            {
                return;
            }

            DestroyImmediate(previewMesh);
            previewMesh = null;
        }

        private void CreateSceneObject()
        {
            Mesh mesh = BuildMesh();
            GameObject go = new GameObject(mesh.name);
            Undo.RegisterCreatedObjectUndo(go, "Create FP Mesh Grid");

            if (targetParent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, targetParent.gameObject);
                go.transform.SetParent(targetParent, false);
            }

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = go.AddComponent<MeshRenderer>();
            if (previewMaterial != null)
            {
                meshRenderer.sharedMaterial = previewMaterial;
            }

            if (addMeshCollider)
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            if (meshDataAsset != null)
            {
                var gridInstance = go.AddComponent<FPMeshGridInstance>();
                gridInstance.DataAsset = meshDataAsset;
                gridInstance.PreviewMaterial = previewMaterial;
                gridInstance.AddMeshCollider = addMeshCollider;
                gridInstance.AutoRegenerateInEditor = true;
            }

            lastGeneratedObject = go;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private void SaveMeshAsset()
        {
            Mesh mesh = ResolveMeshForSaving();
            string defaultName = string.IsNullOrWhiteSpace(mesh.name) ? "FP_GridSurface" : mesh.name;
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Grid Mesh",
                defaultName,
                "asset",
                "Choose where to save the generated mesh asset.");

            if (string.IsNullOrWhiteSpace(path))
            {
                if (!EditorUtility.IsPersistent(mesh))
                {
                    DestroyImmediate(mesh);
                }
                return;
            }

            Mesh originalMeshReference = mesh;
            string result = FP_Utility_Editor.CreateAssetAt(mesh, path);
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (savedMesh != null)
            {
                ReplaceSceneMeshReferences(originalMeshReference, savedMesh);
            }

            Debug.Log($"[Mesh Generator] Mesh saved to {result}");
        }

        private Mesh ResolveMeshForSaving()
        {
            Mesh liveMesh = TryGetLiveGeneratedMesh();
            if (liveMesh != null)
            {
                return liveMesh;
            }

            return BuildMesh();
        }

        private Mesh TryGetLiveGeneratedMesh()
        {
            if (lastGeneratedObject == null)
            {
                return null;
            }

            var meshFilter = lastGeneratedObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return null;
            }

            if (EditorUtility.IsPersistent(meshFilter.sharedMesh))
            {
                return null;
            }

            return meshFilter.sharedMesh;
        }

        private void ReplaceSceneMeshReferences(Mesh originalMesh, Mesh savedMesh)
        {
            if (originalMesh == null || savedMesh == null)
            {
                return;
            }

            var meshFilters = Resources.FindObjectsOfTypeAll<MeshFilter>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || EditorUtility.IsPersistent(meshFilter))
                {
                    continue;
                }

                if (meshFilter.sharedMesh != originalMesh)
                {
                    continue;
                }

                Undo.RecordObject(meshFilter, "Assign Saved Grid Mesh");
                meshFilter.sharedMesh = savedMesh;
                EditorUtility.SetDirty(meshFilter);
            }

            var meshColliders = Resources.FindObjectsOfTypeAll<MeshCollider>();
            for (int i = 0; i < meshColliders.Length; i++)
            {
                MeshCollider meshCollider = meshColliders[i];
                if (meshCollider == null || EditorUtility.IsPersistent(meshCollider))
                {
                    continue;
                }

                if (meshCollider.sharedMesh != originalMesh)
                {
                    continue;
                }

                Undo.RecordObject(meshCollider, "Assign Saved Grid Mesh");
                meshCollider.sharedMesh = savedMesh;
                EditorUtility.SetDirty(meshCollider);
            }

            if (lastGeneratedObject != null)
            {
                EditorUtility.SetDirty(lastGeneratedObject);
            }
        }

        private void RefreshLastGeneratedPreview()
        {
            if (lastGeneratedObject == null)
            {
                return;
            }

            var gridInstance = lastGeneratedObject.GetComponent<FPMeshGridInstance>();
            if (gridInstance != null && meshDataAsset != null)
            {
                gridInstance.PreviewMaterial = previewMaterial;
                gridInstance.AddMeshCollider = addMeshCollider;
                gridInstance.AutoRegenerateInEditor = autoUpdatePreviewObject;
                EditorUtility.SetDirty(gridInstance);
                return;
            }

            var meshFilter = lastGeneratedObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            Mesh previousMesh = meshFilter.sharedMesh;
            Mesh nextMesh = BuildMesh();
            meshFilter.sharedMesh = nextMesh;

            var meshRenderer = lastGeneratedObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null && previewMaterial != null)
            {
                meshRenderer.sharedMaterial = previewMaterial;
                EditorUtility.SetDirty(meshRenderer);
            }

            var meshCollider = lastGeneratedObject.GetComponent<MeshCollider>();
            if (addMeshCollider)
            {
                if (meshCollider == null)
                {
                    meshCollider = lastGeneratedObject.AddComponent<MeshCollider>();
                }

                meshCollider.sharedMesh = nextMesh;
                EditorUtility.SetDirty(meshCollider);
            }
            else if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                EditorUtility.SetDirty(meshCollider);
            }

            if (previousMesh != null && !EditorUtility.IsPersistent(previousMesh))
            {
                DestroyImmediate(previousMesh);
            }

            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(lastGeneratedObject);
        }
    }
}
