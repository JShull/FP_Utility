// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

using UnityEngine;

namespace FuzzPhyte.Utility.Interactive
{
    public class FPInteractiveItemRoot : FP_SelectionBase
    {
        [Space]
        [Tooltip("Visual Parent")]
        public Transform VisualRoot;
        [Tooltip("Collider Parent")]
        public Transform ColliderRoot;
        [Tooltip("RigidBody Parent")]
        public Transform RigidBodyRoot;
        [Tooltip("AudioSources Parent")]
        public Transform AudioRoot;
    }
}
