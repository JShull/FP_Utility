namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public sealed class FPRuntimeCutawayVolume : MonoBehaviour
    {
        public static FPRuntimeCutawayVolume Active;

        public bool useSphere = true;
        public float sphereRadius = 2f;
        public Vector3 boxExtents = Vector3.one;
        public bool UseGizmo = true;
        public Color GizmoColor = Color.orangeRed;
        public Vector3 Center => transform.position;
        private Vector3 startPos;
        private Vector3 startExtents;
        private float sphereRadiuStart;
        private bool useSphereStart;
        private static readonly int VolumeCenterID = Shader.PropertyToID("_VolumeCenter");
        private static readonly int SphereRadiusID = Shader.PropertyToID("_SphereRadius");
        private static readonly int BoxExtentsID = Shader.PropertyToID("_BoxExtents");
        private static readonly int UseSphereID = Shader.PropertyToID("_UseSphere");
        void OnEnable()
        {
            Active = this;
            startPos = transform.position;
            startExtents = boxExtents;
            sphereRadiuStart = sphereRadius;
            useSphereStart = useSphere;
        }

        void OnDisable()
        {
            if (Active == this)
            {
                this.transform.position = startPos;
                useSphere = useSphereStart;
                sphereRadius = sphereRadiuStart;
                boxExtents = startExtents;
                ResetGlobals();
                Active = null;
            }  
        }
        private void OnDrawGizmos()
        {
            if (!UseGizmo) return;
            Gizmos.color = GizmoColor;

            if (useSphere)
                Gizmos.DrawWireSphere(Center, sphereRadius);
            else
            {
                Gizmos.DrawWireCube(Center, boxExtents * 2f);
            }
        }
        private void ResetGlobals()
        {
            Shader.SetGlobalVector(VolumeCenterID, startPos);
            Shader.SetGlobalFloat(SphereRadiusID, sphereRadiuStart);
            Shader.SetGlobalVector(BoxExtentsID, startExtents);
            Shader.SetGlobalInt(UseSphereID, useSphereStart ? 1 : 0);
        }
    }
}
