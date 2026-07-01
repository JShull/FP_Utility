// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Video
{
    using FuzzPhyte.Utility.Video;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Builds and saves an inside-out sphere mesh for 360 video rendering workflows.
    /// </summary>
    public class FPVideoSphereGeneratorWindow : EditorWindow
    {
        [SerializeField] private FPVideoMeshShape meshShape = FPVideoMeshShape.Sphere;
        [SerializeField] private FPVideoSphereBuildSettings sphereSettings = FPVideoSphereBuildSettings.Default;
        [SerializeField] private FPVideoEllipsoidBuildSettings ellipsoidSettings = FPVideoEllipsoidBuildSettings.Default;
        [SerializeField] private FPVideoQuadBuildSettings quadSettings = FPVideoQuadBuildSettings.Default;
        [SerializeField] private Mesh targetMeshAsset;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Transform targetParent;
        [SerializeField] private bool addMeshCollider;
        [SerializeField] private GameObject lastGeneratedObject;
        [SerializeField] private FPMeshPreviewProjection cameraProjection = FPMeshPreviewProjection.Perspective;
        [SerializeField] private bool invertCameraOrbit;
        [SerializeField] private bool showVertices;
        [SerializeField] private bool showEdges;

        private Vector2 scrollPosition;
        private PreviewRenderUtility previewUtility;
        private Mesh previewMesh;
        private Material generatedPreviewMaterial;
        private Quaternion previewRotation = Quaternion.Euler(22f, -35f, 0f);
        private float previewZoom = 1.45f;
        private int activeOrbitAxis = -1;
        private bool previewDirty = true;

        private const float ParameterPanelWidth = 352f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private const float ActionPanelHeight = 110f;
        private const float ParameterViewHeight = 760f;

        [MenuItem("FuzzPhyte/Utility/Video/FP Video Sphere Generator", priority = FP_UtilityData.MENU_UTILITY_VIDEO + 1)]
        public static void ShowWindow()
        {
            FPVideoSphereGeneratorWindow window = GetWindow<FPVideoSphereGeneratorWindow>("FP Video Sphere");
            window.minSize = new Vector2(760f, 520f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(sphereSettings.MeshName))
            {
                sphereSettings = FPVideoSphereBuildSettings.Default;
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
            GUILayout.Label("FP Video Sphere Generator", EditorStyles.boldLabel);
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
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, ParameterViewHeight);

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
            DrawSphereSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawCameraSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawAssetSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawSceneSettings();

            if (EditorGUI.EndChangeCheck())
            {
                previewDirty = true;
                Repaint();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("FP Video Sphere Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates video-ready meshes for 360 and flat playback workflows. " +
                "Sphere and Ellipsoid are intended for immersive equirectangular content, while Quad is useful for flat video surfaces.",
                MessageType.Info);
        }

        private void DrawCameraSettings()
        {
            EditorGUILayout.LabelField("Camera Properties", EditorStyles.boldLabel);
            cameraProjection = FPMeshPreviewEditorUtility.DrawProjectionPopup(cameraProjection);
            invertCameraOrbit = FPMeshPreviewEditorUtility.DrawInvertCameraOrbitToggle(invertCameraOrbit);
            showVertices = FPMeshPreviewEditorUtility.DrawShowVerticesToggle(showVertices);
            showEdges = FPMeshPreviewEditorUtility.DrawShowEdgesToggle(showEdges);
        }

        private void DrawSphereSettings()
        {
            EditorGUILayout.LabelField("Mesh Settings", EditorStyles.boldLabel);
            meshShape = (FPVideoMeshShape)EditorGUILayout.EnumPopup("Shape", meshShape);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Stats", EditorStyles.boldLabel);

            switch (meshShape)
            {
                case FPVideoMeshShape.Sphere:
                    DrawSphereParameterBlock();
                    break;
                case FPVideoMeshShape.Ellipsoid:
                    DrawEllipsoidParameterBlock();
                    break;
                case FPVideoMeshShape.Quad:
                    DrawQuadParameterBlock();
                    break;
            }
        }

        private void DrawSphereParameterBlock()
        {
            sphereSettings.MeshName = EditorGUILayout.TextField("Mesh Name", sphereSettings.MeshName);
            sphereSettings.Radius = EditorGUILayout.FloatField("Radius", sphereSettings.Radius);
            sphereSettings.LongitudeSegments = EditorGUILayout.IntSlider("Longitude Segments", sphereSettings.LongitudeSegments, 3, 512);
            sphereSettings.LatitudeSegments = EditorGUILayout.IntSlider("Latitude Segments", sphereSettings.LatitudeSegments, 2, 256);
            sphereSettings.GenerateInsideOut = EditorGUILayout.Toggle("Inside Out", sphereSettings.GenerateInsideOut);

            FPVideoSphereBuildSettings safeSettings = sphereSettings.Sanitized();
            EditorGUILayout.LabelField("Vertices", safeSettings.VertexCount.ToString());
            EditorGUILayout.LabelField("Triangles", safeSettings.TriangleCount.ToString());

            if (!safeSettings.GenerateInsideOut)
            {
                EditorGUILayout.HelpBox("Inside Out is disabled, so this will generate a standard outward-facing sphere.", MessageType.None);
            }
        }

        private void DrawEllipsoidParameterBlock()
        {
            ellipsoidSettings.MeshName = EditorGUILayout.TextField("Mesh Name", ellipsoidSettings.MeshName);
            ellipsoidSettings.Radii = EditorGUILayout.Vector3Field("Radii", ellipsoidSettings.Radii);
            ellipsoidSettings.LongitudeSegments = EditorGUILayout.IntSlider("Longitude Segments", ellipsoidSettings.LongitudeSegments, 3, 512);
            ellipsoidSettings.LatitudeSegments = EditorGUILayout.IntSlider("Latitude Segments", ellipsoidSettings.LatitudeSegments, 2, 256);
            ellipsoidSettings.GenerateInsideOut = EditorGUILayout.Toggle("Inside Out", ellipsoidSettings.GenerateInsideOut);

            FPVideoEllipsoidBuildSettings safeSettings = ellipsoidSettings.Sanitized();
            EditorGUILayout.LabelField("Vertices", safeSettings.VertexCount.ToString());
            EditorGUILayout.LabelField("Triangles", safeSettings.TriangleCount.ToString());
            EditorGUILayout.HelpBox("Use Ellipsoid for stretched immersive spaces or non-uniform dome volumes.", MessageType.None);
        }

        private void DrawQuadParameterBlock()
        {
            quadSettings.MeshName = EditorGUILayout.TextField("Mesh Name", quadSettings.MeshName);
            quadSettings.Width = EditorGUILayout.FloatField("Width", quadSettings.Width);
            quadSettings.Height = EditorGUILayout.FloatField("Height", quadSettings.Height);
            quadSettings.WidthSegments = EditorGUILayout.IntSlider("Width Segments", quadSettings.WidthSegments, 1, 512);
            quadSettings.HeightSegments = EditorGUILayout.IntSlider("Height Segments", quadSettings.HeightSegments, 1, 512);
            quadSettings.FlipFacing = EditorGUILayout.Toggle("Flip Facing", quadSettings.FlipFacing);

            FPVideoQuadBuildSettings safeSettings = quadSettings.Sanitized();
            EditorGUILayout.LabelField("Vertices", safeSettings.VertexCount.ToString());
            EditorGUILayout.LabelField("Triangles", safeSettings.TriangleCount.ToString());
            EditorGUILayout.HelpBox("Use Quad for flat video playback surfaces, menus, kiosks, or billboards.", MessageType.None);
        }

        private void DrawAssetSettings()
        {
            EditorGUILayout.LabelField("Mesh Asset", EditorStyles.boldLabel);

            targetMeshAsset = (Mesh)EditorGUILayout.ObjectField("Target Mesh", targetMeshAsset, typeof(Mesh), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Mesh Asset"))
                {
                    SyncSelectedMeshAsset();
                }

                using (new EditorGUI.DisabledScope(targetMeshAsset == null || !EditorUtility.IsPersistent(targetMeshAsset)))
                {
                    if (GUILayout.Button("Overwrite Target Mesh"))
                    {
                        OverwriteTargetMeshAsset();
                    }
                }
            }

            string suggestedFileName = GetDefaultMeshFileName();
            EditorGUILayout.LabelField("Suggested File Name", suggestedFileName);

            if (targetMeshAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a saved Mesh asset here if you want to rebuild and overwrite an existing sphere asset in place.",
                    MessageType.None);
            }
        }

        private void DrawSceneSettings()
        {
            EditorGUILayout.LabelField("Scene Output", EditorStyles.boldLabel);

            targetParent = (Transform)EditorGUILayout.ObjectField("Parent", targetParent, typeof(Transform), true);
            previewMaterial = (Material)EditorGUILayout.ObjectField("Material", previewMaterial, typeof(Material), false);
            addMeshCollider = EditorGUILayout.Toggle("Add MeshCollider", addMeshCollider);

            if (GUILayout.Button("Use Current Selection As Parent"))
            {
                SyncSelectionDefaults();
            }

            EditorGUILayout.Space(10f);
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

        private void DrawPreviewPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect previewRect = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);

            if (Event.current.type == EventType.Repaint && previewDirty)
            {
                RebuildPreview();
            }

            DrawVideoPreview(previewRect);
        }

        private void DrawVideoPreview(Rect rect)
        {
            HandlePreviewInput(rect);

            if (previewMesh == null)
            {
                GUI.Label(rect, "Adjust mesh settings to preview the generated video surface.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewMaterials();

            if (Event.current.type != EventType.Repaint)
            {
                FPMeshPreviewEditorUtility.DrawOrbitGizmo(rect, SetPreviewView);
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

            DrawMeshOverlays(rect);
            DrawPreviewOverlay(rect);
            FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(rect, previewUtility.camera, cameraProjection);
            FPMeshPreviewEditorUtility.DrawOrbitGizmo(rect, SetPreviewView);
        }

        private void SyncSelectionDefaults()
        {
            if (Selection.activeTransform != null)
            {
                targetParent = Selection.activeTransform;
            }

            SyncSelectedMeshAsset();
        }

        private void SyncSelectedMeshAsset()
        {
            if (Selection.activeObject is Mesh selectedMesh && EditorUtility.IsPersistent(selectedMesh))
            {
                targetMeshAsset = selectedMesh;
            }
        }

        private Mesh BuildMesh()
        {
            switch (meshShape)
            {
                case FPVideoMeshShape.Ellipsoid:
                    return FPVideoEllipsoidBuilder.Build(ellipsoidSettings);
                case FPVideoMeshShape.Quad:
                    return FPVideoQuadBuilder.Build(quadSettings);
                default:
                    return FPVideoSphereBuilder.Build(sphereSettings);
            }
        }

        private void RebuildPreview()
        {
            previewDirty = false;
            CleanupPreviewMesh();

            previewMesh = BuildMesh();
            if (previewMesh != null)
            {
                previewMesh.name = $"Preview_{previewMesh.name}";
                previewMesh.hideFlags = HideFlags.HideAndDontSave;
            }
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

        private void DrawMeshOverlays(Rect rect)
        {
            if ((!showVertices && !showEdges) || previewUtility == null || previewUtility.camera == null)
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

        private void DrawPreviewOverlay(Rect rect)
        {
            Rect overlayRect = new Rect(rect.x + 8f, rect.y + 8f, 226f, 108f);
            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(overlayRect.x + 6f, overlayRect.y + 5f, overlayRect.width - 12f, 18f);
            GUI.Label(lineRect, $"Shape: {meshShape}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Vertices: {GetVertexCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Triangles: {GetTriangleCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Submeshes: {GetSubMeshCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Zoom: {previewZoom:0.##}x", EditorStyles.miniLabel);
        }

        private static int GetVertexCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.vertexCount;
        }

        private static int GetTriangleCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.triangles.Length / 3;
        }

        private static int GetSubMeshCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.subMeshCount;
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
                int axis = FPMeshPreviewEditorUtility.GetOrbitAxisAtPosition(rect, current.mousePosition);
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
                previewRotation = FPMeshPreviewEditorUtility.ApplyOrbitAxisDrag(previewRotation, activeOrbitAxis, current.delta);
                current.Use();
                Repaint();
                return;
            }

            if (FPMeshPreviewEditorUtility.IsOrbitGizmoPosition(rect, current.mousePosition))
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

        private void SetPreviewView(Vector3 viewDirection, Vector3 up)
        {
            previewRotation = Quaternion.LookRotation(viewDirection, up);
            Repaint();
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

        private void CreateSceneObject()
        {
            Mesh mesh = BuildMesh();
            GameObject go = new GameObject(mesh.name);
            Undo.RegisterCreatedObjectUndo(go, "Create FP Video Sphere");

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
                MeshCollider collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            lastGeneratedObject = go;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private void SaveMeshAsset()
        {
            Mesh mesh = ResolveMeshForSaving();
            string defaultName = GetDefaultMeshFileName();
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Video Sphere Mesh",
                defaultName,
                "asset",
                "Choose where to save the generated video sphere mesh asset.");

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
                targetMeshAsset = savedMesh;
                ReplaceSceneMeshReferences(originalMeshReference, savedMesh);
            }

            Debug.Log($"[FP Video Sphere Generator] Mesh saved to {result}");
        }

        private void OverwriteTargetMeshAsset()
        {
            if (targetMeshAsset == null || !EditorUtility.IsPersistent(targetMeshAsset))
            {
                Debug.LogWarning("[FP Video Sphere Generator] Assign a saved Mesh asset before overwriting.");
                return;
            }

            Mesh builtMesh = BuildMesh();
            builtMesh.name = targetMeshAsset.name;

            Undo.RecordObject(targetMeshAsset, "Overwrite Video Sphere Mesh");
            EditorUtility.CopySerialized(builtMesh, targetMeshAsset);
            EditorUtility.SetDirty(targetMeshAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(targetMeshAsset));

            ApplyTargetMeshToPreviewObject(targetMeshAsset);
            DestroyImmediate(builtMesh);

            Debug.Log($"[FP Video Sphere Generator] Overwrote mesh asset at {AssetDatabase.GetAssetPath(targetMeshAsset)}");
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

            MeshFilter meshFilter = lastGeneratedObject.GetComponent<MeshFilter>();
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

        private string GetDefaultMeshFileName()
        {
            switch (meshShape)
            {
                case FPVideoMeshShape.Ellipsoid:
                    FPVideoEllipsoidBuildSettings safeEllipsoid = ellipsoidSettings.Sanitized();
                    return $"{safeEllipsoid.MeshName}_{FormatFloatForFileName(safeEllipsoid.Radii.x)}_{FormatFloatForFileName(safeEllipsoid.Radii.y)}_{FormatFloatForFileName(safeEllipsoid.Radii.z)}_{safeEllipsoid.LongitudeSegments}_{safeEllipsoid.LatitudeSegments}";
                case FPVideoMeshShape.Quad:
                    FPVideoQuadBuildSettings safeQuad = quadSettings.Sanitized();
                    return $"{safeQuad.MeshName}_{FormatFloatForFileName(safeQuad.Width)}_{FormatFloatForFileName(safeQuad.Height)}_{safeQuad.WidthSegments}_{safeQuad.HeightSegments}";
                default:
                    FPVideoSphereBuildSettings safeSphere = sphereSettings.Sanitized();
                    return $"{safeSphere.MeshName}_{FormatFloatForFileName(safeSphere.Radius)}_{safeSphere.LongitudeSegments}_{safeSphere.LatitudeSegments}";
            }
        }

        private static string FormatFloatForFileName(float value)
        {
            return value.ToString("0.###").Replace('.', '_');
        }

        private void ApplyTargetMeshToPreviewObject(Mesh meshAsset)
        {
            if (lastGeneratedObject == null || meshAsset == null)
            {
                return;
            }

            MeshFilter meshFilter = lastGeneratedObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                Undo.RecordObject(meshFilter, "Assign Overwritten Video Sphere Mesh");
                meshFilter.sharedMesh = meshAsset;
                EditorUtility.SetDirty(meshFilter);
            }

            MeshCollider meshCollider = lastGeneratedObject.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                Undo.RecordObject(meshCollider, "Assign Overwritten Video Sphere Mesh");
                meshCollider.sharedMesh = meshAsset;
                EditorUtility.SetDirty(meshCollider);
            }
        }

        private static void ReplaceSceneMeshReferences(Mesh originalMesh, Mesh savedMesh)
        {
            if (originalMesh == null || savedMesh == null)
            {
                return;
            }

            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == originalMesh)
                {
                    Undo.RecordObject(meshFilter, "Assign Saved Video Sphere Mesh");
                    meshFilter.sharedMesh = savedMesh;
                    EditorUtility.SetDirty(meshFilter);
                }
            }

            MeshCollider[] meshColliders = Object.FindObjectsByType<MeshCollider>();
            foreach (MeshCollider meshCollider in meshColliders)
            {
                if (meshCollider.sharedMesh == originalMesh)
                {
                    Undo.RecordObject(meshCollider, "Assign Saved Video Sphere Mesh");
                    meshCollider.sharedMesh = savedMesh;
                    EditorUtility.SetDirty(meshCollider);
                }
            }

            if (!EditorUtility.IsPersistent(originalMesh))
            {
                Object.DestroyImmediate(originalMesh);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
