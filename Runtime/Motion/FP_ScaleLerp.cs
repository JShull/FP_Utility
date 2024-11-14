namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections;
    public class FP_ScaleLerp : FP_MotionBase
    {
        [Space]
        [Header("Scale Lerp Settings")]
        [SerializeField]
        protected Vector3 startScale = Vector3.one;

        [SerializeField]
        protected Vector3 endScale = Vector3.one * 2;

        [SerializeField]
        protected AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        public override void ResetMotion()
        {
            base.ResetMotion();
            targetObject.localScale = startScale;
        }
        protected override IEnumerator MotionRoutine()
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
            EndMotion();
        }
        /// <summary>
        /// Custom Coroutine to lerp the scale of the target object between two points
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        private IEnumerator ScaleBetweenPoints(Vector3 from, Vector3 to)
        {
            float timeElapsed = 0f;

            while (timeElapsed < lerpDuration)
            {
                if (!isPaused)
                {
                    timeElapsed += Time.deltaTime;
                    float t = timeElapsed / lerpDuration;

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
        public override void OnDrawGizmos()
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
