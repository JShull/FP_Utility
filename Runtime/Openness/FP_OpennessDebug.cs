namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public class FP_OpennessDebug : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FP_OpennessStateTracker tracker;

        [Header("Debug Value (Normalized 0..1)")]
        [Range(0f, 1f)]
        [SerializeField] private float debugNormalized = 0f;

        [SerializeField] private float stepAmount = 0.1f;

        [Header("Logging")]
        [SerializeField] private bool logStateChanges = true;
        [SerializeField] private bool logDirectionChanges = true;
        [SerializeField] private bool logThresholds = true;

        private void Awake()
        {
            if (tracker == null)
                tracker = GetComponent<FP_OpennessStateTracker>();

            if (tracker != null)
            {
                tracker.StateChanged += OnStateChanged;
                tracker.DirectionChanged += OnDirectionChanged;

                if (logThresholds)
                {
                    tracker.OnCrossOpenUp.AddListener(() => Debug.Log("Cross Open UP"));
                    tracker.OnCrossPartialUp.AddListener(() => Debug.Log("Cross Partial UP"));
                    tracker.OnCrossFullyOpenUp.AddListener(() => Debug.Log("Cross FullyOpen UP"));

                    tracker.OnCrossOpenDownOneShot.AddListener(() => Debug.Log("Cross Open DOWN"));
                    tracker.OnCrossPartialDownOneShot.AddListener(() => Debug.Log("Cross Partial DOWN"));
                    tracker.OnCrossFullyOpenDownOneShot.AddListener(() => Debug.Log("Cross FullyOpen DOWN"));

                    tracker.OnReturnClosed.AddListener(() => Debug.Log("Returned to CLOSED (cycle reset)"));
                }
            }
        }

        #region Context Menu Controls

        [ContextMenu("DEBUG: Start Interaction (Grab)")]
        public void DebugStart()
        {
            if (tracker == null) return;

            Debug.Log("=== START INTERACTION ===");
            tracker.StartMotion();
        }

        [ContextMenu("DEBUG: End Interaction (Drop)")]
        public void DebugEnd()
        {
            if (tracker == null) return;

            Debug.Log("=== END INTERACTION ===");
            tracker.EndMotion();
        }

        [ContextMenu("DEBUG: Apply Current Normalized")]
        public void DebugApply()
        {
            if (tracker == null) return;

            tracker.UpdateNormalized(debugNormalized);
            Debug.Log($"Applied Normalized: {debugNormalized:0.00}");
        }

        [ContextMenu("DEBUG: Increase")]
        public void DebugIncrease()
        {
            debugNormalized = Mathf.Clamp01(debugNormalized + stepAmount);
            tracker.UpdateNormalized(debugNormalized);
            Debug.Log($"Increase → {debugNormalized:0.00}");
        }

        [ContextMenu("DEBUG: Decrease")]
        public void DebugDecrease()
        {
            debugNormalized = Mathf.Clamp01(debugNormalized - stepAmount);
            tracker.UpdateNormalized(debugNormalized);
            Debug.Log($"Decrease → {debugNormalized:0.00}");
        }

        [ContextMenu("DEBUG: Reset To Zero")]
        public void DebugResetValue()
        {
            debugNormalized = 0f;
            tracker.UpdateNormalized(debugNormalized);
            Debug.Log("Value Reset To 0");
        }

        #endregion

        private void OnStateChanged(OpennessState prev, OpennessState next)
        {
            if (!logStateChanges) return;
            Debug.Log($"State Changed: {prev} → {next}");
        }

        private void OnDirectionChanged(OpennessDirection dir)
        {
            if (!logDirectionChanges) return;
            Debug.Log($"Direction: {dir}");
        }
    }
}
