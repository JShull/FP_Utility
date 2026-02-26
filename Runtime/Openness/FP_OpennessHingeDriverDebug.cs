namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public class FP_OpennessHingeDriverDebug : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform hingePivot;      // the rotating object
        [SerializeField] private Transform closedReference; // closed orientation reference
        [SerializeField] private Transform openReference;   // open orientation reference
        [SerializeField] private FP_OpennessStateTracker tracker;

        [Header("Hinge Settings")]
        [Tooltip("Local hinge axis (e.g., Vector3.up for typical door).")]
        [SerializeField] private Vector3 hingeAxisLocal = Vector3.up;

        [Tooltip("Clamp normalized value between 0 and 1.")]
        [SerializeField] private bool clampNormalized = true;

        [Header("Debug")]
        [SerializeField] private bool isGrabbed = false;
        [SerializeField] private float fullyOpenAngle;
        [SerializeField] private float currentRawAngle;
        [SerializeField] private float currentNormalized;

        private Vector3 _referencePerpLocal;

        private void Awake()
        {
        }
        private void Start()
        {
            if (hingePivot == null || closedReference == null || openReference == null)
                return;

            // Build perpendicular reference FIRST
            Vector3 axis = hingeAxisLocal.normalized;
            Vector3 fallback = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f
                ? Vector3.up
                : Vector3.right;

            _referencePerpLocal = Vector3.ProjectOnPlane(fallback, axis).normalized;

            // compute fully open angle
            fullyOpenAngle = ComputeAngleBetween(
                closedReference.localRotation,
                openReference.localRotation
            );

            Debug.Log($"Fully Open Angle: {fullyOpenAngle}");
        }

        private void Update()
        {
            if (!isGrabbed) return;
            if (hingePivot == null || closedReference == null || tracker == null) return;

            currentRawAngle = ComputeAngleBetween(closedReference.localRotation, hingePivot.localRotation);

            if (fullyOpenAngle <= 0f)
            {
                currentNormalized = 0f;
            }
            else
            {
                currentNormalized = currentRawAngle / fullyOpenAngle;
                if (clampNormalized)
                    currentNormalized = Mathf.Clamp01(currentNormalized);
            }

            tracker.UpdateNormalized(currentNormalized);
        }

        private float ComputeAngleBetween(Quaternion from, Quaternion to)
        {
            Quaternion delta = Quaternion.Inverse(from) * to;

            Vector3 axis = hingeAxisLocal.normalized;
            Vector3 refDir = _referencePerpLocal;

            Vector3 rotated = delta * refDir;

            float signed = Vector3.SignedAngle(refDir, rotated, axis);

            return Mathf.Abs(signed); // symmetric hinge for debug
        }

        #region Interaction Hooks

        [ContextMenu("Begin Grab")]
        public void BeginGrab()
        {
            isGrabbed = true;
            tracker?.StartMotion();
        }

        [ContextMenu("End Grab")]
        public void EndGrab()
        {
            isGrabbed = false;
            tracker?.EndMotion();
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            if (hingePivot == null) return;

            Gizmos.color = Color.magenta;
            Vector3 worldAxis = hingePivot.TransformDirection(hingeAxisLocal.normalized);
            Gizmos.DrawLine(hingePivot.position, hingePivot.position + worldAxis * 0.5f);
        }
    }
}
