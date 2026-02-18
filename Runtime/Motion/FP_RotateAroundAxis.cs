namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;
    /// <summary>
    /// Rotates a transform around an axis.
    /// - Spin in place and/or orbit around a pivot.
    /// Fixes: prevents jump to (0,0,0) by caching a valid orbit center at start.
    /// </summary>
    public class FP_RotateAroundAxis : FP_MotionBase
    {
        [Header("Rotation Axis")]
        [SerializeField] protected Vector3 rotationAxis = Vector3.up;
        [SerializeField] protected bool useLocalAxis = false;

        [Header("Spin & Orbit Modes")]
        [SerializeField] protected bool spinSelf = true;
        [SerializeField] protected bool orbitAroundPivot = false;

        [Tooltip("Optional explicit pivot. If null, we’ll use the object's position.")]
        [SerializeField] protected Transform pivot;

        [SerializeField] protected Vector3 pivotOffset = Vector3.zero;

        [Header("Pivot Behavior")]
        [Tooltip("If true, cache the orbit center at motion start so it never 'jumps' if the pivot moves or Setup order changes.")]
        [SerializeField] protected bool lockPivotAtStart = true;

        [Header("Angle / Speed")]
        [SerializeField] protected bool continuousSpeedMode = false;
        [SerializeField] protected float degreesPerSecond = 90f;
        [SerializeField] protected float totalAngle = 360f;
        [SerializeField] protected AnimationCurve rotationCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Reverse Behavior")]
        [SerializeField] protected bool alternateDirectionOnEachPlay = false;

        // Internal direction state (1 = forward, -1 = reverse)
        [SerializeField] protected int _direction = 1;


        // Cached orbit center for this run (prevents 0,0,0 jumps)
        [Space]
        [Header("Cached Values and Debugging")]
        // Baselines
        [SerializeField] protected Quaternion _startWorldRotation;
        [SerializeField] protected Vector3 _startPosition;
        [SerializeField] protected Vector3 _cachedOrbitCenter;
        [SerializeField] protected bool _hasCachedOrbit;
        [Space]
        [SerializeField] protected bool _debugShowGizmos = true;

        public override void SetupMotion()
        {
            Debug.Log($"Setup Motion");
            CacheStart();
            base.SetupMotion();

            // Cache the orbit center ONCE at motion start (unless user wants dynamic)
            if (orbitAroundPivot && lockPivotAtStart)
            {
                _cachedOrbitCenter = ComputePivotNow();
                _hasCachedOrbit = true;
            }
            else
            {
                _hasCachedOrbit = false;
            }
            Debug.Log($"Setup Motion ENDED");
        }

        public override void ResetMotion()
        {
            base.ResetMotion();
            if (!targetObject) return;

            targetObject.position = _startPosition;
            targetObject.rotation = _startWorldRotation;

            // Clear cached orbit center so a fresh run re-captures correctly
            _hasCachedOrbit = false;
        }

        public override void StartMotion()
        {
            if (alternateDirectionOnEachPlay)
            {
                _direction *= -1;
            }
            base.StartMotion();
        }
       
        public void SetDirection(bool forward)
        {
            _direction = forward ? 1 : -1;
        }
        public void ToggleDirection()
        {
            _direction *= -1;
        }
        protected override IEnumerator MotionRoutine()
        {
            do
            {
                if (!isPaused)
                {
                    if (continuousSpeedMode)
                        yield return RotateBySpeed();
                    else
                        yield return RotateByAngle(totalAngle);
                }
                yield return null;
            }
            while (loop);

            EndMotion();
        }

        private IEnumerator RotateByAngle(float angle)
        {
            float elapsed = 0f;
            float lastApplied = 0f;

            while (elapsed < lerpDuration)
            {
                if (!isPaused)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / lerpDuration);
                    float eased = rotationCurve.Evaluate(t);
                    float targetAngle = angle * eased;
                    float deltaAngle = (targetAngle - lastApplied)*-_direction;

                    ApplyRotation(ResolvePivot(), deltaAngle);

                    lastApplied = targetAngle;
                }
                yield return null;
            }

            // Snap residual
            float snap = (angle - lastApplied)*_direction;
            if (Mathf.Abs(snap) > 0.0001f)
            {
                ApplyRotation(ResolvePivot(), snap);
            }
        }

        private IEnumerator RotateBySpeed()
        {
            float elapsed = 0f;
            while (loop || elapsed < lerpDuration)
            {
                if (!isPaused)
                {
                    float delta = degreesPerSecond * Time.deltaTime*_direction;
                    ApplyRotation(ResolvePivot(), delta);
                    elapsed += Time.deltaTime;
                }
                yield return null;
            }
        }

        private void ApplyRotation(Vector3 center, float deltaAngle)
        {
            if (!targetObject) return;

            Vector3 worldAxis = useLocalAxis
                ? targetObject.TransformDirection(rotationAxis.normalized)
                : rotationAxis.normalized;

            if (spinSelf)
            {
                targetObject.Rotate(worldAxis, deltaAngle, Space.World);
            }

            if (orbitAroundPivot)
            {
                targetObject.RotateAround(center, worldAxis, deltaAngle);
            }
        }

        private Vector3 ResolvePivot()
        {
            if (!orbitAroundPivot)
            {
                return targetObject ? targetObject.position : Vector3.zero;
            }
               

            if (lockPivotAtStart && _hasCachedOrbit)
            {
                return _cachedOrbitCenter;
            }
              

            // Dynamic (or first-time) center
            var VectorCalc = ComputePivotNow();
            Debug.LogWarning($"Vector Calc: {VectorCalc.x},{VectorCalc.y},{VectorCalc.z}");
            return VectorCalc;
        }

        private Vector3 ComputePivotNow()
        {
            // IMPORTANT: Never fall back to (0,0,0). Use current position if no explicit pivot.
            if (pivot) return pivot.position + pivotOffset;
            return targetObject ? targetObject.position + pivotOffset : Vector3.zero;
        }

        private void CacheStart()
        {
            if (!targetObject) return;
            _startWorldRotation = targetObject.rotation;
            _startPosition = targetObject.position;
        }

#if UNITY_EDITOR
        public override void OnDrawGizmos()
        {
            if (!targetObject) return;
            if (!_debugShowGizmos) return;
            Vector3 worldAxis = useLocalAxis
                ? targetObject.TransformDirection(rotationAxis.normalized)
                : rotationAxis.normalized;

            Gizmos.color = FP_UtilityData.FPActiveColor;
            Vector3 origin = targetObject.position;
            Gizmos.DrawLine(origin, origin + worldAxis * 2.5f);

            if (orbitAroundPivot)
            {
                Vector3 center = (_hasCachedOrbit ? _cachedOrbitCenter : ComputePivotNow());
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(center, 0.05f);
                Gizmos.DrawLine(center, origin);
            }
        }
#endif
    }
}
