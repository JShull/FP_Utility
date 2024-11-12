namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Events;
    using TMPro;
    using FuzzPhyte.Utility;

    public class FP_AlignLerp : FP_MotionBase
    {
        [Space]
        [Header("Align Lerp Settings")]
        [Tooltip("The target transform to align with")]
        [SerializeField]
        protected Transform targetAligned;
      
        [Space]
        [Header("Motion Settings")]
        public Vector3 offset = new Vector3(0, -0.5f, 0); // Position offset
       
        public AnimationCurve positionCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve rotationCurve = AnimationCurve.Linear(0, 0, 1, 1); // Separate curve for rotation lerp

        [Space]
        [Header("Timer Based Options")]
        public bool useTimer = false;
        public float timerDuration = 5f;
        [Space]
        [Header("Rotation Parameters")]
        public bool useRotation = true;

        protected Vector3 lastPosition;
        protected Quaternion lastRotation;
        protected Vector3 targetPosition;
        protected Quaternion targetRotation;
        protected FP_Timer testTimer;

        public override void SetupMotion()
        {
            if (targetAligned == null)
            {
                Debug.LogError("TargetAligned or object to move is not set.");
                return;
            }
            base.SetupMotion();
            
        }

        public override void StartMotion()
        {
            base.StartMotion();
            // Check if timer should be used
            if (useTimer)
            {
                testTimer = FP_Timer.CCTimer;
                if (testTimer == null)
                {
                    Debug.LogError("No Timer Found");
                    return;
                }
                testTimer.StartTimer(timerDuration, EndMotion);  // Start the timer and call EndMotion after timerDuration
            }
        }

        public override void ResetMotion()
        {
            base.ResetMotion();
            lastPosition = targetAligned.position + offset;
            lastRotation = targetAligned.rotation;
            targetObject.position = lastPosition;
            targetObject.rotation = lastRotation;
        }
        
        protected override IEnumerator MotionRoutine()
        {
            do
            {
                if (!isPaused)
                {
                    yield return StartCoroutine(AnimateToTargetPositionAndRotation());
                }
                yield return null;
            } while (loop);
        }

        private IEnumerator AnimateToTargetPositionAndRotation()
        {
            float elapsedTime = 0f;
            lastPosition = targetObject.position;
            lastRotation = targetObject.rotation;
            targetPosition = targetAligned.position + offset;
            targetRotation = targetAligned.rotation;

            while (elapsedTime < lerpDuration)
            {
                if (!isPaused)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / lerpDuration);

                    // Use curves to control both position and rotation interpolation
                    float positionCurveValue = positionCurve.Evaluate(t);
                    float rotationCurveValue = rotationCurve.Evaluate(t);

                    targetObject.position = Vector3.Lerp(lastPosition, targetPosition, positionCurveValue);
                    if (useRotation)
                    {
                        targetObject.rotation = Quaternion.Slerp(lastRotation, targetRotation, rotationCurveValue);
                    }
                    
                }
                targetPosition = targetAligned.position + offset;
                targetRotation = targetAligned.rotation;
                yield return null;
            }

            // Ensure the final position and rotation align precisely
            targetObject.position = targetPosition;
            if (useRotation)
            {
                targetObject.rotation = targetRotation;
            }
            
        }

        public override void OnDrawGizmos()
        {
            if (targetAligned == null) return;
#if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject == this.gameObject)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(targetAligned.position + offset, 0.2f); // Visualize target position
            }
#endif
        }
    }

}
