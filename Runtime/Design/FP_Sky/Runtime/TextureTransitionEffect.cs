namespace FuzzPhyte.Utility
{

    using UnityEngine;
    using System;
    using System.Collections;
    /// <summary>
    /// Works with a custom shader to transition between two textures.
    /// </summary>
    public class TextureTransitionEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Material transitionMaterial;

        [Header("Textures")]
        [SerializeField] private Texture textureA;
        [SerializeField] private Texture textureB;

        [Header("Blend Settings")]
        [Range(-1.1f, 1.1f)] public float blendFactor = -1.1f;
        public Vector3 blendDirection = Vector3.right;
        public float blendEdgeWidth = 0.1f;
        public Color blendColor = Color.white;
        private Color blendTransitionColor = Color.white;
        public bool UseGradient = false;
        public bool UseEdgeThickness = true;
        public bool UsePingPong = false;
        public Gradient OverTimeBlendColor = new Gradient();
        [Header("Transition Control")]
        public float transitionDuration = 3f;
        public AnimationCurve edgeWidthCurve = AnimationCurve.EaseInOut(0, 0.1f, 1, 0.2f);

        public delegate void TextureTransitionEffectDelegate();
        public event TextureTransitionEffectDelegate OnTransitionStart;
        public event TextureTransitionEffectDelegate OnTransitionComplete;

        private Coroutine transitionRoutine;

        private void Start()
        {
            if (UseGradient)
            {
                blendTransitionColor = OverTimeBlendColor.Evaluate(0);
            }
            else
            {
                blendTransitionColor = blendColor;
            }

            ApplyMaterialValues();
        }

        [ContextMenu("Forward Transition")]
        public void TransitionTest()
        {
            StartTransition(true);
        }
        [ContextMenu("Reverse Transition")]
        public void ReverseTransitionTest()
        {
            StartTransition(false);
        }
        public void StartTransition(bool forward = true)
        {
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            if (UsePingPong)
            {
                transitionRoutine = StartCoroutine(TransitionLoopRoutine(forward));
            }
            else
            {
                transitionRoutine = StartCoroutine(TransitionRoutine(forward));
            }

        }

        private IEnumerator TransitionRoutine(bool forward)
        {
            OnTransitionStart?.Invoke();

            float elapsed = 0f;
            float start = forward ? -1.1f : 1.1f;
            float end = forward ? 1.1f : -1.1f;
            blendFactor = start;
            ApplyMaterialValues();
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);

                // Animate blend factor linearly
                blendFactor = Mathf.Lerp(start, end, t);

                // Animate edge width using the curve
                if (UseEdgeThickness)
                {
                    blendEdgeWidth = edgeWidthCurve.Evaluate(t);
                }
                else
                {
                    blendEdgeWidth = 0;
                }

                if (UseGradient)
                {
                    blendTransitionColor = OverTimeBlendColor.Evaluate(t);
                }
                else
                {
                    blendTransitionColor = blendColor;
                }
                ApplyMaterialValues();
                yield return null;
            }

            blendFactor = end;
            ApplyMaterialValues();
            OnTransitionComplete?.Invoke();
        }
        private IEnumerator TransitionLoopRoutine(bool forward)
        {
            OnTransitionStart?.Invoke();

            float start = forward ? -1.1f : 1.1f;
            float end = forward ? 1.1f : -1.1f;

            do
            {
                float elapsed = 0f;
                blendFactor = start;
                ApplyMaterialValues();

                while (elapsed < transitionDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / transitionDuration);

                    // Animate blend factor linearly
                    blendFactor = Mathf.Lerp(start, end, t);

                    // Animate edge width using the curve
                    if (UseEdgeThickness)
                        blendEdgeWidth = edgeWidthCurve.Evaluate(t);
                    else
                        blendEdgeWidth = 0;

                    // Animate transition color
                    if (UseGradient)
                        blendTransitionColor = OverTimeBlendColor.Evaluate(t);
                    else
                        blendTransitionColor = blendColor;

                    ApplyMaterialValues();
                    yield return null;
                }

                blendFactor = end;
                ApplyMaterialValues();
                OnTransitionComplete?.Invoke();

                // If ping-ponging, reverse direction and continue
                if (UsePingPong)
                {
                    yield return null; // optional pause
                    var temp = start;
                    start = end;
                    end = temp;
                    OnTransitionStart?.Invoke();
                }

            } while (UsePingPong);
        }



        private void ApplyMaterialValues()
        {
            if (!transitionMaterial) return;

            transitionMaterial.SetTexture("_TextureA", textureA);
            transitionMaterial.SetTexture("_TextureB", textureB);
            transitionMaterial.SetFloat("_BlendFactor", blendFactor);
            transitionMaterial.SetVector("_BlendDirection", blendDirection.normalized);
            transitionMaterial.SetFloat("_BlendEdgeWidth", blendEdgeWidth);
            transitionMaterial.SetColor("_BlendColor", blendTransitionColor);
        }
    }

}