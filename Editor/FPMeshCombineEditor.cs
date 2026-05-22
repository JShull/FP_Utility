namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Editor window to combine multiple meshes into a single mesh using submeshes.
    /// The combined mesh is baked using world-space transforms into the local space
    /// of a chosen root object. Intended mostly for generating MeshCollider assets.
    /// </summary>
    public class FPMeshCombineEditor:EditorWindow
    {
        // Root under which we search for meshes (or null to use explicit selection)
        [SerializeField]
        private GameObject rootObject;

        // General options
        [SerializeField]
        private bool includeChildren = true;
        [SerializeField]
        private bool includeInactive = true;
        [SerializeField]
        private bool includeMeshColliders = true;
        [SerializeField]
        private bool includeMeshFilters = true;
        [SerializeField]
        private bool includeSkinnedMeshRenderers = true;
        [SerializeField]
        private bool skipEditorOnlyTagged = true;
        [SerializeField]
        private FPMeshPreviewProjection cameraProjection = FPMeshPreviewProjection.Perspective;
        [SerializeField]
        private bool invertCameraOrbit = false;
        [SerializeField]
        private bool showSourceMesh = false;
        [SerializeField]
        private bool showVertices = false;
        [SerializeField]
        private bool showEdges = false;

        // Output options
        [SerializeField]
        private string combinedMeshName = "FP_CombinedCollider";
        [SerializeField]
        private bool addMeshColliderToRoot = true;
        [SerializeField]
        private bool replaceExistingMeshCollider = true;
        [SerializeField]
        private bool makeColliderConvex = false;
        [SerializeField]
        private bool isTrigger = false;

        private Vector2 scrollPos;
        private Mesh previewMesh;
        private PreviewRenderUtility previewUtility;
        private Material combinedPreviewMaterial;
        private Material sourcePreviewMaterial;
        private Quaternion previewRotation = Quaternion.Euler(22f, -35f, 0f);
        private float previewZoom = 1.45f;
        private int activeOrbitAxis = -1;
        private bool previewDirty = true;

        private const float ParameterPanelWidth = 352f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private const float ActionPanelHeight = 104f;

        [MenuItem("FuzzPhyte/Utility/Mesh/Combine Meshes", priority = FP_UtilityData.MENU_UTILITY_MESH + 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPMeshCombineEditor>("Combine Meshes");
            window.minSize = new Vector2(760f, 420f);
            window.InitDefaults();
        }

        private void OnEnable()
        {
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

            if (combinedPreviewMaterial != null)
            {
                DestroyImmediate(combinedPreviewMaterial);
                combinedPreviewMaterial = null;
            }

            if (sourcePreviewMaterial != null)
            {
                DestroyImmediate(sourcePreviewMaterial);
                sourcePreviewMaterial = null;
            }
        }

        private void InitDefaults()
        {
            if (Selection.activeGameObject != null)
            {
                rootObject = Selection.activeGameObject;
                combinedMeshName = $"{rootObject.name}_CombinedCollider";
                previewDirty = true;
            }
        }

        private void OnGUI()
        {
            if (rootObject == null && Selection.activeGameObject != null)
            {
                // soft default to current selection on first open
                rootObject = Selection.activeGameObject;
                if (string.IsNullOrEmpty(combinedMeshName))
                    combinedMeshName = $"{rootObject.name}_CombinedCollider";
                previewDirty = true;
            }

            GUILayout.Label("Combine Meshes", EditorStyles.boldLabel);
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
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, 680f);

            scrollPos = GUI.BeginScrollView(scrollRect, scrollPos, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawParameterPanel();
            GUILayout.EndArea();
            GUI.EndScrollView();

            GUILayout.BeginArea(actionRect);
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawActionButtons();
            GUILayout.EndArea();
        }

        private void DrawParameterPanel()
        {
            EditorGUI.BeginChangeCheck();

            DrawHeader();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawRootSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawSourceSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawCameraSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawOutputSettings();

            if (EditorGUI.EndChangeCheck())
            {
                previewDirty = true;
                Repaint();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("Combine Meshes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Combine multiple MeshFilters / SkinnedMeshRenderers / MeshColliders into a single Mesh asset, " +
                "baked using world-space positions into the local space of the selected root.\n\n" +
                "Ideal for generating a single MeshCollider from many scene meshes.",
                MessageType.Info);
        }

        private void DrawRootSettings()
        {
            EditorGUILayout.LabelField("Root / Scope", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            rootObject = (GameObject)EditorGUILayout.ObjectField("Root Object", rootObject, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (rootObject != null && string.IsNullOrEmpty(combinedMeshName))
                {
                    combinedMeshName = $"{rootObject.name}_CombinedCollider";
                }

                previewDirty = true;
            }

            using (new EditorGUI.DisabledScope(Selection.activeGameObject == null))
            {
                if (GUILayout.Button("Use Current Selection As Root"))
                {
                    rootObject = Selection.activeGameObject;
                    if (rootObject != null)
                    {
                        combinedMeshName = $"{rootObject.name}_CombinedCollider";
                    }

                    previewDirty = true;
                }
            }
        }

        private void DrawSourceSettings()
        {
            EditorGUILayout.LabelField("Source Mesh Settings", EditorStyles.boldLabel);

            includeChildren = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Include Children", includeChildren);
            includeInactive = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Include Inactive", includeInactive);

            EditorGUILayout.Space();
            includeMeshFilters = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Include MeshFilters", includeMeshFilters);
            includeSkinnedMeshRenderers = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Include SkinnedMeshRenderers", includeSkinnedMeshRenderers);
            includeMeshColliders = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Include MeshColliders", includeMeshColliders);

            skipEditorOnlyTagged = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Skip 'EditorOnly' Tagged Objects", skipEditorOnlyTagged);

            EditorGUILayout.Space();

            int count = PreviewMeshCount();
            EditorGUILayout.LabelField("Meshes Found (Preview)", count.ToString());
        }

        private void DrawCameraSettings()
        {
            EditorGUILayout.LabelField("Camera Properties", EditorStyles.boldLabel);
            cameraProjection = FPMeshPreviewEditorUtility.DrawProjectionPopup(cameraProjection);
            invertCameraOrbit = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Invert Camera Orbit", invertCameraOrbit);
            showSourceMesh = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Show Source Mesh", showSourceMesh);
            showVertices = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Show Vertices", showVertices);
            showEdges = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Show Edges", showEdges);
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);

            combinedMeshName = EditorGUILayout.TextField("Combined Mesh Name", combinedMeshName);

            EditorGUILayout.Space();
            addMeshColliderToRoot = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Add MeshCollider to Root", addMeshColliderToRoot);

            using (new EditorGUI.DisabledScope(!addMeshColliderToRoot))
            {
                replaceExistingMeshCollider = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Replace Existing Collider", replaceExistingMeshCollider);
                makeColliderConvex = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Collider Convex", makeColliderConvex);
                isTrigger = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Collider Is Trigger", isTrigger);
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space();

            bool canCombine = rootObject != null && PreviewMeshCount() > 0;

            using (new EditorGUI.DisabledScope(!canCombine))
            {
                Color defaultColor = GUI.color;

                if (canCombine)
                {
                    GUI.color = FP_Utility_Editor.OkayColor;
                }

                if (GUILayout.Button("Combine Meshes and Save Asset", GUILayout.Height(32)))
                {
                    CombineAndSave();
                }

                GUI.color = defaultColor;
            }

            if (!canCombine)
            {
                EditorGUILayout.HelpBox(
                    "Assign a root object and ensure there are MeshFilters, SkinnedMeshRenderers, and/or MeshColliders under it.",
                    MessageType.Warning);
            }
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

            if (rootObject == null)
            {
                GUI.Label(rect, "Assign a root object to preview.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (previewMesh == null)
            {
                GUI.Label(rect, "No mesh sources found for the current settings.", EditorStyles.centeredGreyMiniLabel);
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

            if (showSourceMesh)
            {
                DrawSourcePreviewMeshes();
            }

            DrawPreviewMesh(previewMesh, combinedPreviewMaterial);
            previewUtility.camera.Render();
            Texture result = previewUtility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);

            DrawMeshOverlays(rect);
            DrawPreviewOverlay(rect);
            FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(rect, previewUtility.camera, cameraProjection);
            DrawOrbitGizmo(rect);
        }

        // --- Core Logic ------------------------------------------------------

        private struct MeshSource
        {
            public Mesh Mesh;
            public Transform Transform;

            public MeshSource(Mesh mesh, Transform transform)
            {
                Mesh = mesh;
                Transform = transform;
            }
        }

        private int PreviewMeshCount()
        {
            if (rootObject == null)
                return 0;

            var sources = GetSourceMeshes();
            return sources.Count;
        }

        private List<MeshSource> GetSourceMeshes()
        {
            var result = new List<MeshSource>();
            var sourceComponents = new HashSet<Component>();

            if (!includeMeshFilters && !includeSkinnedMeshRenderers && !includeMeshColliders)
                return result;

            if (rootObject == null)
                return result;

            if (includeMeshFilters)
            {
                var filters = includeChildren
                    ? rootObject.GetComponentsInChildren<MeshFilter>(includeInactive)
                    : rootObject.GetComponents<MeshFilter>();

                foreach (var mf in filters)
                {
                    if (!IsValidSourceObject(mf.gameObject))
                        continue;

                    if (mf.sharedMesh != null)
                    {
                        result.Add(new MeshSource(mf.sharedMesh, mf.transform));
                        sourceComponents.Add(mf);
                    }
                }
            }

            if (includeSkinnedMeshRenderers)
            {
                var skinnedRenderers = includeChildren
                    ? rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive)
                    : rootObject.GetComponents<SkinnedMeshRenderer>();

                foreach (var smr in skinnedRenderers)
                {
                    if (!IsValidSourceObject(smr.gameObject))
                        continue;

                    if (smr.sharedMesh != null)
                    {
                        result.Add(new MeshSource(smr.sharedMesh, smr.transform));
                        sourceComponents.Add(smr);
                    }
                }
            }

            if (includeMeshColliders)
            {
                var colliders = includeChildren
                    ? rootObject.GetComponentsInChildren<MeshCollider>(includeInactive)
                    : rootObject.GetComponents<MeshCollider>();

                foreach (var col in colliders)
                {
                    if (!IsValidSourceObject(col.gameObject))
                        continue;

                    if (col.sharedMesh == null)
                        continue;

                    // Prefer visual mesh sources when present, but fall back to the collider's mesh.
                    var mf = col.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && sourceComponents.Contains(mf))
                        continue;

                    var smr = col.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh != null && sourceComponents.Contains(smr))
                        continue;

                    result.Add(new MeshSource(col.sharedMesh, col.transform));
                    sourceComponents.Add(col);
                }
            }

            return result;
        }

        private void RebuildPreview()
        {
            previewDirty = false;
            CleanupPreviewMesh();

            var sources = GetSourceMeshes();
            if (sources.Count == 0)
            {
                return;
            }

            previewMesh = BuildCombinedMesh(sources, "FP_CombinedPreview");
            if (previewMesh != null)
            {
                previewMesh.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private Mesh BuildCombinedMesh(List<MeshSource> sources, string meshName)
        {
            var combineInstances = BuildCombineInstances(sources);
            if (combineInstances.Count == 0)
            {
                return null;
            }

            var combinedMesh = new Mesh
            {
                name = string.IsNullOrEmpty(meshName) ? "FP_CombinedCollider" : meshName
            };

            int estimatedVertexCount = EstimateVertexCount(combineInstances);
            if (estimatedVertexCount > 65535)
            {
                combinedMesh.indexFormat = IndexFormat.UInt32;
            }

            combinedMesh.CombineMeshes(
                combineInstances.ToArray(),
                mergeSubMeshes: false,
                useMatrices: true);

            combinedMesh.RecalculateBounds();
            return combinedMesh;
        }

        private List<CombineInstance> BuildCombineInstances(List<MeshSource> sources)
        {
            var combineInstances = new List<CombineInstance>();
            if (rootObject == null)
            {
                return combineInstances;
            }

            var rootToLocal = rootObject.transform.worldToLocalMatrix;

            foreach (var source in sources)
            {
                var mesh = source.Mesh;
                if (mesh == null || source.Transform == null)
                {
                    continue;
                }

                var localToWorld = source.Transform.localToWorldMatrix;
                var xform = rootToLocal * localToWorld;

                int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                for (int s = 0; s < subMeshCount; s++)
                {
                    combineInstances.Add(new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = s,
                        transform = xform
                    });
                }
            }

            return combineInstances;
        }

        private static int EstimateVertexCount(List<CombineInstance> combineInstances)
        {
            int estimatedVertexCount = 0;
            foreach (var ci in combineInstances)
            {
                if (ci.mesh != null)
                {
                    estimatedVertexCount += ci.mesh.vertexCount;
                }
            }

            return estimatedVertexCount;
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

        private void DrawSourcePreviewMeshes()
        {
            if (rootObject == null)
            {
                return;
            }

            var sources = GetSourceMeshes();
            var rootToLocal = rootObject.transform.worldToLocalMatrix;
            for (int i = 0; i < sources.Count; i++)
            {
                MeshSource source = sources[i];
                if (source.Mesh == null || source.Transform == null)
                {
                    continue;
                }

                Matrix4x4 matrix = rootToLocal * source.Transform.localToWorldMatrix;
                int subMeshCount = Mathf.Max(1, source.Mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    previewUtility.DrawMesh(source.Mesh, matrix, sourcePreviewMaterial, subMesh);
                }
            }
        }

        private void DrawMeshOverlays(Rect rect)
        {
            if ((!showVertices && !showEdges) || previewUtility == null || previewUtility.camera == null)
            {
                return;
            }

            if (showSourceMesh && rootObject != null)
            {
                var sources = GetSourceMeshes();
                var rootToLocal = rootObject.transform.worldToLocalMatrix;
                for (int i = 0; i < sources.Count; i++)
                {
                    MeshSource source = sources[i];
                    if (source.Mesh == null || source.Transform == null)
                    {
                        continue;
                    }

                    Matrix4x4 matrix = rootToLocal * source.Transform.localToWorldMatrix;
                    if (showEdges)
                    {
                        FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, source.Mesh, matrix, FPMeshPreviewEditorUtility.VertexOverlayColor, 1.25f);
                    }

                    if (showVertices)
                    {
                        FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, source.Mesh, matrix, FPMeshPreviewEditorUtility.VertexOverlayColor, 2f);
                    }
                }
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
            Rect overlayRect = new Rect(rect.x + 8f, rect.y + 8f, 226f, 90f);
            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(overlayRect.x + 6f, overlayRect.y + 5f, overlayRect.width - 12f, 18f);
            GUI.Label(lineRect, $"Meshes Found: {PreviewMeshCount()}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Vertices: {GetVertexCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Submeshes: {GetSubMeshCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Zoom: {previewZoom:0.##}x", EditorStyles.miniLabel);
        }

        private static int GetVertexCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.vertexCount;
        }

        private static int GetSubMeshCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.subMeshCount;
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
            if (combinedPreviewMaterial == null)
            {
                combinedPreviewMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                combinedPreviewMaterial.SetColor("_Color", FPMeshPreviewEditorUtility.PreviewMeshColor);
                combinedPreviewMaterial.SetInt("_Cull", (int)CullMode.Off);
                combinedPreviewMaterial.SetInt("_ZWrite", 1);
                combinedPreviewMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            }

            if (sourcePreviewMaterial == null)
            {
                sourcePreviewMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                sourcePreviewMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.18f));
                sourcePreviewMaterial.SetInt("_Cull", (int)CullMode.Off);
                sourcePreviewMaterial.SetInt("_ZWrite", 0);
                sourcePreviewMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
                sourcePreviewMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                sourcePreviewMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
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

        private bool IsValidSourceObject(GameObject go)
        {
            if (skipEditorOnlyTagged && go.CompareTag("EditorOnly"))
                return false;

            return true;
        }

        private void CombineAndSave()
        {
            if (rootObject == null)
            {
                Debug.LogError("[Combine Meshes] Root object is null.");
                return;
            }

            var sources = GetSourceMeshes();
            if (sources.Count == 0)
            {
                Debug.LogWarning("[Combine Meshes] No valid source meshes found.");
                return;
            }

            var combinedMesh = BuildCombinedMesh(sources, string.IsNullOrEmpty(combinedMeshName) ? "FP_CombinedCollider" : combinedMeshName);

            if (combinedMesh == null)
            {
                Debug.LogWarning("[Combine Meshes] No CombineInstances created. Aborting.");
                return;
            }

            // Ask user where to save the asset
            string defaultName = string.IsNullOrEmpty(combinedMeshName)
                ? "FP_CombinedCollider"
                : combinedMeshName;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Combined Mesh",
                defaultName,
                "asset",
                "Choose a location to save the combined mesh asset."
            );

            if (string.IsNullOrEmpty(path))
            {
                DestroyImmediate(combinedMesh);
                return;
            }

            string result = FP_Utility_Editor.CreateAssetAt(combinedMesh, path);
            Debug.Log($"[Combine Meshes] {result}");

            // Optionally hook up a MeshCollider on the root
            if (addMeshColliderToRoot)
            {
                var rootCollider = rootObject.GetComponent<MeshCollider>();
                if (rootCollider == null)
                {
                    rootCollider = rootObject.AddComponent<MeshCollider>();
                }
                else if (!replaceExistingMeshCollider)
                {
                    // If we're not allowed to replace, add a new child with the collider instead
                    var child = new GameObject(defaultName + "_Collider");
                    child.transform.SetParent(rootObject.transform, false);
                    rootCollider = child.AddComponent<MeshCollider>();
                }

                rootCollider.sharedMesh = combinedMesh;
                rootCollider.convex = makeColliderConvex;
                rootCollider.isTrigger = isTrigger;
            }

            EditorUtility.DisplayDialog(
                "Combine Meshes",
                $"Combined mesh created with {combinedMesh.subMeshCount} submeshes.\nSaved to:\n{path}",
                "OK"
            );
        }
    }
}
