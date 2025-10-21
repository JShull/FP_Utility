namespace FuzzPhyte.Utility.Interactive
{
    using UnityEngine;
    using System;
    
    [Serializable]
    public struct FPVariantColliderSpec
    {
        [Tooltip("Label in the UI")]
        public string name; // Label in the UI
        [Tooltip("Box/Sphere/Capsule/Mesh")]
        public FPColliderType type; // Box/Sphere/Capsule/Mesh
        [Tooltip("Optional trigger")]
        public bool isTrigger; // Optional trigger
        [Tooltip("Optional physics material")]
        public PhysicsMaterial material; // Optional physics material
        [Tooltip("Relative to visual root")]
        public Vector3 localPosition; // Relative to visual root
        public Vector3 localEuler; // Degrees
        [Tooltip("For Box only; others use radius/height")]
        public Vector3 localScale; // For Box only; others use radius/height
        [Tooltip("Sphere/Capsule radius")]
        public float radius; // Sphere/Capsule
        [Tooltip("Capsule height")]
        public float height; // Capsule height
        [Tooltip("0=NA,1=X,2=Y,4=Z,7=ALL, part of PrimitiveBoundsHandle.Axes enum")]//didn't want an editor ref here
        public int direction; // 0=NA,1=X,2=Y,4=Z,7=All for Capsule
        [Tooltip("Helper: auto-fit box to visual bounds")]
        public bool fitToVisual; // Helper: auto-fit box to visual bounds
        [Tooltip("Optional explicit mesh (defaults to visual mesh)")]
        public Mesh meshForMeshCollider; // Optional explicit mesh (defaults to visual mesh)
        public bool convex; // MeshCollider convex
    }
}
