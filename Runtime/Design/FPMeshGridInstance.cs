namespace FuzzPhyte.Utility
{
    using UnityEngine;

    /// <summary>
    /// Scene component that regenerates a mesh from FPMeshGridData.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class FPMeshGridInstance : MonoBehaviour
    {
        public FPMeshGridData DataAsset;
        public Material PreviewMaterial;
        public bool AddMeshCollider;
        public bool AutoRegenerateInEditor = true;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        private void OnEnable()
        {
            EnsureComponents();
            FPMeshGridData.Changed += HandleDataAssetChanged;
        }

        private void OnDisable()
        {
            FPMeshGridData.Changed -= HandleDataAssetChanged;
        }

        public void Regenerate()
        {
            RegenerateInternal(null);
        }

        public void RegenerateWithHeightmapOverride(Texture2D heightmapOverride)
        {
            RegenerateInternal(heightmapOverride);
        }

        private void RegenerateInternal(Texture2D heightmapOverride)
        {
            if (DataAsset == null)
            {
                return;
            }

            EnsureComponents();

            Mesh previousMesh = _meshFilter.sharedMesh;
            Mesh nextMesh = FPMeshGridBuilder.Build(DataAsset.GridSettings);
            FPMeshHeightmapSettings heightmapSettings = DataAsset.HeightmapSettings.Sanitized();
            if (heightmapOverride != null)
            {
                heightmapSettings.Heightmap = heightmapOverride;
            }

            FPMeshHeightmapUtility.ApplyHeightmap(nextMesh, heightmapSettings, DataAsset.HeightProcessSettings);

            _meshFilter.sharedMesh = nextMesh;

            if (PreviewMaterial != null)
            {
                _meshRenderer.sharedMaterial = PreviewMaterial;
            }

            MeshCollider meshCollider = GetComponent<MeshCollider>();
            if (AddMeshCollider)
            {
                if (meshCollider == null)
                {
                    meshCollider = gameObject.AddComponent<MeshCollider>();
                }

                meshCollider.sharedMesh = nextMesh;
            }
            else if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }

            gameObject.name = string.IsNullOrWhiteSpace(DataAsset.GridSettings.MeshName)
                ? "FP_GridSurface"
                : DataAsset.GridSettings.MeshName;

#if UNITY_EDITOR
            if (!Application.isPlaying && previousMesh != null && !UnityEditor.EditorUtility.IsPersistent(previousMesh))
            {
                DestroyImmediate(previousMesh);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.EditorUtility.SetDirty(_meshFilter);
            UnityEditor.EditorUtility.SetDirty(_meshRenderer);
            if (meshCollider != null)
            {
                UnityEditor.EditorUtility.SetDirty(meshCollider);
            }
#else
            if (previousMesh != null)
            {
                Destroy(previousMesh);
            }
#endif
        }

        private void EnsureComponents()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private void OnValidate()
        {
            EnsureComponents();

#if UNITY_EDITOR
            if (!Application.isPlaying && AutoRegenerateInEditor && DataAsset != null)
            {
                Regenerate();
            }
#endif
        }

        private void HandleDataAssetChanged(FPMeshGridData changedAsset)
        {
#if UNITY_EDITOR
            if (Application.isPlaying || !AutoRegenerateInEditor)
            {
                return;
            }

            if (changedAsset == null || changedAsset != DataAsset)
            {
                return;
            }

            Regenerate();
#endif
        }
    }
}
