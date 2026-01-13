namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    public enum MeshViewMode
    {
        Default,
        Vertices,
        Wireframe,
        WireframeAndVertices,
        Normals,
        SurfaceWorldNormals,
        SurfaceUV0,
        SurfaceVertexColor,
    }

    public sealed class FPRuntimeMeshViewer:MonoBehaviour
    {
        public static FPRuntimeMeshViewer Active { get; private set; }
        [SerializeField] private MeshViewMode mode = MeshViewMode.Default;
        [SerializeField] private Renderer[] targetRenderers = null;
        // Assign these in inspector or load via Resources/Addressables
        [Header("Override Materials (URP)")]
        [SerializeField] private Material wireframeMat;
        public Material WireframeMat => wireframeMat;
        [SerializeField] private Material vertexMat;
        public Material VertexMat => vertexMat;
        [SerializeField] private Material normalsMat;
        public Material NormalsMat => normalsMat;
        [SerializeField] private Material surfaceDebugMat;
        public Material SurfaceDebugMat => surfaceDebugMat;

        private readonly Dictionary<Mesh, FPMeshViewCache> _cache = new();
        public bool TryGetCache(Mesh mesh, out FPMeshViewCache cache) => _cache.TryGetValue(mesh, out cache);
        private readonly List<Renderer> _targets = new();
        public void SetMode(MeshViewMode newMode) => mode = newMode;
        public MeshViewMode GetMode => mode;
        public Renderer[] GetTargets => _targets.ToArray();
#region Unity Methods
        public void Awake()
        {
            //Temp setup
            if (targetRenderers != null)
            {
                SetTargets(targetRenderers);
            } 
        }
        private void OnEnable()
        {
            Active = this;
        }
        private void OnDisable() 
        { 
            if (Active == this)
            {
                Active = null;
            }
        }
        private void OnDestroy()
        {
            foreach (var kvp in _cache)
            {
                kvp.Value.Dispose();
            }  
            _cache.Clear();
        }
        #endregion
        public void SetTargets(IEnumerable<Renderer> renderers)
        {
            _targets.Clear();
            _targets.AddRange(renderers);

            // Prewarm caches
            foreach (var r in _targets)
            {
                var mesh = GetMeshFromRenderer(r);
                if (mesh == null) continue;
                if (!_cache.ContainsKey(mesh))
                {
                    _cache[mesh] = FPMeshViewCache.Build(mesh);
                }
            }
        }

       

        /*
        private void OnRenderObject()
        {
            // Simple placeholder rendering hook.
            // In production URP, prefer a ScriptableRendererFeature pass.
            if (mode == MeshViewMode.Default) return;

            foreach (var r in _targets)
            {
                var mesh = GetMeshFromRenderer(r);
                if (mesh == null) continue;

                if (!_cache.TryGetValue(mesh, out var cache)) continue;

                var matrix = r.localToWorldMatrix;

                switch (mode)
                {
                    case MeshViewMode.Vertices:
                        cache.DrawVertices(matrix, vertexMat);
                        break;

                    case MeshViewMode.Wireframe:
                        cache.DrawWireframe(matrix, wireframeMat);
                        break;

                    case MeshViewMode.WireframeAndVertices:
                        cache.DrawWireframe(matrix, wireframeMat);
                        cache.DrawVertices(matrix, vertexMat);
                        break;

                    case MeshViewMode.Normals:
                        cache.DrawNormals(matrix, normalsMat);
                        break;

                    case MeshViewMode.SurfaceWorldNormals:
                    case MeshViewMode.SurfaceUV0:
                    case MeshViewMode.SurfaceVertexColor:
                        // easiest: draw mesh again with an override debug material
                        // (better in URP via renderer feature)
                        Graphics.DrawMesh(mesh, matrix, surfaceDebugMat, r.gameObject.layer);
                        break;
                }
            }
        }
        */

       
        private static Mesh GetMeshFromRenderer(Renderer r)
        {
            if (r is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                return mf ? mf.sharedMesh : null;
            }
            if (r is SkinnedMeshRenderer smr)
            {
                // For skinned meshes you might want baked mesh each frame (expensive),
                // or a static snapshot on selection.
                return smr.sharedMesh;
            }
            return null;
        }

    }
}
