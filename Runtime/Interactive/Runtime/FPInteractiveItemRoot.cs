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
