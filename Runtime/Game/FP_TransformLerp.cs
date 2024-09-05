namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;

    public class FP_TransformLerp : MonoBehaviour
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
        
        private void Start()
        {
            if (targetObject == null)
            {
                targetObject = transform;
            }
            ResetMovement();
            if(playOnStart)
            {
                StartMovement();
            }
        }
        
        public void StartMovement()
        {
            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
            }
            isPaused = false;
            moveCoroutine = StartCoroutine(MoveTransform());
        }
        
        public void PauseMovement()
        {
            isPaused = true;
        }
        
        public void ResumeMovement()
        {
            if (isPaused && moveCoroutine != null)
            {
                isPaused = false;
            }
        }
        
        public void ResetMovement()
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
    }
}

