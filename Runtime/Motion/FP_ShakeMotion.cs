namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using System.Collections;
    public class FP_ShakeMotion : FP_MotionBase
    {
        [Header("Shake Settings")]
        [SerializeField]
        private Vector3 shakeAmplitude = new Vector3(0.05f, 0.05f, 0.05f);

        [SerializeField]
        private AnimationCurve shakeCurve =
            AnimationCurve.EaseInOut(0, 1, 1, 0);

        [SerializeField]
        private float frequency = 25f;

        [SerializeField]private Vector3 originalPosition;
        [ContextMenu("Test Shake: Setup Motion")]
        public override void SetupMotion()
        {
            originalPosition = targetObject.localPosition;
        }

        public override void ResetMotion()
        {
            base.ResetMotion();
            targetObject.localPosition = originalPosition;
        }
        public override void SetOverrideCurve(AnimationCurve curve,float d,Vector4 motionData)
        {
            shakeCurve = curve != null ? curve : shakeCurve;
            lerpDuration = d > 0f ? d : lerpDuration;
            var amp = new Vector3(motionData.x, motionData.y, motionData.z);
            shakeAmplitude = amp != Vector3.zero ? amp : shakeAmplitude;
            frequency = motionData.w > 0f ? motionData.w : frequency;
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
                    float intensity = shakeCurve.Evaluate(t);

                    Vector3 noise = new Vector3(
                        Mathf.Sin(time * frequency),
                        Mathf.Cos(time * frequency * 1.3f),
                        Mathf.Sin(time * frequency * 0.7f)
                    );

                    Vector3 offset = Vector3.Scale(noise, shakeAmplitude) * intensity;
                    targetObject.localPosition = originalPosition + offset;
                }

                yield return null;
            }

            targetObject.localPosition = originalPosition;
            EndMotion();
        }
    }
}
