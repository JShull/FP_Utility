namespace FuzzPhyte.Utility
{
    using System.Collections;
    
    using UnityEngine;

    public class FP_TransformLerp : MonoBehaviour, IFPMotionController
    {
        [SerializeField]
        private Transform targetObject;
        
        [SerializeField]
        private Transform startPoint;
        
        [SerializeField]
        private Transform endPoint;
        
        [SerializeField]
        private AnimationCurve movementCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [SerializeField]
        private float duration = 3.0f;
        
        [SerializeField]
        private bool loop = false;
        [SerializeField]
        private bool localTransform = false;
        
        [SerializeField]
        private bool playOnStart = true;
        
        private bool isPaused = true;
        private Coroutine moveCoroutine;
        
        public void SetupMotion()
        {
            if (targetObject == null)
            {
                targetObject = transform;
            }
            ResetMotion();
            if (playOnStart)
            {
                StartMotion();
            }
        }
        
        public void StartMotion()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
            }
            isPaused = false;
            moveCoroutine = StartCoroutine(MoveTransform());
        }
        
        public void PauseMotion()
        {
            isPaused = true;
        }
        
        public void ResumeMotion()
        {
            if (isPaused && moveCoroutine != null)
            {
                isPaused = false;
            }
        }
        public void ResetMotion()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
            }
            if (localTransform)
            {
                targetObject.transform.localPosition = startPoint.localPosition;
            }
            else
            {
                targetObject.transform.position = startPoint.position;
            }
            
            isPaused = true;
        }
        public void EndMotion()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
            }
        }
        
        private IEnumerator MoveTransform()
        {
            do
            {
                if (localTransform)
                {
                    yield return StartCoroutine(MoveBetweenPoints(startPoint.localPosition, endPoint.localPosition));
                }
                else
                {
                    yield return StartCoroutine(MoveBetweenPoints(startPoint.position, endPoint.position));
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
        }
        
        private IEnumerator MoveBetweenPoints(Vector3 from, Vector3 to)
        {
            float timeElapsed = 0f;
        
            while (timeElapsed < duration)
            {
                if (!isPaused)
                {
                    timeElapsed += Time.deltaTime;
                    float t = timeElapsed / duration;
        
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

        public void OnDrawGizmos()
        {
            if(startPoint == null || endPoint == null)
            {
                return;
            }
#if UNITY_EDITOR
            if(UnityEditor.Selection.activeGameObject == this.gameObject)
            {
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
            }
#endif
           
        }
    }
}

