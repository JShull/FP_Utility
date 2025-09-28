namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Generate and return information associated with bounds/size of items
    /// </summary>
    public static class FPColliderBoundsUtility
    {
        #region Parameters
        /// <summary>
        /// Simple AABB volume from Bounds.
        /// </summary>
        private static float Volume(Bounds b)
        {
            var s = b.size;
            return s.x * s.y * s.z;
        }
        #endregion
        #region Public Accessors
        /// <summary>
        /// Try to get bounds for a GameObject based on collider type selection.
        /// Falls back to Renderers if no suitable colliders found.
        /// Useful for how Unity 'F-Key' Focus operates but with options
        /// </summary>
        /// <param name="go">Gameobject of reference</param>
        /// <param name="type">Type of Collider</param>
        /// <param name="bounds"></param>
        /// <param name="scope">If you want all renderers = EncapsulateAll</param>
        /// <param name="includeTriggers"></param>
        /// <param name="combine"></param>
        /// <returns></returns>
        public static bool TryGetBounds(GameObject go, FPColliderType type, out Bounds bounds,
            FPSearchScope scope = FPSearchScope.IncludeChildren,
            bool includeTriggers = true,
            FPBoundsCombine combine = FPBoundsCombine.EncapsulateAll)
        {
            bounds = default;
            if (go == null) return false;

            // 1) collect candidate colliders
            IEnumerable<Collider> colliders = scope == FPSearchScope.IncludeChildren
                ? go.GetComponentsInChildren<Collider>(true)
                : go.GetComponents<Collider>();

            if (!includeTriggers)
                colliders = colliders.Where(c => !c.isTrigger);

            // 2) filter by type (NA = any)
            colliders = FilterByType(colliders, type);

            // 3) try to produce bounds from colliders
            if (TryCombineBoundsFromColliders(colliders, combine, out bounds))
                return true;

            // 4) fallback to renderers
            IEnumerable<Renderer> renderers = scope == FPSearchScope.IncludeChildren
                ? go.GetComponentsInChildren<Renderer>(true)
                : go.GetComponents<Renderer>();

            return TryCombineBoundsFromRenderers(renderers, combine, out bounds);
        }

        /// <summary>
        /// Ensure a single collider of the requested type exists and fits the object’s renderers.
        /// If ignoreExisting is true, any existing colliders on the target are removed first.
        /// Returns the created/updated Collider, or null if fit fails (e.g., no renderers).
        /// </summary>
        /// <param name="target">Object in question</param>
        /// <param name="type"></param>
        /// <param name="includeChildren"></param>
        /// <param name="ignoreExisting"></param>
        /// <param name="meshColliderConvex"></param>
        /// <returns></returns>
        public static Collider EnsureFittedCollider(
            GameObject target,
            FPColliderType type,
            bool includeChildren = true,
            bool ignoreExisting = false,
            bool meshColliderConvex = true)
        {
            if (target == null) return null;

            if (ignoreExisting)
            {
                var existing = target.GetComponents<Collider>();
                foreach (var c in existing) Object.DestroyImmediate(c);
            }

            // 1) Collect renderers
            var renderers = includeChildren
                ? target.GetComponentsInChildren<Renderer>(true)
                : target.GetComponents<Renderer>();

            // Remove disabled/hidden renderers if you prefer strict visuals
            renderers = renderers.Where(r => r != null).ToArray();
            if (renderers.Length == 0)
            {
                // no renderers → give a tiny collider at origin as a fallback
                return CreateTinyCollider(target, type);
            }

            // 2) World AABB that encapsulates all renderers
            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                worldBounds.Encapsulate(renderers[i].bounds);

            // 3) Decide by collider type
            switch (type)
            {
                case FPColliderType.Box:
                    return FitBoxCollider(target, worldBounds);

                case FPColliderType.Sphere:
                    return FitSphereCollider(target, worldBounds);

                case FPColliderType.Capsule:
                    return FitCapsuleCollider(target, worldBounds);

                case FPColliderType.Mesh:
                    return FitMeshCollider(target, renderers, includeChildren, meshColliderConvex);

                case FPColliderType.NA:
                default:
                    // Choose a sensible default → Box
                    return FitBoxCollider(target, worldBounds);
            }
        }
        #endregion       
        #region Bounds Related

        /// <summary>
        /// Filters colliders by FPVariantColliderType (NA = no filter).
        /// </summary>
        private static IEnumerable<Collider> FilterByType(IEnumerable<Collider> colliders, FPColliderType type)
        {
            switch (type)
            {
                case FPColliderType.NA:
                    return colliders;
                case FPColliderType.Box:
                    return colliders.Where(c => c is BoxCollider);
                case FPColliderType.Capsule:
                    return colliders.Where(c => c is CapsuleCollider);
                case FPColliderType.Mesh:
                    return colliders.Where(c => c is MeshCollider);
                case FPColliderType.Sphere:
                    return colliders.Where(c => c is SphereCollider);
                default:
                    return colliders;
            }
        }

        /// <summary>
        /// Combines bounds from a set of colliders according to strategy.
        /// </summary>
        private static bool TryCombineBoundsFromColliders(IEnumerable<Collider> colliders, FPBoundsCombine combine, out Bounds bounds)
        {
            bounds = default;

            // materialize once
            var list = colliders as IList<Collider> ?? colliders.ToList();
            if (list.Count == 0) return false;

            switch (combine)
            {
                case FPBoundsCombine.FirstMatch:
                    bounds = list[0].bounds;
                    return true;

                case FPBoundsCombine.LargestByBoundsVolume:
                    {
                        Collider largest = null;
                        float maxVol = -1f;
                        foreach (var c in list)
                        {
                            var b = c.bounds;
                            float v = Volume(b);
                            if (v > maxVol)
                            {
                                maxVol = v;
                                largest = c;
                            }
                        }
                        if (largest == null) return false;
                        bounds = largest.bounds;
                        return true;
                    }

                case FPBoundsCombine.EncapsulateAll:
                default:
                    {
                        var combined = list[0].bounds;
                        for (int i = 1; i < list.Count; i++)
                            combined.Encapsulate(list[i].bounds);
                        bounds = combined;
                        return true;
                    }
            }
        }
        /// <summary>
        /// Combines bounds from a set of renderers according to strategy.
        /// </summary>
        private static bool TryCombineBoundsFromRenderers(IEnumerable<Renderer> renderers, FPBoundsCombine combine, out Bounds bounds)
        {
            bounds = default;

            var list = renderers as IList<Renderer> ?? renderers.ToList();
            if (list.Count == 0) return false;

            switch (combine)
            {
                case FPBoundsCombine.FirstMatch:
                    bounds = list[0].bounds;
                    return true;

                case FPBoundsCombine.LargestByBoundsVolume:
                    {
                        Renderer largest = null;
                        float maxVol = -1f;
                        foreach (var r in list)
                        {
                            var b = r.bounds;
                            float v = Volume(b);
                            if (v > maxVol)
                            {
                                maxVol = v;
                                largest = r;
                            }
                        }
                        if (largest == null) return false;
                        bounds = largest.bounds;
                        return true;
                    }

                case FPBoundsCombine.EncapsulateAll:
                default:
                    {
                        var combined = list[0].bounds;
                        for (int i = 1; i < list.Count; i++)
                            combined.Encapsulate(list[i].bounds);
                        bounds = combined;
                        return true;
                    }
            }
        }
        #endregion
        #region Fit Utility Work
        
        // ---------- Box ----------
        private static Collider FitBoxCollider(GameObject target, Bounds worldBounds)
        {
            // Convert world AABB to a *local* AABB for this transform.
            ToLocalAABB(target.transform, worldBounds, out var localCenter, out var localSize);

            var box = target.GetComponent<BoxCollider>();
            if (box == null) box = target.AddComponent<BoxCollider>();
            box.center = localCenter;
            box.size = localSize;
            return box;
        }

        // ---------- Sphere ----------
        private static Collider FitSphereCollider(GameObject target, Bounds worldBounds)
        {
            ToLocalAABB(target.transform, worldBounds, out var localCenter, out var localSize);

            // Fit a sphere around the local AABB (circumscribed sphere).
            float radius = 0.5f * Mathf.Max(localSize.x, Mathf.Max(localSize.y, localSize.z));

            var sphere = target.GetComponent<SphereCollider>();
            if (sphere == null) sphere = target.AddComponent<SphereCollider>();
            sphere.center = localCenter;
            sphere.radius = radius;
            return sphere;
        }

        // ---------- Capsule ----------
        private static Collider FitCapsuleCollider(GameObject target, Bounds worldBounds)
        {
            ToLocalAABB(target.transform, worldBounds, out var localCenter, out var localSize);

            // Choose the longest local axis as capsule axis
            int direction; // 0=X, 1=Y, 2=Z (Unity CapsuleCollider convention)
            float x = localSize.x, y = localSize.y, z = localSize.z;
            if (x >= y && x >= z) direction = 0;
            else if (y >= x && y >= z) direction = 1;
            else direction = 2;

            // Radius = half of the *smaller* of the other two dimensions
            float radius;
            float height;

            switch (direction)
            {
                case 0: // X axis
                    radius = 0.5f * Mathf.Min(y, z);
                    height = Mathf.Max(x, 2f * radius);
                    break;
                case 1: // Y axis
                    radius = 0.5f * Mathf.Min(x, z);
                    height = Mathf.Max(y, 2f * radius);
                    break;
                default: // Z axis
                    radius = 0.5f * Mathf.Min(x, y);
                    height = Mathf.Max(z, 2f * radius);
                    break;
            }

            var capsule = target.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = target.AddComponent<CapsuleCollider>();
            capsule.center = localCenter;
            capsule.direction = direction;
            capsule.radius = radius;
            capsule.height = height;
            return capsule;
        }

        // ---------- Mesh ----------
        private static Collider FitMeshCollider(
            GameObject target,
            Renderer[] renderers,
            bool includeChildren,
            bool convex)
        {
            // Collect meshes from MeshFilters and SkinnedMeshRenderers.
            var meshFilters = includeChildren
                ? target.GetComponentsInChildren<MeshFilter>(true)
                : target.GetComponents<MeshFilter>();

            var skinned = includeChildren
                ? target.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                : target.GetComponents<SkinnedMeshRenderer>();

            if (meshFilters.Length == 0 && skinned.Length == 0)
            {
                // Fall back to a Box if we can’t get meshes
                Bounds worldBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    worldBounds.Encapsulate(renderers[i].bounds);
                return FitBoxCollider(target, worldBounds);
            }

            var combine = new List<CombineInstance>();

            // Static meshes
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                var ci = new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = target.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix
                };
                combine.Add(ci);
            }

            // Skinned meshes (bake to a temporary mesh)
            foreach (var smr in skinned)
            {
                if (smr.sharedMesh == null) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked);
                var ci = new CombineInstance
                {
                    mesh = baked,
                    transform = target.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix
                };
                combine.Add(ci);
            }

            var combinedMesh = new Mesh { name = $"{target.name}_CombinedColliderMesh" };
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // safer for big merges
            combinedMesh.CombineMeshes(combine.ToArray(), true, true, false);

            var mc = target.GetComponent<MeshCollider>();
            if (mc == null) mc = target.AddComponent<MeshCollider>();
            mc.sharedMesh = combinedMesh;
            mc.convex = convex; // needed if you want triggers or dynamic Rigidbody interaction
            return mc;
        }

        // ---------- Helpers ----------
        private static Collider CreateTinyCollider(GameObject target, FPColliderType type)
        {
            switch (type)
            {
                case FPColliderType.Sphere:
                    var sph = target.AddComponent<SphereCollider>();
                    sph.center = Vector3.zero;
                    sph.radius = 0.1f;
                    return sph;

                case FPColliderType.Capsule:
                    var cap = target.AddComponent<CapsuleCollider>();
                    cap.center = Vector3.zero;
                    cap.direction = 1;
                    cap.radius = 0.05f;
                    cap.height = 0.2f;
                    return cap;

                case FPColliderType.Mesh:
                    var mc = target.AddComponent<MeshCollider>();
                    mc.sharedMesh = null;
                    mc.convex = true;
                    return mc;

                case FPColliderType.Box:
                case FPColliderType.NA:
                default:
                    var box = target.AddComponent<BoxCollider>();
                    box.center = Vector3.zero;
                    box.size = Vector3.one * 0.2f;
                    return box;
            }
        }

        /// <summary>
        /// Convert a world-space Bounds to a *local-space AABB* for a transform by
        /// transforming the 8 corners and re-AABBing them.
        /// </summary>
        private static void ToLocalAABB(Transform t, Bounds worldBounds, out Vector3 localCenter, out Vector3 localSize)
        {
            Vector3 c = worldBounds.center;
            Vector3 e = worldBounds.extents;

            // 8 corners of the world AABB
            var corners = new[]
            {
                new Vector3(c.x - e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z - e.z),
                new Vector3(c.x - e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y - e.y, c.z + e.z),
                new Vector3(c.x - e.x, c.y + e.y, c.z + e.z),
                new Vector3(c.x + e.x, c.y + e.y, c.z + e.z),
            };

            // Transform to local, then AABB them
            var lb = new Bounds(t.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
                lb.Encapsulate(t.InverseTransformPoint(corners[i]));

            localCenter = lb.center;
            localSize = lb.size;
        }
        #endregion
    }
}
