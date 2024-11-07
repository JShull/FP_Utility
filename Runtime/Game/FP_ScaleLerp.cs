
namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections;
    public class FP_ScaleLerp : MonoBehaviour,IFPLerpController
    {
        [SerializeField]
        private Transform targetObject;

        [SerializeField]
        private Vector3 startScale = Vector3.one;

        [SerializeField]
        private Vector3 endScale = Vector3.one * 2;

        [SerializeField]
        private AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [SerializeField]
        private float duration = 3.0f;

        [SerializeField]
        private bool loop = false;

        [SerializeField]
        private bool playOnStart = true;

        private bool isPaused = true;
        private Coroutine scaleCoroutine;

        public void SetupLerp()
        {
            if (targetObject == null)
            {
                targetObject = transform;
            }
            ResetLerp();
            if (playOnStart)
            {
                StartLerp();
            }
        }
        public void StartLerp()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            isPaused = false;
            scaleCoroutine = StartCoroutine(ScaleTransform());
        }

        public void PauseLerp()
        {
            isPaused = true;
        }

        public void ResumeLerp()
        {
            if (isPaused && scaleCoroutine != null)
            {
                isPaused = false;
            }
        }

        public void ResetLerp()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }

            targetObject.localScale = startScale;
            isPaused = true;
        }
        public void EndLerp()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
        }
        private IEnumerator ScaleTransform()
        {
            do
            {
                yield return StartCoroutine(ScaleBetweenPoints(startScale, endScale));

                if (loop)
                {
                    // Swap startScale and endScale for the next loop
                    yield return StartCoroutine(ScaleBetweenPoints(endScale, startScale));
                }

            } while (loop);
        }

        private IEnumerator ScaleBetweenPoints(Vector3 from, Vector3 to)
        {
            float timeElapsed = 0f;

            while (timeElapsed < duration)
            {
                if (!isPaused)
                {
                    timeElapsed += Time.deltaTime;
                    float t = timeElapsed / duration;

                    // Sample the AnimationCurve
                    float curveValue = scaleCurve.Evaluate(t);

                    // Use the curve value to interpolate the scale
                    targetObject.localScale = Vector3.Lerp(from, to, curveValue);
                }
                yield return null;
            }

            // Ensure it ends at the exact end scale
            targetObject.localScale = to;
        }
        public void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if(UnityEditor.Selection.activeGameObject == this.gameObject)
            {
                var scaleLerpStyle = new GUIStyle();
               
                scaleLerpStyle.normal.textColor = Color.yellow;
               
                scaleLerpStyle.fontSize = 12; // Set the font size if needed
                scaleLerpStyle.alignment = TextAnchor.MiddleCenter; // Center alignment
                UnityEditor.Handles.Label(this.transform.position+new Vector3(0,(endScale.y/2f)+0.05f,0), "Scale Lerp", scaleLerpStyle);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(this.transform.position, (startScale.x / 2f));
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(this.transform.position, (endScale.x / 2f));
            }
#endif
        }
    }
}
