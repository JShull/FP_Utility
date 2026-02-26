namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public class FP_OpennessProviderHinge : MonoBehaviour, IFPOpennessProvider
    {
        [Header("References")]
        [SerializeField] private Transform rotatingPart;
        [SerializeField] private Transform closedReference; // optional; if null, capture on Setup

        [Header("Axis")]
        [Tooltip("Hinge axis in the selected space. For a typical door hinge, often local up.")]
        [SerializeField] private Vector3 hingeAxis = Vector3.up;

        [SerializeField] private SpaceMode spaceMode = SpaceMode.Local;

        [Header("Angle Range")]
        [Tooltip("Angle from closed to fully-open in degrees.")]
        [Min(0.1f)]
        [SerializeField] private float fullyOpenAngleDeg = 90f;

        [Tooltip("If true, uses absolute value so opening either direction counts as opening.")]
        [SerializeField] private bool useAbsolute = true;

        // To compute signed angle we need a stable reference vector perpendicular to the axis
        [Tooltip("Reference direction used to measure angle around axis. If zero, will auto-pick.")]
        [SerializeField] private Vector3 referencePerp = Vector3.forward;

        private Quaternion _closedRot;

        public void Setup()
        {
            if (rotatingPart == null) rotatingPart = transform;

            if (closedReference != null)
                _closedRot = GetRotation(closedReference);
            else
                _closedRot = GetRotation(rotatingPart);
        }

        public float GetOpennessRaw()
        {
            if (rotatingPart == null) return 0f;

            Quaternion current = GetRotation(rotatingPart);
            Quaternion delta = Quaternion.Inverse(_closedRot) * current;

            Vector3 axisWorldOrLocal = GetAxisNormalized();
            Vector3 refDir = GetReferencePerpNormalized(axisWorldOrLocal);

            // Rotate the ref direction by the delta rotation and measure signed angle around axis
            Vector3 refRotated = delta * refDir;

            float signed = Vector3.SignedAngle(refDir, refRotated, axisWorldOrLocal);
            if (useAbsolute) signed = Mathf.Abs(signed);

            // Clamp to a sane range (optional)
            return signed;
        }

        public float GetOpennessNormalized()
        {
            float raw = GetOpennessRaw();
            if (fullyOpenAngleDeg <= 0f) return 0f;
            return Mathf.Clamp01(raw / fullyOpenAngleDeg);
        }

        private Quaternion GetRotation(Transform t)
            => (spaceMode == SpaceMode.World) ? t.rotation : t.localRotation;

        private Vector3 GetAxisNormalized()
        {
            Vector3 a = hingeAxis;
            if (a.sqrMagnitude < 0.000001f) a = Vector3.up;

            if (spaceMode == SpaceMode.World && rotatingPart != null)
            {
                // hingeAxis provided in local-ish terms but wants world measurement:
                // If you prefer hingeAxis to be explicitly world, set it directly and remove this transform.
                // Keeping this as "axis relative to part" is usually what you want.
                a = rotatingPart.TransformDirection(hingeAxis);
            }

            return a.normalized;
        }

        private Vector3 GetReferencePerpNormalized(Vector3 axisN)
        {
            Vector3 r = referencePerp;

            // If no ref given or it’s parallel, auto-pick something perpendicular.
            if (r.sqrMagnitude < 0.000001f || Mathf.Abs(Vector3.Dot(r.normalized, axisN)) > 0.98f)
            {
                // pick any vector not parallel to axis
                r = (Mathf.Abs(Vector3.Dot(axisN, Vector3.up)) < 0.9f) ? Vector3.up : Vector3.right;
            }

            // Make it perpendicular to axis (project onto plane)
            r = Vector3.ProjectOnPlane(r, axisN);
            if (r.sqrMagnitude < 0.000001f) r = Vector3.ProjectOnPlane(Vector3.forward, axisN);

            return r.normalized;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (fullyOpenAngleDeg < 0.1f) fullyOpenAngleDeg = 0.1f;
        }
#endif
    }
}
