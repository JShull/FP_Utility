namespace FuzzPhyte.Utility.Interactive
{
    using UnityEngine;
    using System.Collections.Generic;
    [CreateAssetMenu(fileName = "FPVariantConfig", menuName = "FuzzPhyte/FPVariantConfig")]
    public class FPVariantConfig : FP_Data
    {
        [Header("Base Prefab (logic root)")]
        public GameObject basePrefab;

        [Header("Visual")]
        public Mesh visualMesh;
        public Material[] visualMaterials;

        [Header("Colliders")]
        public List<FPVariantColliderSpec> colliders = new();

        [Header("Output")]
        public string variantName = "NewVariant";
        public string saveFolder = "Assets/Variants"; // Project-relative path
    }
}
