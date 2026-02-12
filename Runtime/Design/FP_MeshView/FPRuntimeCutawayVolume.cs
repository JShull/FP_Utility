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
        private static readonly int UseSphereID = Shader.PropertyToID("_UseSphere");
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
            //Shader.SetGlobalVector(VolumeCenterID, transform.position);
            //Shader.SetGlobalFloat(SphereRadiusID, sphereRadius);
            //Shader.SetGlobalVector(BoxExtentsID, boxExtents);
            //Shader.SetGlobalInt(UseSphereID, useSphere ? 1 : 0);
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
