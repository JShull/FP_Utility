namespace FuzzPhyte.Utility
{
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using System.Collections;
    using UnityEngine;

    public class FP_FollowMotion : FP_MotionBase
    {
        [Header("Follow Settings")]
        [SerializeField]
        protected Transform followTarget;

        public Transform FollowTarget
        {
            get => followTarget;
            set => followTarget = value;
        }

        [SerializeField]
        protected AnimationCurve followCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField]
        protected bool localSpace = false;

        [SerializeField]
        [Tooltip("If true, rotation will also follow.")]
        protected bool followRotation = false;

        [SerializeField]
        [Tooltip("Distance threshold before snapping to target.")]
        protected float snapDistance = 0.001f;

        private Vector3 velocity; // Optional smoothing support

        protected override IEnumerator MotionRoutine()
        {
            if (followTarget == null)
            {
                yield break;
            }

            float timeElapsed = 0f;

            do
            {
                if (!isPaused)
                {
                    timeElapsed += Time.deltaTime;

                    float normalizedTime = lerpDuration > 0f
                        ? Mathf.Clamp01(timeElapsed / lerpDuration)
                        : 1f;

                    float curveValue = followCurve.Evaluate(normalizedTime);

                    UpdateFollow(curveValue);

                    // Optional reset timer if looping curve response
                    if (loop && normalizedTime >= 1f)
                    {
                        timeElapsed = 0f;
                    }
                }

                yield return null;

            } while (loop || true); // continuous follow unless explicitly ended
        }

        private void UpdateFollow(float curveValue)
        {
            if (followTarget == null || targetObject == null)
                return;

            Vector3 currentPos = localSpace
                ? targetObject.localPosition
                : targetObject.position;

            Vector3 targetPos = localSpace
                ? followTarget.localPosition
                : followTarget.position;

            float distance = Vector3.Distance(currentPos, targetPos);

            if (distance <= snapDistance)
            {
                if (localSpace)
                    targetObject.localPosition = targetPos;
                else
                    targetObject.position = targetPos;
            }
            else
            {
                Vector3 newPos = Vector3.Lerp(currentPos, targetPos, curveValue * Time.deltaTime);

                if (localSpace)
                    targetObject.localPosition = newPos;
                else
                    targetObject.position = newPos;
            }

            if (followRotation)
            {
                Quaternion currentRot = localSpace
                    ? targetObject.localRotation
                    : targetObject.rotation;

                Quaternion targetRot = localSpace
                    ? followTarget.localRotation
                    : followTarget.rotation;

                Quaternion newRot = Quaternion.Slerp(
                    currentRot,
                    targetRot,
                    curveValue * Time.deltaTime
                );

                if (localSpace)
                    targetObject.localRotation = newRot;
                else
                    targetObject.rotation = newRot;
            }
        }

        public override void ResetMotion()
        {
            base.ResetMotion();

            if (followTarget == null || targetObject == null)
                return;

            if (localSpace)
                targetObject.localPosition = followTarget.localPosition;
            else
                targetObject.position = followTarget.position;

            if (followRotation)
            {
                if (localSpace)
                    targetObject.localRotation = followTarget.localRotation;
                else
                    targetObject.rotation = followTarget.rotation;
            }
        }
        public override void OnDrawGizmos()
        {
            if (targetObject == null || followTarget == null)
                return;

#if UNITY_EDITOR
            if (UnityEditor.Selection.activeGameObject != this.gameObject)
                return;


            Vector3 currentPos = localSpace
                ? targetObject.localPosition
                : targetObject.position;

            Vector3 targetPos = localSpace
                ? followTarget.localPosition
                : followTarget.position;

            float distance = Vector3.Distance(currentPos, targetPos);

            // ---------- Draw connection line ----------
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(currentPos, targetPos);

            // ---------- Draw target position ----------
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetPos, HandleUtility.GetHandleSize(targetPos) * 0.1f);

            // ---------- Draw follower position ----------
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentPos, HandleUtility.GetHandleSize(currentPos) * 0.075f);

            // ---------- Snap radius ----------
            if (snapDistance > 0f)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
                Gizmos.DrawWireSphere(targetPos, snapDistance);
            }

            // ---------- Direction Arrow ----------
            Vector3 dir = (targetPos - currentPos).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float arrowSize = HandleUtility.GetHandleSize(currentPos) * 0.25f;
                Vector3 arrowTip = currentPos + dir * arrowSize;

                Gizmos.color = Color.red;
                Gizmos.DrawLine(currentPos, arrowTip);
                Gizmos.DrawSphere(arrowTip, arrowSize * 0.2f);
            }
            // ---------- Debug Label ----------
            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 12;
            style.alignment = TextAnchor.MiddleCenter;

            UnityEditor.Handles.Label(
                targetPos + Vector3.up * HandleUtility.GetHandleSize(targetPos) * 0.4f,
                $"Distance: {distance:F3}",
                style
            );
#endif
        }
    }
}
