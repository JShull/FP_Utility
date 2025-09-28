namespace FuzzPhyte.Utility.Interactive
{
    using UnityEngine;
    using System.Collections.Generic;

    [System.Serializable]
    public class FPMeshMaterialSet
    {
        [Tooltip("The mesh to render.")]
        public Mesh Mesh;

        [Tooltip("Materials corresponding to mesh submeshes. Will be auto-padded if fewer than subMeshCount.")]
        public List<Material> Materials = new List<Material>();

        [Tooltip("Optional name for this piece (used when creating child GameObjects).")]
        public string NameHint = "Mesh";

        [Tooltip("If true, use SkinnedMeshRenderer instead of MeshRenderer.")]
        public bool UseSkinned = false;

        [Tooltip("Optional local offset to apply to the created/updated child transform.")]
        public Vector3 LocalPositionOffset;

        [Tooltip("Optional local rotation (Euler) to apply to the created/updated child transform.")]
        public Vector3 LocalEulerOffset;

        [Tooltip("Optional local scale to apply to the created/updated child transform. Leave at (0,0,0) to ignore.")]
        public Vector3 LocalScaleOverride;

        public Material[] GetPaddedMaterials()
        {
            if (Mesh == null)
                return Materials.Count == 0 ? new Material[0] : Materials.ToArray();

            int subCount = Mathf.Max(1, Mesh.subMeshCount);
            var arr = new Material[subCount];

            if (Materials == null || Materials.Count == 0)
                return arr; // nulls are allowed; Unity will render with default material

            for (int i = 0; i < subCount; i++)
                arr[i] = i < Materials.Count ? Materials[i] : Materials[Materials.Count - 1];

            return arr;
        }
    }
    public static class FPMeshMaterialSetHelper
    {
        public static FPMeshMaterialSet CreateSetFromPiece(Transform root, Transform pieceTf, Mesh mesh, Material[] sharedMats, bool useSkinned)
        {
            // Local offsets relative to the provided root
            var localPos = root.InverseTransformPoint(pieceTf.position);
            var localRot = (Quaternion.Inverse(root.rotation) * pieceTf.rotation).eulerAngles;

            // Compute scale relative to root (handles nested scaling reasonably well)
            var relScale = RelativeLossyScale(pieceTf, root);
            var scaleOverride = Approximately(relScale, Vector3.one) ? Vector3.zero : relScale;

            var set = new FPMeshMaterialSet
            {
                Mesh = mesh,
                Materials = sharedMats != null ? new List<Material>(sharedMats) : new List<Material>(),
                NameHint = pieceTf.name,
                UseSkinned = useSkinned,
                LocalPositionOffset = localPos,
                LocalEulerOffset = localRot,
                LocalScaleOverride = scaleOverride
            };

            return set;
        }

        private static Vector3 RelativeLossyScale(Transform child, Transform root)
        {
            var c = child.lossyScale;
            var r = root.lossyScale;
            return new Vector3(SafeDiv(c.x, r.x), SafeDiv(c.y, r.y), SafeDiv(c.z, r.z));
        }

        private static float SafeDiv(float a, float b) => Mathf.Approximately(b, 0f) ? 0f : a / b;

        private static bool Approximately(Vector3 a, Vector3 b, float eps = 1e-4f)
        {
            return Mathf.Abs(a.x - b.x) < eps && Mathf.Abs(a.y - b.y) < eps && Mathf.Abs(a.z - b.z) < eps;
        }
    }
}
