// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Interactive
{
    using UnityEngine;
    using System.Collections.Generic;
    using UnityEngine.Serialization;
    [CreateAssetMenu(fileName = "FPVariantConfig", menuName = "FuzzPhyte/FPVariantConfig")]
    public class FPVariantConfig : FP_Data
    {
        [Header("Base Prefab (logic root)")]
        public GameObject basePrefab;

        [Header("New Mesh Setup")]
        [Tooltip("One or more visual pieces (Mesh + Materials). New workflow.")]
        public List<FPMeshMaterialSet> MeshSets = new();

        [Header("Colliders")]
        public List<FPVariantColliderSpec> colliders = new();

        [Header("Output")]
        public string variantName = "NewVariant";
        public string saveFolder = "Assets/Variants"; // Project-relative path


    }
}
