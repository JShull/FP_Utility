namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using System.Linq;
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
        //public IReadOnlyList<Renderer> Targets => _targets;

        public IReadOnlyList<Renderer>GetTargets => _targets;
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
            ResetCacheAndClear();
        }
        #endregion
        public void SetTargets(IEnumerable<Renderer> renderers, bool showRenderers=true)
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
       
        
        public void SetMeshModeType(MeshViewMode incomingMeshMode,IEnumerable<Renderer> renderers = null)
        {
            // JOHN for now we are going to use Vertices/Wireframe and WireframeAndVertices as the same
            // JOHN default will just be a clear
            switch (incomingMeshMode)
            {
                case MeshViewMode.Default:
                    mode = MeshViewMode.Default;
                    ResetCacheAndClear();
                    //hide all overlays
                    break;
                case MeshViewMode.Vertices:
                    mode = MeshViewMode.Vertices;
                    if(renderers != null)
                    {
                        SetTargets(renderers);
                    }
                    //show vertices/wireframe
                    break;
                case MeshViewMode.Wireframe:
                    mode = MeshViewMode.Wireframe;
                    if(renderers != null)
                    {
                        SetTargets(renderers);
                    }
                    //show wireframe/vertices
                    break;
                case MeshViewMode.WireframeAndVertices:
                    mode = MeshViewMode.WireframeAndVertices;
                    if(renderers != null)
                    {
                        SetTargets(renderers);
                    }
                    //show wireframe & vertices
                    break;
                case MeshViewMode.Normals:
                    mode = MeshViewMode.Normals;
                    break;
                case MeshViewMode.SurfaceWorldNormals:
                    mode = MeshViewMode.SurfaceWorldNormals;
                    break;
                case MeshViewMode.SurfaceUV0:
                    mode = MeshViewMode.SurfaceUV0;
                    break;
                case MeshViewMode.SurfaceVertexColor:
                    mode = MeshViewMode.SurfaceVertexColor;
                    break;
            }
        }
        private void ResetCacheAndClear()
        {
             foreach (var kvp in _cache)
            {
                kvp.Value.Dispose();
            }  
            _cache.Clear();
        }


    }
}
