namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections;
    public class FP_SquishMotion : FP_MotionBase
    {
        [Header("Squish Settings")]
        [SerializeField]
        private Vector3 squishAmount = new Vector3(0.2f, -0.2f, 0f);

        [SerializeField]
        private AnimationCurve squishCurve =
            AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vector3 originalScale;
        [ContextMenu("Test Squish: Setup Motion")]
        public override void SetupMotion()
        {
            originalScale = targetObject.localScale;
        }

        public override void ResetMotion()
        {
            base.ResetMotion();
            if (targetObject != null)
            {
                targetObject.localScale = originalScale;
            }
        }
        public override void SetOverrideCurve(AnimationCurve curve, float d, Vector4 motionData)
        {
            squishCurve = curve != null ? curve : squishCurve;
            lerpDuration = d > 0f ? d : lerpDuration;
            var amp = new Vector3(motionData.x, motionData.y, motionData.z);
            squishAmount = amp != Vector3.zero ? amp : squishAmount;
        }
        protected override IEnumerator MotionRoutine()
        {
            float time = 0f;

            while (time < lerpDuration)
            {
                if (!isPaused)
                {
                    time += Time.deltaTime;
                    float t = Mathf.Clamp01(time / lerpDuration);
                    float intensity = squishCurve.Evaluate(t);

                    Vector3 offset = new Vector3(
                        1f + squishAmount.x * intensity,
                        1f + squishAmount.y * intensity,
                        1f + squishAmount.z * intensity
                    );

                    targetObject.localScale = Vector3.Scale(originalScale, offset);
                }

                yield return null;
            }

            targetObject.localScale = originalScale;
            EndMotion();
        }
    }
}
