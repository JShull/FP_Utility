namespace FuzzPhyte.Utility.Interactive
{
    using UnityEngine;
    using System.Collections.Generic;
    public class FPVariantPreview : MonoBehaviour
    {
        public GameObject baseInstance; // Instantiated base prefab in the scene for preview
        public Transform visualsRoot; // Where visuals/colliders are attached
        public Transform colliderRoot;

        // Working state (mirrors the editor config) so the scene handles can edit live
        public Mesh workingMesh;
        public List<Material> workingMaterials = new();
        public List<FPVariantColliderSpec> workingColliders = new();

        // Track generated bits for cleanup
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;

        public void EnsureVisual()
        {
            if (meshFilter == null)
            {
                var mf = new GameObject("Visual_Mesh");
                mf.transform.SetParent(visualsRoot, false);
                meshFilter = mf.AddComponent<MeshFilter>();
                meshRenderer = mf.AddComponent<MeshRenderer>();
            }
            meshFilter.sharedMesh = workingMesh;
            meshRenderer.sharedMaterials = workingMaterials.ToArray();
            
        }


        public void RebuildColliders()
        {
            // Clear existing
            var existing = colliderRoot.GetComponentsInChildren<Collider>(true);
            foreach (var col in existing)
            {
                if (col.gameObject.name.StartsWith("Col_"))
                    DestroyImmediate(col.gameObject);
            }

            for (int i = 0; i < workingColliders.Count; i++)
            {
                var spec = workingColliders[i];
                var go = new GameObject($"Col_{i}_{spec.type}");
                go.transform.SetParent(colliderRoot, false);
                go.transform.localPosition = spec.localPosition;
                go.transform.localEulerAngles = spec.localEuler;


                switch (spec.type)
                {
                    case FPVariantColliderType.Box:
                        var bc = go.AddComponent<BoxCollider>();
                        bc.isTrigger = spec.isTrigger;
                        bc.material = spec.material;
                        bc.size = spec.localScale == Vector3.zero ? Vector3.one : spec.localScale;
                        break;
                    case FPVariantColliderType.Sphere:
                        var sc = go.AddComponent<SphereCollider>();
                        sc.isTrigger = spec.isTrigger;
                        sc.material = spec.material;
                        sc.radius = Mathf.Max(0.001f, spec.radius);
                        break;
                    case FPVariantColliderType.Capsule:
                        var cc = go.AddComponent<CapsuleCollider>();
                        cc.isTrigger = spec.isTrigger;
                        cc.material = spec.material;
                        cc.radius = Mathf.Max(0.001f, spec.radius);
                        cc.height = Mathf.Max(cc.radius * 2f, spec.height);
                        cc.direction = Mathf.Clamp(spec.direction, 0, 2);
                        break;
                    case FPVariantColliderType.Mesh:
                        var mc = go.AddComponent<MeshCollider>();
                        mc.convex = spec.convex;
                        mc.isTrigger = spec.isTrigger;
                        mc.material = spec.material;
                        mc.sharedMesh = spec.meshForMeshCollider != null ? spec.meshForMeshCollider : workingMesh;
                        break;
                }
            }
        }
    }
}
