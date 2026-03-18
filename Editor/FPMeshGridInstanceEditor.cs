namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(FPMeshGridInstance))]
    public class FPMeshGridInstanceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            var instance = (FPMeshGridInstance)target;
            using (new EditorGUI.DisabledScope(instance.DataAsset == null))
            {
                if (GUILayout.Button("Regenerate Mesh"))
                {
                    RegenerateInstance(instance);
                }

                if (GUILayout.Button("Save Mesh Asset"))
                {
                    SaveInstanceMesh(instance);
                }
            }

            if (instance.DataAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an FPMeshGridData asset to enable regeneration from stored grid and heightmap settings.",
                    MessageType.Info);
            }
        }

        [MenuItem("GameObject/FuzzPhyte/Rendering/Regenerate Selected Mesh Grid", false, 21)]
        private static void RegenerateSelectedMeshGrid()
        {
            var instances = Selection.GetFiltered<FPMeshGridInstance>(SelectionMode.Editable | SelectionMode.TopLevel);
            for (int i = 0; i < instances.Length; i++)
            {
                RegenerateInstance(instances[i]);
            }
        }

        [MenuItem("GameObject/FuzzPhyte/Rendering/Regenerate Selected Mesh Grid", true)]
        private static bool ValidateRegenerateSelectedMeshGrid()
        {
            return Selection.GetFiltered<FPMeshGridInstance>(SelectionMode.Editable | SelectionMode.TopLevel).Length > 0;
        }

        private static void RegenerateInstance(FPMeshGridInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            Undo.RecordObject(instance, "Regenerate Mesh Grid");
            instance.Regenerate();
            EditorUtility.SetDirty(instance);
        }

        private static void SaveInstanceMesh(FPMeshGridInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            Mesh mesh = ResolveMeshForSaving(instance);
            if (mesh == null)
            {
                Debug.LogWarning("[FP Mesh Generator] No mesh is available to save.");
                return;
            }

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
                    Object.DestroyImmediate(mesh);
                }

                return;
            }

            Mesh originalMeshReference = mesh;
            string result = FP_Utility_Editor.CreateAssetAt(mesh, path);
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (savedMesh != null)
            {
                ReplaceSceneMeshReferences(instance, originalMeshReference, savedMesh);
            }

            Debug.Log($"[FP Mesh Generator] Mesh saved to {result}");
        }

        private static Mesh ResolveMeshForSaving(FPMeshGridInstance instance)
        {
            Mesh liveMesh = TryGetLiveGeneratedMesh(instance);
            if (liveMesh != null)
            {
                return liveMesh;
            }

            if (instance.DataAsset == null)
            {
                return null;
            }

            Mesh mesh = FPMeshGridBuilder.Build(instance.DataAsset.GridSettings);
            FPMeshHeightmapSettings heightmapSettings = instance.DataAsset.HeightmapSettings.Sanitized();
            FPMeshHeightmapUtility.ApplyHeightmap(mesh, heightmapSettings, instance.DataAsset.HeightProcessSettings);
            return mesh;
        }

        private static Mesh TryGetLiveGeneratedMesh(FPMeshGridInstance instance)
        {
            if (instance == null)
            {
                return null;
            }

            MeshFilter meshFilter = instance.GetComponent<MeshFilter>();
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

        private static void ReplaceSceneMeshReferences(FPMeshGridInstance instance, Mesh originalMesh, Mesh savedMesh)
        {
            if (originalMesh == null || savedMesh == null)
            {
                return;
            }

            MeshFilter[] meshFilters = Resources.FindObjectsOfTypeAll<MeshFilter>();
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

            MeshCollider[] meshColliders = Resources.FindObjectsOfTypeAll<MeshCollider>();
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

            if (instance != null)
            {
                EditorUtility.SetDirty(instance.gameObject);
            }
        }
    }
}
