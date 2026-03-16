namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;

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

        private Vector2 scrollPosition;

        [MenuItem("FuzzPhyte/Utility/Rendering/FP Mesh Generator", priority = FP_UtilityData.ORDER_MENU + 6)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPMeshGeneratorWindow>("FP Mesh Generator");
            window.minSize = new Vector2(360f, 320f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(gridSettings.MeshName))
            {
                gridSettings = FPMeshGridBuildSettings.Default;
            }

            SyncSelectionDefaults();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollView.scrollPosition;

                DrawHeader();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawDataAssetSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawGridSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawHeightmapSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawHeightProcessSettings();
                FP_Utility_Editor.DrawUILine(Color.gray);

                DrawSceneSettings();
                FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);

                DrawActions();
            }

            if (EditorGUI.EndChangeCheck() && autoUpdatePreviewObject)
            {
                RefreshLastGeneratedPreview();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("FP Mesh Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build a flat rectangular grid mesh on the XZ plane. " +
                "This first pass is intended as the base surface for future heightmap deformation.",
                MessageType.Info);
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

            Debug.Log($"[FP Mesh Generator] Mesh saved to {result}");
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
