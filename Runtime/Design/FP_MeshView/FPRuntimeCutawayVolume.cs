namespace FuzzPhyte.Utility
{
    using Unity.Mathematics;
    using UnityEngine;

    public sealed class FPRuntimeCutawayVolume : MonoBehaviour
    {
        public static FPRuntimeCutawayVolume Active;

        public bool useSphere = true;
        public float sphereRadius = 2f;
        public Vector3 boxExtents = Vector3.one;
        public Vector3 Center => transform.position;

        private static readonly int VolumeCenterID = Shader.PropertyToID("_VolumeCenter");
        private static readonly int SphereRadiusID = Shader.PropertyToID("_SphereRadius");
        private static readonly int BoxExtentsID = Shader.PropertyToID("_BoxExtents");
        void OnEnable()
        {
            Active = this;
        }

        void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        void LateUpdate()
        {
            // Push globals every frame
            Shader.SetGlobalVector(VolumeCenterID, Center);
            Shader.SetGlobalFloat(SphereRadiusID, sphereRadius);
            Shader.SetGlobalVector(BoxExtentsID, boxExtents);
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;

            if (useSphere)
                Gizmos.DrawWireSphere(Center, sphereRadius);
            else
            {
                Gizmos.DrawWireCube(Center, boxExtents * 2f);
            }
               
        }
    }
}
