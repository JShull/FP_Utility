namespace FuzzPhyte.Utility
{
    using System.Collections;
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine;

    public class FP_TransformLerp : FP_MotionBase
    {
        [Space]
        [Header("Transform Lerp Settings")]
        [SerializeField]
        protected Transform startPoint;
        public Transform StartPosition { get => startPoint; set => startPoint = value; }
        [SerializeField]
        protected Transform endPoint;
        public Transform EndPosition { get => endPoint; set => endPoint = value; }

        [SerializeField]
        protected AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [SerializeField]
        protected bool localTransform = false;
        public bool LocalTransform { get => localTransform; set => localTransform = value; }
        [SerializeField]
        protected bool playOnStart = true;
        [SerializeField]
        protected bool returnToStart = false;
        public bool ReturnToStart { get => returnToStart; set => returnToStart = value; }

        public override void ResetMotion()
        {
            base.ResetMotion();
            if (localTransform)
            {
                targetObject.transform.localPosition = startPoint.localPosition;
            }
            else
            {
                targetObject.transform.position = startPoint.position;
            }
        }

        protected override IEnumerator MotionRoutine()
        {
            do
            {
                // Start-->End Forward motion
                if (localTransform)
                {
                    yield return StartCoroutine(MoveBetweenPoints(startPoint.localPosition, endPoint.localPosition));
                }
                else
                {
                    yield return StartCoroutine(MoveBetweenPoints(startPoint.position, endPoint.position));
                }

                // Ping Pong Start-->End-->Start once
                if (returnToStart && !loop)
                {
                    if (localTransform)
                    {
                        yield return StartCoroutine(
                            MoveBetweenPoints(endPoint.localPosition, startPoint.localPosition)
                        );
                    }
                    else
                    {
                        yield return StartCoroutine(
                            MoveBetweenPoints(endPoint.position, startPoint.position)
                        );
                    }
                }
                if (loop)
                {
                    // Swap startPoint and endPoint for the next loop
                    if (localTransform)
                    {
                        yield return StartCoroutine(MoveBetweenPoints(endPoint.localPosition,startPoint.localPosition));
                    }
                    else
                    {
                        yield return StartCoroutine(MoveBetweenPoints(endPoint.position, startPoint.position));
                    }
                    
                }
        
            } while (loop);
            EndMotion();
        }
        
        private IEnumerator MoveBetweenPoints(Vector3 from, Vector3 to)
        {
            float timeElapsed = 0f;
        
            while (timeElapsed < lerpDuration)
            {
                if (!isPaused)
                {
                    timeElapsed += Time.deltaTime;
                    float t = timeElapsed / lerpDuration;
        
                    // Sample the AnimationCurve
                    float curveValue = movementCurve.Evaluate(t);
        
                    // Use the curve value to interpolate the position
                    if (localTransform)
                    {
                        targetObject.transform.localPosition = Vector3.Lerp(from, to, curveValue);
                    }
                    else
                    {
                        targetObject.transform.position = Vector3.Lerp(from, to, curveValue);
                    }
                }
                yield return null;
            }
            // Ensure it ends at the exact end position
            if (localTransform)
            {
                targetObject.transform.localPosition = to;
            }
            else
            {
                targetObject.transform.position = to;
            }
            //targetObject.transform.position = to;
        }

        public override void OnDrawGizmos()
        {
            if(startPoint == null || endPoint == null)
            {
                return;
            }
#if UNITY_EDITOR
            if(UnityEditor.Selection.activeGameObject == this.gameObject)
            {
                var startFontStyle = new GUIStyle();
                var endFontStyle = new GUIStyle();

                startFontStyle.normal.textColor = Color.green;
                endFontStyle.normal.textColor = Color.cyan;

                startFontStyle.fontSize = endFontStyle.fontSize = 12;
                startFontStyle.alignment = endFontStyle.alignment = TextAnchor.MiddleCenter;

#if UNITY_EDITOR

                // 🔹 Get camera-relative handle sizes
                float startSize = HandleUtility.GetHandleSize(startPoint.position) * 0.1f;
                float endSize = HandleUtility.GetHandleSize(endPoint.position) * 0.1f;

                // Labels
                Handles.Label(
                    startPoint.position + Vector3.up * startSize * 1.5f,
                    "START",
                    startFontStyle
                );

                Handles.Label(
                    endPoint.position + Vector3.up * endSize * 1.5f,
                    "END",
                    endFontStyle
                );

                // Draw START
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(startPoint.position, startSize);

                // Draw path
                Gizmos.DrawLine(startPoint.position, endPoint.position);

                // Draw END
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(endPoint.position, endSize);

#endif
                /*
                var  startFontStyle = new GUIStyle();
                var endFontStyle = new GUIStyle();
                startFontStyle.normal.textColor = Color.green;
                endFontStyle.normal.textColor = Color.cyan;
                startFontStyle.fontSize = endFontStyle.fontSize=12; // Set the font size if needed
                startFontStyle.alignment = endFontStyle.alignment = TextAnchor.MiddleCenter; // Center alignment
                UnityEditor.Handles.Label(startPoint.position + new Vector3(0, 0.225f, 0), "START", startFontStyle);
                UnityEditor.Handles.Label(endPoint.position + new Vector3(0, 0.225f, 0), "END", endFontStyle);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(startPoint.position, 0.2f);
                Gizmos.DrawLineStrip(new Vector3[] { startPoint.position, endPoint.position }, false);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(endPoint.position, 0.2f);
                */
            }
#endif
        }
    }
}
