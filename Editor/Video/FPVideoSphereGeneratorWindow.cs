namespace FuzzPhyte.Utility.Editor.Video
{
    using FuzzPhyte.Utility.Video;
    using UnityEditor;
    using UnityEngine;

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

        private Vector2 scrollPosition;

        [MenuItem("FuzzPhyte/Utility/Video/FP Video Sphere Generator", priority = FP_UtilityData.ORDER_MENU + 7)]
        public static void ShowWindow()
        {
            FPVideoSphereGeneratorWindow window = GetWindow<FPVideoSphereGeneratorWindow>("FP Video Sphere");
            window.minSize = new Vector2(360f, 300f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(sphereSettings.MeshName))
            {
                sphereSettings = FPVideoSphereBuildSettings.Default;
            }

            SyncSelectionDefaults();
        }

        private void OnGUI()
        {
            using (EditorGUILayout.ScrollViewScope scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollView.scrollPosition;

                DrawHeader();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawSphereSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawAssetSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawSceneSettings();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawActions();
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

            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == originalMesh)
                {
                    Undo.RecordObject(meshFilter, "Assign Saved Video Sphere Mesh");
                    meshFilter.sharedMesh = savedMesh;
                    EditorUtility.SetDirty(meshFilter);
                }
            }

            MeshCollider[] meshColliders = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
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
