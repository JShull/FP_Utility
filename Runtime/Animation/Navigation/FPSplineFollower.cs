namespace FuzzPhyte.Utility.Animation
{
    using UnityEngine;
    using UnityEngine.Splines;
    using Unity.Mathematics;
    //Designer workflow: drop a FP_SplineFollowTrack, bind to your NPC (with FP_SplineFollower).
    //Add clips that move from startT → endT over each clip’s duration. Use the curve for ease-in/out, holds, etc.
    public class FPSplineFollower : MonoBehaviour
    {
        [Header("Path")]
        public SplineContainer TheSpline;
        public Transform TheTarget;
        [Range(0f, 1f)] public float t = 0f;
        [Tooltip("If true, Timeline drives t via SetNormalizedT. If false, Update() advances t.")]
        public bool TimelineControl;

        [Header("Motion")]
        [Tooltip("Multiplier applied when clips drive t; useful for slow/fast motion.")]
        public float SpeedMultiplier = 1f;  // affects Timeline-driven movement
        public float NormalizedSpeedPerSecond = 0.2f; // used only when TimelineControl == false
        public bool OrientToTangent = true;
        public Vector3 TransformUp = Vector3.up;

        //Current runtime state
        public bool IsPaused => _paused;
        public bool IsStopped => _stopped;
        //When true, preview commands (e.g., from Timeline) will bypass pause/stop gates 
        public bool ForcePreviewIgnoreGates { get; set; }
        protected bool _paused;
        protected bool _stopped;

        public virtual void Update()
        {
            // If you also want free-run (not Timeline-driven), you can add optional logic here.
            // This follower is primarily driven by Timeline (SetNormalizedT).
            if (TimelineControl)
            {
                return;
            }
            // Free-run mode: we own advancing t here.
            if (_stopped || _paused) return;

            t += NormalizedSpeedPerSecond * SpeedMultiplier * Time.deltaTime;
            t = Mathf.Clamp01(t); // or Mathf.Repeat for looping
            UpdateTransform();
        }

        public void SetNormalizedT(float value)
        {
            // If forced preview override is active, ignore pause/stop gates
            if (!ForcePreviewIgnoreGates && (_stopped || _paused))
                return;

            t = Mathf.Clamp01(value);
            UpdateTransform();
        }

        public void WarpToNormalizedT(float value)  // ignores pause/stop; for teleports
        {
            t = Mathf.Clamp01(value);
            UpdateTransform();
        }

        public void SetPaused(bool paused) => _paused = paused;
        public void Pause() => _paused = true;
        public void Resume() => _paused = false;
        public void Stop() => _stopped = true;
        public void Unstop() => _stopped = false;
        public void SetSpeedMultiplier(float m) => SpeedMultiplier = Mathf.Max(0f, m);

        protected void UpdateTransform()
        {
            if (TheSpline == null || TheSpline.Spline == null || TheTarget==null) return;

            var sp = TheSpline.Spline;

            Vector3 localPos = (Vector3) sp.EvaluatePosition(t);
            Vector3 worldPos = TheSpline.transform.TransformPoint(localPos);
            TheTarget.position = worldPos;

            if (OrientToTangent)
            {
                Vector3 localTan = (Vector3)sp.EvaluateTangent(t);
                Vector3 worldTan = TheSpline.transform.TransformVector(localTan);
                if (worldTan.sqrMagnitude > 1e-6f)
                {
                    Vector3 worldUp = TheSpline.transform.TransformDirection(TransformUp);
                    TheTarget.rotation = Quaternion.LookRotation(worldTan, worldUp);
                }
            }
        }
    }
}
