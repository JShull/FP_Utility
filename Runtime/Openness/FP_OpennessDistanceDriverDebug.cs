namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections;
    public class FP_OpennessDistanceDriverDebug : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform closedPoint;
        [SerializeField] private Transform openPoint;
        [SerializeField] private Transform movingPart;
        [SerializeField] private FP_OpennessStateTracker tracker;

        [Header("Distance Settings")]
        [Tooltip("Distance from closed to fully open.")]
        [Min(0.0001f)]
        [SerializeField] private float fullyOpenDistance = 0.5f;

        [Tooltip("Clamp normalized value between 0 and 1.")]
        [SerializeField] private bool clampNormalized = true;

        [Header("Debug")]
        [SerializeField] private bool isGrabbed = false;
        [SerializeField] private float currentRawDistance;
        [SerializeField] private float currentNormalized;

        private void Awake()
        {
            fullyOpenDistance = Vector3.Distance(closedPoint.position, openPoint.position);
        }
        private void Update()
        {
            if (!isGrabbed) return;
            if (closedPoint == null || movingPart == null || tracker == null) return;

            currentRawDistance = Vector3.Distance(closedPoint.position, movingPart.position);

            if (fullyOpenDistance <= 0f)
            {
                currentNormalized = 0f;
            }
            else
            {
                currentNormalized = currentRawDistance / fullyOpenDistance;
                if (clampNormalized)
                    currentNormalized = Mathf.Clamp01(currentNormalized);
            }

            tracker.UpdateNormalized(currentNormalized);
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

        void OnDrawGizmosSelected()
        {
            if (closedPoint == null || movingPart == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(closedPoint.position, movingPart.position);
            Gizmos.color = Color.orange;
            Gizmos.DrawLine(closedPoint.position, openPoint.position);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(closedPoint.position, 0.05f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(openPoint.position, 0.05f);
        }
    }
}
