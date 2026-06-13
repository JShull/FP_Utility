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
    using UnityEngine.Rendering;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Editor window for slicing a readable mesh with an adjustable plane.
    /// </summary>
    public class FPMeshSlicerWindow : EditorWindow
    {
        private enum SlicePreviewMode
        {
            Positive,
            Negative,
            Both
        }

        private enum SliceOutputMode
        {
            KeepBoth,
            KeepPositive,
            KeepNegative
        }

        private enum MeshReferenceMode
        {
            Pivot,
            Center
        }

        [SerializeField] private Object sourceObject;
        [SerializeField] private string outputMeshName = "FP_SlicedMesh";
        [SerializeField] private string outputFolder = "Assets/_FPUtility";
        [SerializeField] private Vector3 planePosition;
        [SerializeField] private Vector3 planeRotation;
        [SerializeField] private Vector3 objectOffset;
        [SerializeField] private Vector3 objectRotation;
        [SerializeField] private Vector3 objectScale = Vector3.one;
        [SerializeField] private bool includeChildren = false;
        [SerializeField] private bool repairSliceHoles = true;
        [SerializeField] private bool autoUpdatePreview = true;
        [SerializeField] private FPMeshPreviewProjection cameraProjection = FPMeshPreviewProjection.Perspective;
        [SerializeField] private bool invertCameraOrbit = false;
        [SerializeField] private bool showSourceMesh = false;
        [SerializeField] private bool showVertices = false;
        [SerializeField] private bool showEdges = false;
        [SerializeField] private SlicePreviewMode previewMode = SlicePreviewMode.Both;
        [SerializeField] private SliceOutputMode outputMode = SliceOutputMode.KeepPositive;
        [SerializeField] private MeshReferenceMode referenceMode = MeshReferenceMode.Center;
        [SerializeField] private Transform targetParent;
        [SerializeField] private Material sceneMaterial;
        [SerializeField] private bool createSceneObjects = true;
        [SerializeField] private bool addMeshColliders;

        private readonly List<SourcePreviewMesh> sourcePreviewMeshes = new List<SourcePreviewMesh>();
        private readonly List<SliceSourceMesh> sourceMeshes = new List<SliceSourceMesh>();
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> errors = new List<string>();
        private Mesh positivePreviewMesh;
        private Mesh negativePreviewMesh;
        private Mesh planeFrontPreviewMesh;
        private Mesh planeBackPreviewMesh;
        private Mesh lastSavedPositiveMesh;
        private Mesh lastSavedNegativeMesh;
        private PreviewRenderUtility previewUtility;
        private Material sourcePreviewMaterial;
        private Material positivePreviewMaterial;
        private Material negativePreviewMaterial;
        private Material planeFrontPreviewMaterial;
        private Material planeBackPreviewMaterial;
        private Quaternion previewRotation = Quaternion.Euler(22f, -35f, 0f);
        private float previewZoom = 1.45f;
        private int activeOrbitAxis = -1;
        private int activePlaneHandle = -1;
        private int hoverPlaneHandle = -1;
        private bool activePlaneHandleUndoRecorded;
        private bool previewDirty = true;
        private Vector2 parameterScrollPosition;
        private Vector2 messageScrollPosition;
        private FPMeshSliceReport lastReport;
        private Bounds lastPreviewBounds = new Bounds(Vector3.zero, Vector3.one);

        private const float ParameterPanelWidth = 352f;
        private const float BottomDebugHeight = 112f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private const float ActionPanelHeight = 130f;
        private const int PlaneHandleMove = 0;
        private const int PlaneHandleRotateX = 1;
        private const int PlaneHandleRotateY = 2;
        private const int PlaneHandleRotateZ = 3;
        private const int PlaneHandleMoveX = 4;
        private const int PlaneHandleMoveY = 5;
        private const int PlaneHandleMoveZ = 6;
        private const float PlaneMoveHandlePickRadius = 22f;
        private const float PlaneRotateHandlePickRadius = 18f;
        private const float PlaneAxisMovePickRadius = 14f;
        private const float PlaneHandleScreenRadius = 148f;

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

        internal struct SliceSourceMesh
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;

            public SliceSourceMesh(Mesh mesh, Matrix4x4 matrix)
            {
                Mesh = mesh;
                Matrix = matrix;
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

        [MenuItem("FuzzPhyte/Utility/Mesh/Mesh Slicer", priority = FP_UtilityData.MENU_UTILITY_MESH + 3)]
        public static void ShowWindow()
        {
            FPMeshSlicerWindow window = GetWindow<FPMeshSlicerWindow>("Mesh Slicer");
            window.minSize = new Vector2(760f, 520f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            Undo.undoRedoPerformed += HandleUndoRedo;
            EnsurePreviewUtility();
            EnsurePlaneMesh();
            SyncSelectionDefaults();
            previewDirty = true;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndoRedo;
            CleanupPreviewMeshes();

            if (planeFrontPreviewMesh != null)
            {
                DestroyImmediate(planeFrontPreviewMesh);
                planeFrontPreviewMesh = null;
            }

            if (planeBackPreviewMesh != null)
            {
                DestroyImmediate(planeBackPreviewMesh);
                planeBackPreviewMesh = null;
            }

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }

            DestroyMaterial(ref sourcePreviewMaterial);
            DestroyMaterial(ref positivePreviewMaterial);
            DestroyMaterial(ref negativePreviewMaterial);
            DestroyMaterial(ref planeFrontPreviewMaterial);
            DestroyMaterial(ref planeBackPreviewMaterial);
        }

        private void OnFocus()
        {
            wantsMouseMove = true;
        }

        private void OnLostFocus()
        {
            wantsMouseMove = false;
            hoverPlaneHandle = -1;
            activePlaneHandle = -1;
        }

        private void OnGUI()
        {
            GUILayout.Label("Mesh Slicer", EditorStyles.boldLabel);
            DrawWorkspace();
        }

        private void RecordSlicerUndo(string actionName)
        {
            string safeActionName = string.IsNullOrWhiteSpace(actionName) ? "Edit Mesh Slicer" : actionName;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(safeActionName);
            Undo.RecordObject(this, safeActionName);
            EditorUtility.SetDirty(this);
        }

        private void HandleUndoRedo()
        {
            previewDirty = true;
            hoverPlaneHandle = -1;
            activePlaneHandle = -1;
            activePlaneHandleUndoRecorded = false;
            if (sourceObject != null)
            {
                RebuildPreview();
            }
            else
            {
                CleanupPreviewMeshes();
                Repaint();
            }
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
            Rect actionRect = new Rect(innerRect.x, innerRect.yMax - ActionPanelHeight, innerRect.width, ActionPanelHeight);
            Rect scrollRect = new Rect(innerRect.x, innerRect.y, innerRect.width, Mathf.Max(40f, innerRect.height - ActionPanelHeight - 6f));
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, 880f);

            parameterScrollPosition = GUI.BeginScrollView(scrollRect, parameterScrollPosition, viewRect);
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

            DrawSourceSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawCameraSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawObjectSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawPlaneSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawOutputSettings();

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
            Object nextSourceObject = EditorGUILayout.ObjectField("Object / Mesh", sourceObject, typeof(Object), true);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Change Slice Source");
                sourceObject = nextSourceObject;
                ApplyDefaultOutputName();
                AlignPlaneToReference(false);
                previewZoom = 1.45f;
                previewDirty = true;
            }

            EditorGUI.BeginChangeCheck();
            bool nextIncludeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Toggle Include Children");
                includeChildren = nextIncludeChildren;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection"))
                {
                    RecordSlicerUndo("Use Selected Slice Source");
                    SyncSelectedSource();
                    AlignPlaneToReference(false);
                    previewZoom = 1.45f;
                    previewDirty = true;
                }

                if (GUILayout.Button("Frame Plane"))
                {
                    AlignPlaneToReference();
                    previewDirty = true;
                }
            }

            SourceSummary summary = ResolveSource();
            if (!summary.HasMesh)
            {
                EditorGUILayout.HelpBox("Assign a GameObject, MeshFilter, SkinnedMeshRenderer, or Mesh asset. GameObjects bake child meshes into the selected root's local space before slicing.", MessageType.None);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Meshes Found", summary.MeshCount.ToString());
            EditorGUILayout.LabelField("Source Vertices", summary.VertexCount.ToString());
            EditorGUILayout.LabelField("Source Triangles", summary.TriangleCount.ToString());
            EditorGUILayout.LabelField("Source Bounds", FormatVector3(summary.Bounds.size));

            if (summary.HasUnreadableMesh)
            {
                EditorGUILayout.HelpBox("One or more source meshes are not readable. Enable Read/Write on their import settings before slicing.", MessageType.Warning);
            }
        }

        private void DrawCameraSettings()
        {
            EditorGUILayout.LabelField("Camera Properties", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            FPMeshPreviewProjection nextCameraProjection = FPMeshPreviewEditorUtility.DrawProjectionPopup(cameraProjection);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Change Slicer Camera Projection");
                cameraProjection = nextCameraProjection;
            }

            EditorGUI.BeginChangeCheck();
            MeshReferenceMode nextReferenceMode = (MeshReferenceMode)EditorGUILayout.EnumPopup("Reference Origin", referenceMode);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Change Slicer Reference Origin");
                referenceMode = nextReferenceMode;
                AlignPlaneToReference(false);
                previewZoom = 1.45f;
                previewDirty = true;
            }

            EditorGUI.BeginChangeCheck();
            bool nextInvertCameraOrbit = FPMeshPreviewEditorUtility.DrawInvertCameraOrbitToggle(invertCameraOrbit);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Toggle Slicer Camera Orbit");
                invertCameraOrbit = nextInvertCameraOrbit;
            }

            EditorGUI.BeginChangeCheck();
            SlicePreviewMode nextPreviewMode = (SlicePreviewMode)EditorGUILayout.EnumPopup("Preview Visibility", previewMode);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Change Slice Preview Visibility");
                previewMode = nextPreviewMode;
            }

            EditorGUI.BeginChangeCheck();
            bool nextShowSourceMesh = EditorGUILayout.Toggle("Show Source Mesh", showSourceMesh);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Toggle Slicer Source Preview");
                showSourceMesh = nextShowSourceMesh;
            }

            EditorGUI.BeginChangeCheck();
            bool nextShowVertices = FPMeshPreviewEditorUtility.DrawShowVerticesToggle(showVertices);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Toggle Slicer Vertex Preview");
                showVertices = nextShowVertices;
            }

            EditorGUI.BeginChangeCheck();
            bool nextShowEdges = FPMeshPreviewEditorUtility.DrawShowEdgesToggle(showEdges);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Toggle Slicer Edge Preview");
                showEdges = nextShowEdges;
            }
        }

        private void DrawPlaneSettings()
        {
            EditorGUILayout.LabelField("Slice Plane", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 nextPlanePosition = EditorGUILayout.Vector3Field("Position", planePosition);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Move Slice Plane");
                planePosition = nextPlanePosition;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 nextPlaneRotation = EditorGUILayout.Vector3Field("Rotation", planeRotation);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Rotate Slice Plane");
                planeRotation = nextPlaneRotation;
            }

            EditorGUI.BeginChangeCheck();
            bool nextRepairSliceHoles = EditorGUILayout.Toggle("Repair Slice Holes", repairSliceHoles);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Toggle Slice Hole Repair");
                repairSliceHoles = nextRepairSliceHoles;
            }

            EditorGUI.BeginChangeCheck();
            SliceOutputMode nextOutputMode = (SliceOutputMode)EditorGUILayout.EnumPopup("Keep Pieces", outputMode);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Change Slice Keep Mode");
                outputMode = nextOutputMode;
            }

            EditorGUI.BeginChangeCheck();
            bool nextAutoUpdatePreview = EditorGUILayout.Toggle("Auto Update Preview", autoUpdatePreview);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Toggle Slicer Auto Update");
                autoUpdatePreview = nextAutoUpdatePreview;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("XY"))
                {
                    RecordSlicerUndo("Set Slice Plane XY");
                    planeRotation = new Vector3(90f, 0f, 0f);
                }

                if (GUILayout.Button("XZ"))
                {
                    RecordSlicerUndo("Set Slice Plane XZ");
                    planeRotation = Vector3.zero;
                }

                if (GUILayout.Button("YZ"))
                {
                    RecordSlicerUndo("Set Slice Plane YZ");
                    planeRotation = new Vector3(0f, 0f, 90f);
                }
            }

            if (GUILayout.Button("Reset Plane"))
            {
                RecordSlicerUndo("Reset Slice Plane");
                planePosition = Vector3.zero;
                planeRotation = Vector3.zero;
            }
        }

        private void DrawObjectSettings()
        {
            EditorGUILayout.LabelField("Object Adjustment", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            Vector3 nextObjectOffset = EditorGUILayout.Vector3Field("Offset", objectOffset);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Move Slice Source");
                objectOffset = nextObjectOffset;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 nextObjectRotation = EditorGUILayout.Vector3Field("Rotation", objectRotation);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Rotate Slice Source");
                objectRotation = nextObjectRotation;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 nextObjectScale = EditorGUILayout.Vector3Field("Scale", SanitizeScale(objectScale));
            if (EditorGUI.EndChangeCheck())
            {
                RecordSlicerUndo("Scale Slice Source");
                objectScale = SanitizeScale(nextObjectScale);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Object"))
                {
                    RecordSlicerUndo("Reset Slice Source");
                    objectOffset = Vector3.zero;
                    objectRotation = Vector3.zero;
                    objectScale = Vector3.one;
                }

            }
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            outputMeshName = EditorGUILayout.TextField("Mesh Name", outputMeshName);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            targetParent = (Transform)EditorGUILayout.ObjectField("Parent", targetParent, typeof(Transform), true);
            sceneMaterial = (Material)EditorGUILayout.ObjectField("Scene Material", sceneMaterial, typeof(Material), false);
            createSceneObjects = EditorGUILayout.Toggle("Create Scene Objects", createSceneObjects);

            using (new EditorGUI.DisabledScope(!createSceneObjects))
            {
                addMeshColliders = EditorGUILayout.Toggle("Add MeshColliders", addMeshColliders);
            }
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

                using (new EditorGUI.DisabledScope(positivePreviewMesh == null && negativePreviewMesh == null))
                {
                    if (GUILayout.Button("Generate and Save Slice Meshes", GUILayout.Height(32f)))
                    {
                        GenerateAndSave();
                    }

                    if (GUILayout.Button("Export Slice OBJ", GUILayout.Height(32f)))
                    {
                        ExportSliceObj();
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
            if (sourceObject == null)
            {
                GUI.Label(rect, "Assign a source object to preview.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewMaterials();
            EnsurePlaneMesh();

            HandlePreviewInput(rect);
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
            lastPreviewBounds = bounds;
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

            DrawSlicePreviewMeshes();
            DrawPlanePreview(bounds);

            previewUtility.camera.Render();
            Texture result = previewUtility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);

            DrawMeshOverlays(rect);
            DrawPlaneEditorOverlay(rect, bounds);
            DrawPreviewOverlay(rect);
            FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(rect, previewUtility.camera, cameraProjection);
            DrawOrbitGizmo(rect);
        }

        private void DrawSlicePreviewMeshes()
        {
            if ((previewMode == SlicePreviewMode.Positive || previewMode == SlicePreviewMode.Both) && positivePreviewMesh != null)
            {
                DrawPreviewMesh(positivePreviewMesh, GetPreviewMaterialForSide(true));
            }

            if ((previewMode == SlicePreviewMode.Negative || previewMode == SlicePreviewMode.Both) && negativePreviewMesh != null)
            {
                DrawPreviewMesh(negativePreviewMesh, GetPreviewMaterialForSide(false));
            }
        }

        private Material GetPreviewMaterialForSide(bool positiveSide)
        {
            return IsSideKept(positiveSide) ? positivePreviewMaterial : negativePreviewMaterial;
        }

        private bool IsSideKept(bool positiveSide)
        {
            switch (outputMode)
            {
                case SliceOutputMode.KeepPositive:
                    return positiveSide;
                case SliceOutputMode.KeepNegative:
                    return !positiveSide;
                case SliceOutputMode.KeepBoth:
                default:
                    return true;
            }
        }

        private void DrawPlanePreview(Bounds bounds)
        {
            if (planeFrontPreviewMesh == null || planeBackPreviewMesh == null || planeFrontPreviewMaterial == null || planeBackPreviewMaterial == null)
            {
                return;
            }

            float size = GetPlaneDisplaySize(bounds);
            Quaternion rotation = Quaternion.Euler(planeRotation);
            Matrix4x4 matrix = Matrix4x4.TRS(planePosition, rotation, new Vector3(size, 1f, size));
            previewUtility.DrawMesh(planeFrontPreviewMesh, matrix, planeFrontPreviewMaterial, 0);
            previewUtility.DrawMesh(planeBackPreviewMesh, matrix, planeBackPreviewMaterial, 0);
        }

        private Bounds CalculatePreviewBounds()
        {
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

            AddMeshBounds(positivePreviewMesh, ref bounds, ref hasBounds);
            AddMeshBounds(negativePreviewMesh, ref bounds, ref hasBounds);

            for (int i = 0; i < sourcePreviewMeshes.Count; i++)
            {
                SourcePreviewMesh source = sourcePreviewMeshes[i];
                if (source.Mesh == null)
                {
                    continue;
                }

                Bounds sourceBounds = TransformBounds(source.Mesh.bounds, source.Matrix);
                if (!hasBounds)
                {
                    bounds = sourceBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(sourceBounds);
                }
            }

            if (!hasBounds || bounds.size.sqrMagnitude <= 0.0000001f)
            {
                bounds = new Bounds(Vector3.zero, Vector3.one);
            }

            bounds = BuildReferenceCenteredBounds(bounds);
            bounds.Encapsulate(planePosition);
            return bounds;
        }

        private static float GetPlaneDisplaySize(Bounds bounds)
        {
            return Mathf.Max(0.25f, bounds.size.magnitude * 0.85f);
        }

        private Bounds BuildReferenceCenteredBounds(Bounds bounds)
        {
            Vector3 focus = referenceMode == MeshReferenceMode.Center ? bounds.center : ResolveSourcePivot();
            Vector3 extents = Vector3.Max(bounds.max - focus, focus - bounds.min);
            extents.x = Mathf.Abs(extents.x);
            extents.y = Mathf.Abs(extents.y);
            extents.z = Mathf.Abs(extents.z);
            extents = Vector3.Max(extents, Vector3.one * 0.05f);
            return new Bounds(focus, extents * 2f);
        }

        private static void AddMeshBounds(Mesh mesh, ref Bounds bounds, ref bool hasBounds)
        {
            if (mesh == null)
            {
                return;
            }

            if (!hasBounds)
            {
                bounds = mesh.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(mesh.bounds);
            }
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z)
            };

            Bounds transformed = new Bounds(matrix.MultiplyPoint3x4(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                transformed.Encapsulate(matrix.MultiplyPoint3x4(corners[i]));
            }

            return transformed;
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

        private void DrawMeshOverlays(Rect rect)
        {
            if ((!showVertices && !showEdges) || previewUtility == null || previewUtility.camera == null)
            {
                return;
            }

            if (showSourceMesh)
            {
                for (int s = 0; s < sourcePreviewMeshes.Count; s++)
                {
                    SourcePreviewMesh source = sourcePreviewMeshes[s];
                    if (source.Mesh == null)
                    {
                        continue;
                    }

                    if (showEdges)
                    {
                    FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, source.Mesh, source.Matrix, FPMeshPreviewEditorUtility.VertexOverlayColor, 1.25f);
                    }

                    if (showVertices)
                    {
                        FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, source.Mesh, source.Matrix, FPMeshPreviewEditorUtility.VertexOverlayColor, 2f);
                    }
                }
            }

            if ((previewMode == SlicePreviewMode.Positive || previewMode == SlicePreviewMode.Both) && positivePreviewMesh != null)
            {
                if (showEdges)
                {
                    FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, positivePreviewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.EdgeOverlayColor, 1.5f);
                }

                if (showVertices)
                {
                    FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, positivePreviewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.VertexOverlayColor, 2.5f);
                }
            }

            if ((previewMode == SlicePreviewMode.Negative || previewMode == SlicePreviewMode.Both) && negativePreviewMesh != null)
            {
                Color negativeColor = new Color(1f, 0.18f, 0.12f, 1f);
                if (showEdges)
                {
                    FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, negativePreviewMesh, Matrix4x4.identity, negativeColor, 1.5f);
                }

                if (showVertices)
                {
                    FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, negativePreviewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.VertexOverlayColor, 2.5f);
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

        private void DrawPlaneEditorOverlay(Rect rect, Bounds bounds)
        {
            if (previewUtility == null || previewUtility.camera == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(planeRotation);
            float size = GetPlaneDisplaySize(bounds);
            float handleRadius = GetPlaneHandleWorldRadius(rect);
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            float half = size * 0.5f;

            Vector3[] corners =
            {
                planePosition + ((-right - forward) * half),
                planePosition + ((-right + forward) * half),
                planePosition + ((right + forward) * half),
                planePosition + ((right - forward) * half),
                planePosition + ((-right - forward) * half)
            };

            Handles.BeginGUI();
            GUI.BeginClip(rect);
            Rect localRect = new Rect(0f, 0f, rect.width, rect.height);
            DrawProjectedPolyline(rect, localRect, corners, new Color(0.63f, 0.86f, 1f, 0.96f), 2.5f);
            DrawProjectedAxis(rect, localRect, planePosition, right, handleRadius * 0.42f, new Color(0.95f, 0.22f, 0.18f, 0.9f), PlaneHandleMoveX);
            DrawProjectedAxis(rect, localRect, planePosition, up, handleRadius * 0.42f, new Color(0.28f, 0.9f, 0.38f, 0.9f), PlaneHandleMoveY);
            DrawProjectedAxis(rect, localRect, planePosition, forward, handleRadius * 0.42f, new Color(0.25f, 0.5f, 1f, 0.9f), PlaneHandleMoveZ);
            DrawPlaneMoveHandle(rect, localRect);
            DrawPlaneRotationRing(rect, localRect, planePosition, right, handleRadius * 0.88f, new Color(0.95f, 0.22f, 0.18f, 0.88f), PlaneHandleRotateX);
            DrawPlaneRotationRing(rect, localRect, planePosition, up, handleRadius, new Color(0.28f, 0.9f, 0.38f, 0.88f), PlaneHandleRotateY);
            DrawPlaneRotationRing(rect, localRect, planePosition, forward, handleRadius * 1.12f, new Color(0.25f, 0.5f, 1f, 0.88f), PlaneHandleRotateZ);
            DrawPlaneHandleHint(rect, localRect);
            GUI.EndClip();
            Handles.EndGUI();
        }

        private void DrawProjectedAxis(Rect rect, Rect clipRect, Vector3 origin, Vector3 axis, float length, Color color, int handle)
        {
            Vector2 a = WorldToPreviewGuiPoint(rect, origin);
            Vector2 b = WorldToPreviewGuiPoint(rect, origin + (axis.normalized * length));
            if (!IsFinite(a) || !IsFinite(b))
            {
                return;
            }

            a -= rect.position;
            b -= rect.position;
            if (!ClipLineToRect(ref a, ref b, clipRect))
            {
                return;
            }

            bool active = activePlaneHandle == handle;
            bool hover = hoverPlaneHandle == handle;
            if (hover && !active)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.42f);
                Handles.DrawAAPolyLine(10f, new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
            }

            Handles.color = active ? Color.white : color;
            Handles.DrawAAPolyLine(active || hover ? 6f : 4f, new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f));
        }

        private void DrawPlaneMoveHandle(Rect rect, Rect clipRect)
        {
            Vector2 center = WorldToPreviewGuiPoint(rect, planePosition);
            if (!IsFinite(center))
            {
                return;
            }

            center -= rect.position;
            if (!clipRect.Contains(center))
            {
                return;
            }

            Color color = activePlaneHandle == PlaneHandleMove
                ? new Color(1f, 1f, 1f, 0.95f)
                : hoverPlaneHandle == PlaneHandleMove
                    ? new Color(1f, 1f, 1f, 0.9f)
                    : new Color(0.63f, 0.86f, 1f, 0.9f);
            Handles.color = color;
            Vector3 guiCenter = new Vector3(center.x, center.y, 0f);
            float solidRadius = hoverPlaneHandle == PlaneHandleMove || activePlaneHandle == PlaneHandleMove ? 8f : 6f;
            float wireRadius = hoverPlaneHandle == PlaneHandleMove || activePlaneHandle == PlaneHandleMove ? 18f : 13f;
            Handles.DrawSolidDisc(guiCenter, Vector3.forward, solidRadius);
            Handles.DrawWireDisc(guiCenter, Vector3.forward, wireRadius);
        }

        private void DrawPlaneRotationRing(Rect rect, Rect clipRect, Vector3 center, Vector3 normal, float radius, Color color, int handle)
        {
            Vector3[] points = BuildCirclePoints(center, normal, radius, 72);
            bool active = activePlaneHandle == handle;
            bool hover = hoverPlaneHandle == handle;
            if (hover && !active)
            {
                DrawProjectedPolyline(rect, clipRect, points, new Color(1f, 1f, 1f, 0.42f), 9f);
            }

            DrawProjectedPolyline(rect, clipRect, points, active ? Color.white : color, active || hover ? 5.5f : 3.25f);
        }

        private void DrawPlaneHandleHint(Rect rect, Rect clipRect)
        {
            int handle = activePlaneHandle >= 0 ? activePlaneHandle : hoverPlaneHandle;
            if (handle < 0)
            {
                return;
            }

            Vector2 center = WorldToPreviewGuiPoint(rect, planePosition);
            if (!IsFinite(center))
            {
                return;
            }

            center -= rect.position;
            string label = GetPlaneHandleLabel(handle);
            Vector2 size = EditorStyles.helpBox.CalcSize(new GUIContent(label));
            Rect labelRect = new Rect(center.x + 14f, center.y - 13f, size.x + 12f, 24f);
            labelRect.x = Mathf.Clamp(labelRect.x, clipRect.xMin + 2f, Mathf.Max(clipRect.xMin + 2f, clipRect.xMax - labelRect.width - 2f));
            labelRect.y = Mathf.Clamp(labelRect.y, clipRect.yMin + 2f, Mathf.Max(clipRect.yMin + 2f, clipRect.yMax - labelRect.height - 2f));
            GUI.Box(labelRect, label, EditorStyles.helpBox);
        }

        private static string GetPlaneHandleLabel(int handle)
        {
            switch (handle)
            {
                case PlaneHandleMove:
                    return "Move Plane";
                case PlaneHandleMoveX:
                    return "Move X";
                case PlaneHandleMoveY:
                    return "Move Y";
                case PlaneHandleMoveZ:
                    return "Move Z";
                case PlaneHandleRotateX:
                    return "Rotate X";
                case PlaneHandleRotateY:
                    return "Rotate Y";
                case PlaneHandleRotateZ:
                    return "Rotate Z";
                default:
                    return string.Empty;
            }
        }

        private static string GetPlaneHandleUndoLabel(int handle)
        {
            switch (handle)
            {
                case PlaneHandleMove:
                case PlaneHandleMoveX:
                case PlaneHandleMoveY:
                case PlaneHandleMoveZ:
                    return "Move Slice Plane";
                case PlaneHandleRotateX:
                case PlaneHandleRotateY:
                case PlaneHandleRotateZ:
                    return "Rotate Slice Plane";
                default:
                    return "Edit Slice Plane";
            }
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

        private Vector2 WorldToPreviewGuiPoint(Rect rect, Vector3 world)
        {
            Vector3 viewport = previewUtility.camera.WorldToViewportPoint(world);
            if (viewport.z <= 0.001f)
            {
                return new Vector2(float.NaN, float.NaN);
            }

            return new Vector2(rect.x + (viewport.x * rect.width), rect.y + ((1f - viewport.y) * rect.height));
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

        private float GetPlaneHandleWorldRadius(Rect rect)
        {
            return Mathf.Max(0.05f, GetWorldUnitsPerPixel(rect) * PlaneHandleScreenRadius);
        }

        private float GetWorldUnitsPerPixel(Rect rect)
        {
            if (previewUtility == null || previewUtility.camera == null)
            {
                return Mathf.Max(0.001f, lastPreviewBounds.extents.magnitude * 2f) / Mathf.Max(1f, rect.height);
            }

            Camera camera = previewUtility.camera;
            if (camera.orthographic)
            {
                return (camera.orthographicSize * 2f) / Mathf.Max(1f, rect.height);
            }

            float distance = Mathf.Max(0.01f, Vector3.Dot(planePosition - camera.transform.position, camera.transform.forward));
            float verticalWorldSize = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return verticalWorldSize / Mathf.Max(1f, rect.height);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        }

        private static Vector3[] BuildCirclePoints(Vector3 center, Vector3 normal, float radius, int segments)
        {
            Vector3 axisA = Vector3.Cross(normal, Vector3.up);
            if (axisA.sqrMagnitude <= 0.00001f)
            {
                axisA = Vector3.Cross(normal, Vector3.right);
            }

            axisA.Normalize();
            Vector3 axisB = Vector3.Cross(normal, axisA).normalized;
            Vector3[] points = new Vector3[Mathf.Max(8, segments) + 1];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (i / (float)(points.Length - 1)) * Mathf.PI * 2f;
                points[i] = center + ((axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius);
            }

            return points;
        }

        private void DrawPreviewOverlay(Rect rect)
        {
            Rect overlayRect = new Rect(rect.x + 8f, rect.y + 8f, 238f, 108f);
            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(overlayRect.x + 6f, overlayRect.y + 5f, overlayRect.width - 12f, 18f);
            GUI.Label(lineRect, $"Positive Vertices: {GetVertexCount(positivePreviewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Negative Vertices: {GetVertexCount(negativePreviewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Cut Segments: {lastReport.CutSegmentCount}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Filled Holes: {lastReport.FilledHoleCount}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Keep Mode: {outputMode}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Zoom: {previewZoom:0.##}x", EditorStyles.miniLabel);
        }

        private static int GetVertexCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.vertexCount;
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
                EditorGUILayout.LabelField(IsPreviewReady() ? "Preview Ready" : "No Preview", GUILayout.Width(92f));
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("No mesh slicer warnings or errors.", MessageType.None);
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

        private bool IsPreviewReady()
        {
            return positivePreviewMesh != null || negativePreviewMesh != null;
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event current = Event.current;
            if ((current.type == EventType.MouseUp || current.type == EventType.Ignore) && activePlaneHandle >= 0)
            {
                if (activePlaneHandleUndoRecorded)
                {
                    Undo.FlushUndoRecordObjects();
                }

                activePlaneHandle = -1;
                activePlaneHandleUndoRecorded = false;
                current.Use();
                Repaint();
                return;
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.Ignore) && activeOrbitAxis >= 0)
            {
                activeOrbitAxis = -1;
                current.Use();
                return;
            }

            if (!rect.Contains(current.mousePosition))
            {
                if (hoverPlaneHandle >= 0)
                {
                    hoverPlaneHandle = -1;
                    Repaint();
                }

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

            bool mouseOverOrbitGizmo = GetOrbitGizmoRect(rect).Contains(current.mousePosition);
            int currentPlaneHover = mouseOverOrbitGizmo ? -1 : GetPlaneHandleAtPosition(rect, current.mousePosition, hoverPlaneHandle);
            if (activePlaneHandle < 0 && hoverPlaneHandle != currentPlaneHover)
            {
                hoverPlaneHandle = currentPlaneHover;
                Repaint();
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (currentPlaneHover >= 0)
                {
                    activePlaneHandle = currentPlaneHover;
                    activePlaneHandleUndoRecorded = false;
                    hoverPlaneHandle = currentPlaneHover;
                    current.Use();
                    return;
                }
            }

            if (current.type == EventType.MouseDrag && current.button == 0 && activePlaneHandle >= 0)
            {
                ApplyPlaneHandleDrag(rect, current.delta);
                hoverPlaneHandle = activePlaneHandle;
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

        private int GetPlaneHandleAtPosition(Rect rect, Vector2 mousePosition, int preferredHandle)
        {
            if (previewUtility == null || previewUtility.camera == null)
            {
                return -1;
            }

            if (preferredHandle >= 0 && IsPlaneHandleAtPosition(rect, mousePosition, preferredHandle))
            {
                return preferredHandle;
            }

            if (IsPlaneHandleAtPosition(rect, mousePosition, PlaneHandleMove))
            {
                return PlaneHandleMove;
            }

            if (IsPlaneHandleAtPosition(rect, mousePosition, PlaneHandleMoveX))
            {
                return PlaneHandleMoveX;
            }

            if (IsPlaneHandleAtPosition(rect, mousePosition, PlaneHandleMoveY))
            {
                return PlaneHandleMoveY;
            }

            if (IsPlaneHandleAtPosition(rect, mousePosition, PlaneHandleMoveZ))
            {
                return PlaneHandleMoveZ;
            }

            if (IsPlaneHandleAtPosition(rect, mousePosition, PlaneHandleRotateX))
            {
                return PlaneHandleRotateX;
            }

            if (IsPlaneHandleAtPosition(rect, mousePosition, PlaneHandleRotateY))
            {
                return PlaneHandleRotateY;
            }

            if (IsPlaneHandleAtPosition(rect, mousePosition, PlaneHandleRotateZ))
            {
                return PlaneHandleRotateZ;
            }

            return -1;
        }

        private bool IsPlaneHandleAtPosition(Rect rect, Vector2 mousePosition, int handle)
        {
            Vector2 center = WorldToPreviewGuiPoint(rect, planePosition);
            if (handle == PlaneHandleMove)
            {
                return IsFinite(center) && Vector2.Distance(mousePosition, center) <= PlaneMoveHandlePickRadius;
            }

            Quaternion rotation = Quaternion.Euler(planeRotation);
            float handleRadius = GetPlaneHandleWorldRadius(rect);
            if (handle == PlaneHandleMoveX)
            {
                return DistanceToProjectedAxis(rect, mousePosition, planePosition, rotation * Vector3.right, handleRadius * 0.42f) <= PlaneAxisMovePickRadius;
            }

            if (handle == PlaneHandleMoveY)
            {
                return DistanceToProjectedAxis(rect, mousePosition, planePosition, rotation * Vector3.up, handleRadius * 0.42f) <= PlaneAxisMovePickRadius;
            }

            if (handle == PlaneHandleMoveZ)
            {
                return DistanceToProjectedAxis(rect, mousePosition, planePosition, rotation * Vector3.forward, handleRadius * 0.42f) <= PlaneAxisMovePickRadius;
            }

            if (handle == PlaneHandleRotateX)
            {
                return DistanceToProjectedRing(rect, mousePosition, planePosition, rotation * Vector3.right, handleRadius * 0.88f) <= PlaneRotateHandlePickRadius;
            }

            if (handle == PlaneHandleRotateY)
            {
                return DistanceToProjectedRing(rect, mousePosition, planePosition, rotation * Vector3.up, handleRadius) <= PlaneRotateHandlePickRadius;
            }

            if (handle == PlaneHandleRotateZ)
            {
                return DistanceToProjectedRing(rect, mousePosition, planePosition, rotation * Vector3.forward, handleRadius * 1.12f) <= PlaneRotateHandlePickRadius;
            }

            return false;
        }

        private float DistanceToProjectedAxis(Rect rect, Vector2 mousePosition, Vector3 origin, Vector3 axis, float length)
        {
            Vector2 a = WorldToPreviewGuiPoint(rect, origin);
            Vector2 b = WorldToPreviewGuiPoint(rect, origin + (axis.normalized * length));
            if (!IsFinite(a) || !IsFinite(b))
            {
                return float.PositiveInfinity;
            }

            return DistancePointToSegment(mousePosition, a, b);
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

        private void ApplyPlaneHandleDrag(Rect rect, Vector2 delta)
        {
            Camera camera = previewUtility.camera;
            if (activePlaneHandle == PlaneHandleMove)
            {
                RecordActivePlaneHandleUndo();
                float unitsPerPixel = GetWorldUnitsPerPixel(rect);
                Vector3 cameraRight = camera.transform.right;
                Vector3 cameraUp = camera.transform.up;
                planePosition += ((cameraRight * delta.x) - (cameraUp * delta.y)) * unitsPerPixel * 1.8f;
                previewDirty = true;
                if (autoUpdatePreview)
                {
                    RebuildPreview();
                }

                return;
            }

            Quaternion rotation = Quaternion.Euler(planeRotation);
            if (activePlaneHandle == PlaneHandleMoveX || activePlaneHandle == PlaneHandleMoveY || activePlaneHandle == PlaneHandleMoveZ)
            {
                RecordActivePlaneHandleUndo();
                Vector3 axisPlaneHandle = activePlaneHandle == PlaneHandleMoveX
                    ? rotation * Vector3.right
                    : activePlaneHandle == PlaneHandleMoveY
                        ? rotation * Vector3.up
                        : rotation * Vector3.forward;

                Vector2 start = WorldToPreviewGuiPoint(rect, planePosition);
                Vector2 end = WorldToPreviewGuiPoint(rect, planePosition + axisPlaneHandle);
                Vector2 screenAxis = end - start;
                if (screenAxis.sqrMagnitude > 0.0001f)
                {
                    screenAxis.Normalize();
                    float pixels = Vector2.Dot(delta, screenAxis);
                    planePosition += axisPlaneHandle.normalized * pixels * GetWorldUnitsPerPixel(rect) * 1.8f;
                    previewDirty = true;
                    if (autoUpdatePreview)
                    {
                        RebuildPreview();
                    }
                }

                return;
            }

            RecordActivePlaneHandleUndo();
            Vector3 axis = activePlaneHandle == PlaneHandleRotateX
                ? rotation * Vector3.right
                : activePlaneHandle == PlaneHandleRotateY
                    ? rotation * Vector3.up
                    : rotation * Vector3.forward;
            float degrees = (delta.x + delta.y) * 0.35f;
            planeRotation = (Quaternion.AngleAxis(degrees, axis) * rotation).eulerAngles;
            previewDirty = true;
            if (autoUpdatePreview)
            {
                RebuildPreview();
            }
        }

        private void RecordActivePlaneHandleUndo()
        {
            if (activePlaneHandleUndoRecorded)
            {
                return;
            }

            RecordSlicerUndo(GetPlaneHandleUndoLabel(activePlaneHandle));
            activePlaneHandleUndoRecorded = true;
        }

        private void RebuildPreview()
        {
            warnings.Clear();
            errors.Clear();
            previewDirty = false;
            CleanupPreviewMeshes();

            SourceSummary summary = ResolveSource();
            if (!summary.HasMesh)
            {
                errors.Add("Assign a source object with at least one mesh before slicing.");
                Repaint();
                return;
            }

            if (summary.HasUnreadableMesh)
            {
                errors.Add("One or more source meshes are not readable. Enable Read/Write on their import settings and rebuild the preview.");
                Repaint();
                return;
            }

            FPMeshSliceSettings settings = new FPMeshSliceSettings
            {
                MeshName = string.IsNullOrWhiteSpace(outputMeshName) ? "FP_SlicedMesh" : outputMeshName.Trim(),
                PlaneNormal = GetPlaneNormal(),
                PlaneDistance = Vector3.Dot(GetPlaneNormal(), planePosition),
                RepairSliceHoles = repairSliceHoles
            };

            FPMeshSliceResult result = FPMeshSlicer.Build(sourceMeshes, settings, out lastReport);
            positivePreviewMesh = result.PositiveMesh;
            negativePreviewMesh = result.NegativeMesh;
            warnings.AddRange(lastReport.Warnings);
            errors.AddRange(lastReport.Errors);
            Repaint();
        }

        private void GenerateAndSave()
        {
            if (!IsPreviewReady())
            {
                RebuildPreview();
            }

            if (!IsPreviewReady())
            {
                return;
            }

            string baseName = string.IsNullOrWhiteSpace(outputMeshName) ? "FP_SlicedMesh" : outputMeshName.Trim();
            Mesh savedPositive = null;
            Mesh savedNegative = null;

            if (positivePreviewMesh != null && IsSideKept(true))
            {
                Mesh meshToSave = Object.Instantiate(positivePreviewMesh);
                meshToSave.name = baseName + "_Positive";
                savedPositive = SaveMeshAsset(meshToSave, outputFolder, meshToSave.name, out string message);
                HandleSaveMessage(meshToSave, savedPositive, message);
                lastSavedPositiveMesh = savedPositive;
            }

            if (negativePreviewMesh != null && IsSideKept(false))
            {
                Mesh meshToSave = Object.Instantiate(negativePreviewMesh);
                meshToSave.name = baseName + "_Negative";
                savedNegative = SaveMeshAsset(meshToSave, outputFolder, meshToSave.name, out string message);
                HandleSaveMessage(meshToSave, savedNegative, message);
                lastSavedNegativeMesh = savedNegative;
            }

            if (createSceneObjects)
            {
                CreateSceneObject(savedPositive, "Positive");
                CreateSceneObject(savedNegative, "Negative");
            }

            Repaint();
        }

        private void ExportSliceObj()
        {
            if (!IsPreviewReady())
            {
                RebuildPreview();
            }

            if (!IsPreviewReady())
            {
                return;
            }

            string baseName = string.IsNullOrWhiteSpace(outputMeshName) ? "FP_SlicedMesh" : outputMeshName.Trim();
            Material[] exportMaterials = sceneMaterial == null ? null : new[] { sceneMaterial };
            var sources = new List<FPMeshObjExportSource>();

            if (positivePreviewMesh != null && IsSideKept(true))
            {
                sources.Add(new FPMeshObjExportSource(baseName + "_Positive", positivePreviewMesh, Matrix4x4.identity, exportMaterials));
            }

            if (negativePreviewMesh != null && IsSideKept(false))
            {
                sources.Add(new FPMeshObjExportSource(baseName + "_Negative", negativePreviewMesh, Matrix4x4.identity, exportMaterials));
            }

            if (sources.Count == 0)
            {
                EditorUtility.DisplayDialog("Export Slice OBJ", "No slice mesh is available for the current keep mode.", "OK");
                return;
            }

            FPMeshObjExportUtility.ExportSourcesWithDialog(
                sources,
                baseName,
                new FPMeshObjExportOptions
                {
                    ExportMaterials = true,
                    CopyTextures = true
                });
        }

        private void HandleSaveMessage(Mesh unsavedMesh, Mesh savedMesh, string message)
        {
            if (savedMesh == null)
            {
                errors.Add(string.IsNullOrWhiteSpace(message) ? "Mesh generation succeeded, but the mesh asset could not be saved." : message);
                if (unsavedMesh != null && !EditorUtility.IsPersistent(unsavedMesh))
                {
                    DestroyImmediate(unsavedMesh);
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                warnings.Add(message);
            }
        }

        private void CreateSceneObject(Mesh mesh, string suffix)
        {
            if (mesh == null)
            {
                return;
            }

            GameObject go = new GameObject(mesh.name);
            Undo.RegisterCreatedObjectUndo(go, "Create Sliced Mesh");

            Transform parent = ResolveOutputParent();
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
                go.transform.SetParent(parent, false);
            }

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            if (sceneMaterial != null)
            {
                meshRenderer.sharedMaterial = sceneMaterial;
            }

            if (addMeshColliders)
            {
                MeshCollider meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            warnings.Add($"Scene object created ({suffix}): {go.name}");
        }

        private Transform ResolveOutputParent()
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

            bool hadSource = sourceObject != null;
            SyncSelectedSource();
            if (!hadSource && sourceObject != null)
            {
                AlignPlaneToReference(false);
            }
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
            outputMeshName = $"{safeName}_Sliced";
        }

        private void AlignPlaneToReference(bool recordUndo = true)
        {
            SourceSummary summary = ResolveSource();
            if (!summary.HasMesh && referenceMode == MeshReferenceMode.Center)
            {
                if (recordUndo)
                {
                    RecordSlicerUndo("Align Slice Plane");
                }

                planePosition = Vector3.zero;
                return;
            }

            if (recordUndo)
            {
                RecordSlicerUndo("Align Slice Plane");
            }

            planePosition = referenceMode == MeshReferenceMode.Center && summary.HasMesh
                ? summary.Bounds.center
                : ResolveSourcePivot();
        }

        private Vector3 ResolveSourcePivot()
        {
            return GetObjectAdjustmentMatrix().MultiplyPoint3x4(Vector3.zero);
        }

        private SourceSummary ResolveSource()
        {
            sourcePreviewMeshes.Clear();
            sourceMeshes.Clear();

            var summary = new SourceSummary();
            if (sourceObject == null)
            {
                return summary;
            }

            Matrix4x4 objectMatrix = GetObjectAdjustmentMatrix();
            if (sourceObject is Mesh mesh)
            {
                AddSourceMesh(mesh, objectMatrix, null, ref summary);
            }
            else if (sourceObject is GameObject gameObject)
            {
                AddGameObjectSources(gameObject, objectMatrix, ref summary);
            }
            else if (sourceObject is MeshFilter meshFilter)
            {
                Material[] materials = ResolveRendererMaterials(meshFilter.GetComponent<MeshRenderer>());
                AddSourceMesh(meshFilter.sharedMesh, objectMatrix, materials, ref summary);
            }
            else if (sourceObject is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                AddSourceMesh(skinnedMeshRenderer.sharedMesh, objectMatrix, skinnedMeshRenderer.sharedMaterials, ref summary);
            }
            else if (sourceObject is Component component)
            {
                AddGameObjectSources(component.gameObject, objectMatrix, ref summary);
            }

            return summary;
        }

        private void AddGameObjectSources(GameObject gameObject, Matrix4x4 objectMatrix, ref SourceSummary summary)
        {
            if (gameObject == null)
            {
                return;
            }

            Matrix4x4 rootToLocal = gameObject.transform.worldToLocalMatrix;
            MeshFilter[] meshFilters = includeChildren
                ? gameObject.GetComponentsInChildren<MeshFilter>(true)
                : gameObject.GetComponents<MeshFilter>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                Material[] materials = ResolveRendererMaterials(meshFilter.GetComponent<MeshRenderer>());
                AddSourceMesh(meshFilter.sharedMesh, objectMatrix * rootToLocal * meshFilter.transform.localToWorldMatrix, materials, ref summary);
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers = includeChildren
                ? gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                : gameObject.GetComponents<SkinnedMeshRenderer>();
            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedMeshRenderers[i];
                AddSourceMesh(renderer.sharedMesh, objectMatrix * rootToLocal * renderer.transform.localToWorldMatrix, renderer.sharedMaterials, ref summary);
            }
        }

        private void AddSourceMesh(Mesh mesh, Matrix4x4 matrix, Material[] materials, ref SourceSummary summary)
        {
            if (mesh == null)
            {
                return;
            }

            sourcePreviewMeshes.Add(new SourcePreviewMesh(mesh, matrix, materials));
            sourceMeshes.Add(new SliceSourceMesh(mesh, matrix));
            summary.HasMesh = true;
            summary.MeshCount++;
            summary.VertexCount += mesh.vertexCount;
            summary.TriangleCount += EstimateTriangleCount(mesh);

            if (!mesh.isReadable)
            {
                summary.HasUnreadableMesh = true;
                return;
            }

            Bounds transformedBounds = TransformBounds(mesh.bounds, matrix);
            if (summary.MeshCount == 1)
            {
                summary.Bounds = transformedBounds;
            }
            else
            {
                summary.Bounds.Encapsulate(transformedBounds);
            }
        }

        private Matrix4x4 GetObjectAdjustmentMatrix()
        {
            return Matrix4x4.TRS(objectOffset, Quaternion.Euler(objectRotation), SanitizeScale(objectScale));
        }

        private Vector3 GetPlaneNormal()
        {
            return (Quaternion.Euler(planeRotation) * Vector3.up).normalized;
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 0.0001f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 0.0001f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 0.0001f : scale.z);
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
            Shader shader = FindPreviewShader();
            if (sourcePreviewMaterial == null)
            {
                sourcePreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(0.74f, 0.74f, 0.74f, 0.32f)
                };
                ConfigureTransparentPreviewMaterial(sourcePreviewMaterial, 0.32f);
            }

            if (positivePreviewMaterial == null)
            {
                positivePreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = FPMeshPreviewEditorUtility.PreviewMeshColor
                };
                ConfigureOpaquePreviewMaterial(positivePreviewMaterial);
            }

            if (negativePreviewMaterial == null)
            {
                negativePreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(1f, 0.08f, 0.06f, 1f)
                };
                ConfigureOpaquePreviewMaterial(negativePreviewMaterial);
            }

            if (planeFrontPreviewMaterial == null)
            {
                planeFrontPreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(0.23f, 0.5f, 1f, 0.32f)
                };
                ConfigureTransparentPreviewMaterial(planeFrontPreviewMaterial, 0.32f);
            }

            if (planeBackPreviewMaterial == null)
            {
                planeBackPreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    color = new Color(1f, 0.72f, 0.16f, 0.26f)
                };
                ConfigureTransparentPreviewMaterial(planeBackPreviewMaterial, 0.26f);
            }
        }

        private void EnsurePlaneMesh()
        {
            if (planeFrontPreviewMesh != null && planeBackPreviewMesh != null)
            {
                return;
            }

            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f)
            };

            Vector2[] uv = { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            planeFrontPreviewMesh = new Mesh
            {
                name = "FP_SlicerPlaneFrontPreview",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                uv = uv,
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            planeFrontPreviewMesh.RecalculateBounds();

            planeBackPreviewMesh = new Mesh
            {
                name = "FP_SlicerPlaneBackPreview",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                normals = new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down },
                uv = uv,
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            planeBackPreviewMesh.RecalculateBounds();
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

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Back);
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

        private static void ConfigureOpaquePreviewMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            Color color = material.color;
            color.a = 1f;
            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0f);
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
                material.SetFloat("_Mode", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", (int)CullMode.Back);
            }

            material.SetOverrideTag("RenderType", "Opaque");
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        private void CleanupPreviewMeshes()
        {
            DestroyPreviewMesh(ref positivePreviewMesh, lastSavedPositiveMesh);
            DestroyPreviewMesh(ref negativePreviewMesh, lastSavedNegativeMesh);
        }

        private static void DestroyPreviewMesh(ref Mesh mesh, Mesh lastSavedMesh)
        {
            if (mesh != null && !EditorUtility.IsPersistent(mesh) && mesh != lastSavedMesh)
            {
                DestroyImmediate(mesh);
            }

            mesh = null;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
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

            string safeName = string.IsNullOrWhiteSpace(meshName) ? "FP_SlicedMesh" : meshName.Trim();
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

    internal struct FPMeshSliceSettings
    {
        public string MeshName;
        public Vector3 PlaneNormal;
        public float PlaneDistance;
        public bool RepairSliceHoles;
    }

    internal struct FPMeshSliceReport
    {
        public int SourceTriangleCount;
        public int PositiveTriangleCount;
        public int NegativeTriangleCount;
        public int CutSegmentCount;
        public int FilledHoleCount;
        public readonly List<string> Warnings;
        public readonly List<string> Errors;

        public FPMeshSliceReport(int sourceTriangleCount)
        {
            SourceTriangleCount = sourceTriangleCount;
            PositiveTriangleCount = 0;
            NegativeTriangleCount = 0;
            CutSegmentCount = 0;
            FilledHoleCount = 0;
            Warnings = new List<string>();
            Errors = new List<string>();
        }
    }

    internal struct FPMeshSliceResult
    {
        public Mesh PositiveMesh;
        public Mesh NegativeMesh;
    }

    internal static class FPMeshSlicer
    {
        private const float Epsilon = 0.00001f;

        private struct SliceVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 Uv;
            public float Distance;

            public SliceVertex(Vector3 position, Vector3 normal, Vector2 uv, float distance)
            {
                Position = position;
                Normal = normal;
                Uv = uv;
                Distance = distance;
            }
        }

        private struct CapSegment
        {
            public Vector3 A;
            public Vector3 B;

            public CapSegment(Vector3 a, Vector3 b)
            {
                A = a;
                B = b;
            }
        }

        private sealed class MeshPart
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public int TriangleCount => Triangles.Count / 3;

            public void AddPolygon(IReadOnlyList<SliceVertex> polygon, bool flip)
            {
                if (polygon == null || polygon.Count < 3)
                {
                    return;
                }

                int startIndex = Vertices.Count;
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vertices.Add(polygon[i].Position);
                    Normals.Add(polygon[i].Normal.sqrMagnitude <= Epsilon ? Vector3.up : polygon[i].Normal.normalized);
                    Uv.Add(polygon[i].Uv);
                }

                for (int i = 1; i < polygon.Count - 1; i++)
                {
                    if (flip)
                    {
                        Triangles.Add(startIndex);
                        Triangles.Add(startIndex + i + 1);
                        Triangles.Add(startIndex + i);
                    }
                    else
                    {
                        Triangles.Add(startIndex);
                        Triangles.Add(startIndex + i);
                        Triangles.Add(startIndex + i + 1);
                    }
                }
            }

            public void AddCap(IReadOnlyList<Vector3> points, Vector3 normal, bool flip)
            {
                if (points == null || points.Count < 3)
                {
                    return;
                }

                int startIndex = Vertices.Count;
                Vector3 capNormal = flip ? -normal : normal;
                Vector2[] capUv = BuildCapUv(points, normal);
                Vector3 center = Vector3.zero;
                for (int i = 0; i < points.Count; i++)
                {
                    center += points[i];
                }

                center /= points.Count;

                Vertices.Add(center);
                Normals.Add(capNormal);
                Uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i < points.Count; i++)
                {
                    Vertices.Add(points[i]);
                    Normals.Add(capNormal);
                    Uv.Add(capUv[i]);
                }

                for (int i = 0; i < points.Count; i++)
                {
                    int currentIndex = startIndex + 1 + i;
                    int nextIndex = startIndex + 1 + ((i + 1) % points.Count);
                    if (flip)
                    {
                        Triangles.Add(startIndex);
                        Triangles.Add(nextIndex);
                        Triangles.Add(currentIndex);
                    }
                    else
                    {
                        Triangles.Add(startIndex);
                        Triangles.Add(currentIndex);
                        Triangles.Add(nextIndex);
                    }
                }
            }

            public Mesh ToMesh(string name)
            {
                if (Vertices.Count < 3 || Triangles.Count < 3)
                {
                    return null;
                }

                Mesh mesh = new Mesh
                {
                    name = name,
                    indexFormat = Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, Uv);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }

        internal static FPMeshSliceResult Build(IReadOnlyList<FPMeshSlicerWindow.SliceSourceMesh> sources, FPMeshSliceSettings settings, out FPMeshSliceReport report)
        {
            settings.PlaneNormal = settings.PlaneNormal.sqrMagnitude <= Epsilon ? Vector3.up : settings.PlaneNormal.normalized;
            report = new FPMeshSliceReport(CountTriangles(sources));

            if (sources == null || sources.Count == 0)
            {
                report.Errors.Add("No source meshes were found.");
                return new FPMeshSliceResult();
            }

            var positive = new MeshPart();
            var negative = new MeshPart();
            var capSegments = new List<CapSegment>();

            for (int i = 0; i < sources.Count; i++)
            {
                SliceSource(sources[i].Mesh, sources[i].Matrix, settings, positive, negative, capSegments);
            }

            if (settings.RepairSliceHoles && capSegments.Count > 0)
            {
                float tolerance = EstimateTolerance(capSegments);
                List<List<Vector3>> capLoops = BuildCapLoops(capSegments, tolerance, settings.PlaneNormal);
                for (int i = 0; i < capLoops.Count; i++)
                {
                    positive.AddCap(capLoops[i], settings.PlaneNormal, true);
                    negative.AddCap(capLoops[i], settings.PlaneNormal, false);
                }

                report.FilledHoleCount = capLoops.Count;
                if (capLoops.Count == 0)
                {
                    report.Warnings.Add("The slicer found cut segments, but they could not be assembled into closed hole loops.");
                }
            }
            else if (settings.RepairSliceHoles)
            {
                report.Warnings.Add("The plane did not produce any cut segments to repair.");
            }

            string safeName = string.IsNullOrWhiteSpace(settings.MeshName) ? "FP_SlicedMesh" : settings.MeshName.Trim();
            Mesh positiveMesh = positive.ToMesh(safeName + "_Positive");
            Mesh negativeMesh = negative.ToMesh(safeName + "_Negative");

            report.PositiveTriangleCount = positive.TriangleCount;
            report.NegativeTriangleCount = negative.TriangleCount;
            report.CutSegmentCount = capSegments.Count;

            if (positiveMesh == null)
            {
                report.Warnings.Add("The positive side is empty for the current plane.");
            }

            if (negativeMesh == null)
            {
                report.Warnings.Add("The negative side is empty for the current plane.");
            }

            return new FPMeshSliceResult
            {
                PositiveMesh = positiveMesh,
                NegativeMesh = negativeMesh
            };
        }

        private static void SliceSource(
            Mesh mesh,
            Matrix4x4 matrix,
            FPMeshSliceSettings settings,
            MeshPart positive,
            MeshPart negative,
            List<CapSegment> capSegments)
        {
            if (mesh == null || !mesh.isReadable)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    SliceVertex a = BuildSliceVertex(triangles[i], vertices, normals, uv, matrix, normalMatrix, settings);
                    SliceVertex b = BuildSliceVertex(triangles[i + 1], vertices, normals, uv, matrix, normalMatrix, settings);
                    SliceVertex c = BuildSliceVertex(triangles[i + 2], vertices, normals, uv, matrix, normalMatrix, settings);

                    SliceVertex[] triangle = { a, b, c };
                    AddCutSegment(triangle, capSegments);
                    List<SliceVertex> positivePolygon = ClipPolygon(triangle, true);
                    List<SliceVertex> negativePolygon = ClipPolygon(triangle, false);

                    positive.AddPolygon(positivePolygon, false);
                    negative.AddPolygon(negativePolygon, false);
                }
            }
        }

        private static SliceVertex BuildSliceVertex(
            int index,
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uv,
            Matrix4x4 matrix,
            Matrix4x4 normalMatrix,
            FPMeshSliceSettings settings)
        {
            Vector3 position = matrix.MultiplyPoint3x4(vertices[index]);
            Vector3 normal = normals != null && normals.Length == vertices.Length
                ? normalMatrix.MultiplyVector(normals[index]).normalized
                : settings.PlaneNormal;
            Vector2 texCoord = uv != null && uv.Length == vertices.Length ? uv[index] : Vector2.zero;
            float distance = Vector3.Dot(settings.PlaneNormal, position) - settings.PlaneDistance;
            return new SliceVertex(position, normal, texCoord, distance);
        }

        private static void AddCutSegment(IReadOnlyList<SliceVertex> triangle, List<CapSegment> capSegments)
        {
            if (capSegments == null || triangle == null || triangle.Count != 3)
            {
                return;
            }

            if (Mathf.Abs(triangle[0].Distance) <= Epsilon &&
                Mathf.Abs(triangle[1].Distance) <= Epsilon &&
                Mathf.Abs(triangle[2].Distance) <= Epsilon)
            {
                return;
            }

            var points = new List<Vector3>(2);
            for (int i = 0; i < triangle.Count; i++)
            {
                SliceVertex a = triangle[i];
                SliceVertex b = triangle[(i + 1) % triangle.Count];
                bool aOnPlane = Mathf.Abs(a.Distance) <= Epsilon;
                bool bOnPlane = Mathf.Abs(b.Distance) <= Epsilon;

                if (aOnPlane)
                {
                    AddUniquePoint(points, a.Position, Epsilon * Epsilon);
                }

                if (!aOnPlane && !bOnPlane && ((a.Distance > 0f && b.Distance < 0f) || (a.Distance < 0f && b.Distance > 0f)))
                {
                    AddUniquePoint(points, Intersect(a, b).Position, Epsilon * Epsilon);
                }
            }

            if (points.Count >= 2 && (points[0] - points[1]).sqrMagnitude > Epsilon * Epsilon)
            {
                capSegments.Add(new CapSegment(points[0], points[1]));
            }
        }

        private static void AddUniquePoint(List<Vector3> points, Vector3 point, float sqrTolerance)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - point).sqrMagnitude <= sqrTolerance)
                {
                    return;
                }
            }

            points.Add(point);
        }

        private static List<SliceVertex> ClipPolygon(IReadOnlyList<SliceVertex> input, bool keepPositive)
        {
            var output = new List<SliceVertex>();
            if (input == null || input.Count == 0)
            {
                return output;
            }

            SliceVertex previous = input[input.Count - 1];
            bool previousInside = IsInside(previous.Distance, keepPositive);

            for (int i = 0; i < input.Count; i++)
            {
                SliceVertex current = input[i];
                bool currentInside = IsInside(current.Distance, keepPositive);

                if (previousInside && currentInside)
                {
                    output.Add(current);
                }
                else if (previousInside && !currentInside)
                {
                    SliceVertex intersection = Intersect(previous, current);
                    output.Add(intersection);
                }
                else if (!previousInside && currentInside)
                {
                    SliceVertex intersection = Intersect(previous, current);
                    output.Add(intersection);
                    output.Add(current);
                }

                previous = current;
                previousInside = currentInside;
            }

            return output;
        }

        private static bool IsInside(float distance, bool keepPositive)
        {
            return keepPositive ? distance >= -Epsilon : distance <= Epsilon;
        }

        private static SliceVertex Intersect(SliceVertex a, SliceVertex b)
        {
            float denominator = a.Distance - b.Distance;
            float t = Mathf.Abs(denominator) <= Epsilon ? 0f : Mathf.Clamp01(a.Distance / denominator);
            Vector3 position = Vector3.LerpUnclamped(a.Position, b.Position, t);
            Vector3 normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized;
            Vector2 uv = Vector2.LerpUnclamped(a.Uv, b.Uv, t);
            return new SliceVertex(position, normal, uv, 0f);
        }

        private static int CountTriangles(IReadOnlyList<FPMeshSlicerWindow.SliceSourceMesh> sources)
        {
            if (sources == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                Mesh mesh = sources[i].Mesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    count += (int)(mesh.GetIndexCount(s) / 3);
                }
            }

            return count;
        }

        private static Vector2[] BuildCapUv(IReadOnlyList<Vector3> points, Vector3 normal)
        {
            Vector3 axisA = Vector3.Cross(normal, Vector3.up);
            if (axisA.sqrMagnitude <= Epsilon)
            {
                axisA = Vector3.Cross(normal, Vector3.right);
            }

            axisA.Normalize();
            Vector3 axisB = Vector3.Cross(normal, axisA).normalized;

            float minA = float.PositiveInfinity;
            float maxA = float.NegativeInfinity;
            float minB = float.PositiveInfinity;
            float maxB = float.NegativeInfinity;
            Vector2[] projected = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                projected[i] = new Vector2(Vector3.Dot(points[i], axisA), Vector3.Dot(points[i], axisB));
                minA = Mathf.Min(minA, projected[i].x);
                maxA = Mathf.Max(maxA, projected[i].x);
                minB = Mathf.Min(minB, projected[i].y);
                maxB = Mathf.Max(maxB, projected[i].y);
            }

            Vector2[] capUv = new Vector2[points.Count];
            float width = Mathf.Max(Epsilon, maxA - minA);
            float height = Mathf.Max(Epsilon, maxB - minB);
            for (int i = 0; i < points.Count; i++)
            {
                capUv[i] = new Vector2(
                    Mathf.InverseLerp(minA, minA + width, projected[i].x),
                    Mathf.InverseLerp(minB, minB + height, projected[i].y));
            }

            return capUv;
        }

        private static List<List<Vector3>> BuildCapLoops(IReadOnlyList<CapSegment> segments, float tolerance, Vector3 normal)
        {
            var loops = new List<List<Vector3>>();
            if (segments == null || segments.Count == 0)
            {
                return loops;
            }

            float safeTolerance = Mathf.Max(Epsilon, tolerance);
            var pointsByKey = new Dictionary<Vector3Int, Vector3>();
            var adjacency = new Dictionary<Vector3Int, List<Vector3Int>>();
            var usedEdges = new HashSet<string>();

            for (int i = 0; i < segments.Count; i++)
            {
                Vector3Int a = QuantizePoint(segments[i].A, safeTolerance);
                Vector3Int b = QuantizePoint(segments[i].B, safeTolerance);
                if (a == b)
                {
                    continue;
                }

                string edgeKey = BuildEdgeKey(a, b);
                if (usedEdges.Contains(edgeKey))
                {
                    continue;
                }

                usedEdges.Add(edgeKey);
                pointsByKey[a] = segments[i].A;
                pointsByKey[b] = segments[i].B;
                AddAdjacency(adjacency, a, b);
                AddAdjacency(adjacency, b, a);
            }

            usedEdges.Clear();
            foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in adjacency)
            {
                Vector3Int start = pair.Key;
                List<Vector3Int> neighbors = pair.Value;
                for (int i = 0; i < neighbors.Count; i++)
                {
                    string firstEdge = BuildEdgeKey(start, neighbors[i]);
                    if (usedEdges.Contains(firstEdge))
                    {
                        continue;
                    }

                    var loopKeys = new List<Vector3Int> { start };
                    Vector3Int previous = start;
                    Vector3Int current = neighbors[i];
                    usedEdges.Add(firstEdge);

                    int guard = Mathf.Max(8, adjacency.Count * 4);
                    while (guard-- > 0)
                    {
                        loopKeys.Add(current);
                        if (current == start)
                        {
                            break;
                        }

                        if (!adjacency.TryGetValue(current, out List<Vector3Int> currentNeighbors))
                        {
                            break;
                        }

                        bool foundNext = false;
                        for (int n = 0; n < currentNeighbors.Count; n++)
                        {
                            Vector3Int candidate = currentNeighbors[n];
                            string edgeKey = BuildEdgeKey(current, candidate);
                            if (usedEdges.Contains(edgeKey))
                            {
                                continue;
                            }

                            if (candidate == previous && currentNeighbors.Count > 1)
                            {
                                continue;
                            }

                            usedEdges.Add(edgeKey);
                            previous = current;
                            current = candidate;
                            foundNext = true;
                            break;
                        }

                        if (!foundNext)
                        {
                            break;
                        }
                    }

                    if (loopKeys.Count >= 4 && loopKeys[loopKeys.Count - 1] == start)
                    {
                        loopKeys.RemoveAt(loopKeys.Count - 1);
                        var loop = new List<Vector3>(loopKeys.Count);
                        for (int k = 0; k < loopKeys.Count; k++)
                        {
                            loop.Add(pointsByKey[loopKeys[k]]);
                        }

                        OrientLoop(loop, normal);
                        loops.Add(loop);
                    }
                }
            }

            return loops;
        }

        private static void AddAdjacency(Dictionary<Vector3Int, List<Vector3Int>> adjacency, Vector3Int key, Vector3Int neighbor)
        {
            if (!adjacency.TryGetValue(key, out List<Vector3Int> neighbors))
            {
                neighbors = new List<Vector3Int>();
                adjacency[key] = neighbors;
            }

            if (!neighbors.Contains(neighbor))
            {
                neighbors.Add(neighbor);
            }
        }

        private static void OrientLoop(List<Vector3> loop, Vector3 normal)
        {
            if (loop == null || loop.Count < 3)
            {
                return;
            }

            Vector3 areaNormal = Vector3.zero;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector3 current = loop[i];
                Vector3 next = loop[(i + 1) % loop.Count];
                areaNormal += Vector3.Cross(current, next);
            }

            if (Vector3.Dot(areaNormal, normal) < 0f)
            {
                loop.Reverse();
            }
        }

        private static Vector3Int QuantizePoint(Vector3 point, float tolerance)
        {
            return new Vector3Int(
                Mathf.RoundToInt(point.x / tolerance),
                Mathf.RoundToInt(point.y / tolerance),
                Mathf.RoundToInt(point.z / tolerance));
        }

        private static string BuildEdgeKey(Vector3Int a, Vector3Int b)
        {
            string aKey = BuildPointKey(a);
            string bKey = BuildPointKey(b);
            return string.CompareOrdinal(aKey, bKey) <= 0 ? aKey + "|" + bKey : bKey + "|" + aKey;
        }

        private static string BuildPointKey(Vector3Int key)
        {
            return key.x + "," + key.y + "," + key.z;
        }

        private static float EstimateTolerance(IReadOnlyList<CapSegment> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return Epsilon;
            }

            Bounds bounds = new Bounds(segments[0].A, Vector3.zero);
            for (int i = 0; i < segments.Count; i++)
            {
                bounds.Encapsulate(segments[i].A);
                bounds.Encapsulate(segments[i].B);
            }

            return Mathf.Max(Epsilon, bounds.size.magnitude * 0.00001f);
        }

    }
}
