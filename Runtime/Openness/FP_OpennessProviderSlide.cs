namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public class FP_OpennessProviderSlide : MonoBehaviour, IFPOpennessProvider
    {
        [Header("References")]
        [SerializeField] private Transform movingPart;
        [SerializeField] private Transform closedReference; // optional; if null, capture on Setup

        [Header("Measurement")]
        [SerializeField] private MeasureMode measureMode = MeasureMode.AxisProjected;
        [SerializeField] private SpaceMode spaceMode = SpaceMode.Local;

        [Tooltip("Axis to measure along (used only for AxisProjected). Typically drawer forward.")]
        [SerializeField] private Vector3 axis = Vector3.forward;

        [Tooltip("Distance from closed to fully-open (in the chosen space/axis).")]
        [Min(0.0001f)]
        [SerializeField] private float fullyOpenDistance = 0.3f;

        [Tooltip("If true, uses absolute value of projected distance so it works either direction.")]
        [SerializeField] private bool useAbsolute = true;

        private Vector3 _closedPos;

        public void Setup()
        {
            if (movingPart == null) movingPart = transform;

            if (closedReference != null)
                _closedPos = GetPosition(closedReference);
            else
                _closedPos = GetPosition(movingPart);
        }
        public float GetOpennessRaw()
        {
            if (movingPart == null) return 0f;

            Vector3 current = GetPosition(movingPart);
            Vector3 delta = current - _closedPos;

            if (measureMode == MeasureMode.Magnitude)
                return delta.magnitude;

            Vector3 axisN = GetAxisNormalized();
            float projected = Vector3.Dot(delta, axisN);
            if (useAbsolute) projected = Mathf.Abs(projected);
            return projected;
        }
        public float GetOpennessNormalized()
        {
            float raw = GetOpennessRaw();
            if (fullyOpenDistance <= 0f) return 0f;
            return Mathf.Clamp01(raw / fullyOpenDistance);
        }
        private Vector3 GetPosition(Transform t)
            => (spaceMode == SpaceMode.World) ? t.position : t.localPosition;
        private Vector3 GetAxisNormalized()
        {
            Vector3 a = axis;
            if (spaceMode == SpaceMode.World) a = (movingPart != null ? movingPart.TransformDirection(axis) : axis);
            // if spaceMode == Local, axis is already local
            if (a.sqrMagnitude < 0.000001f) a = Vector3.forward;
            return a.normalized;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (fullyOpenDistance < 0.0001f) fullyOpenDistance = 0.0001f;
        }
#endif
    }
}
