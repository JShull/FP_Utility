namespace FuzzPhyte.Utility.Interactive
{
    using UnityEngine;
    using System;
    [Serializable]
    public enum FPVariantColliderType { NA = 0, Box=1, Capsule = 2, Mesh = 3,Sphere = 4}
    [Serializable]
    public struct FPVariantColliderSpec
    {
        public string name; // Label in the UI
        public FPVariantColliderType type; // Box/Sphere/Capsule/Mesh
        public bool isTrigger; // Optional trigger
        public PhysicsMaterial material; // Optional physics material
        public Vector3 localPosition; // Relative to visual root
        public Vector3 localEuler; // Degrees
        public Vector3 localScale; // For Box only; others use radius/height
        public float radius; // Sphere/Capsule
        public float height; // Capsule height
        public int direction; // 0=X,1=Y,2=Z for Capsule
        public bool fitToVisual; // Helper: auto-fit box to visual bounds
        public Mesh meshForMeshCollider; // Optional explicit mesh (defaults to visual mesh)
        public bool convex; // MeshCollider convex
    }
}
