namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.UI;
    using System.Collections;
    public class FP_Fade : MonoBehaviour,IFPOnStartSetup
    {
        [Header("3D Object Settings")]
        [Tooltip("Optional: Reference a 3D object's MeshRenderer to fade.")]
        public MeshRenderer objectRenderer;
        protected Material objectMaterial;
        protected Color objectStartColor;

        [Header("UI Image Settings")]
        [Tooltip("Optional: Reference a UI Image to fade.")]
        public Image canvasImage;
        protected Color imageStartColor;

        [Header("Fade Settings")]
        [Tooltip("Duration of the fade effect in seconds.")]
        public float fadeDuration = 1f;
        [Tooltip("If we want to make it not linear")]
        public AnimationCurve FadeCurve;
        [SerializeField] protected bool onStartFade;
        [SerializeField] protected bool startFadeIn;
        public bool SetupStart { get => onStartFade; set => onStartFade=value; }
        public delegate void FadeHandler(MeshRenderer mesh, Image canvasImg);
        public event FadeHandler OnFadeStarted;
        public event FadeHandler OnFadeEnded;

        public virtual void Start()
        {
            if (objectRenderer != null)
            {
                objectMaterial = objectRenderer.material;
                objectStartColor = objectMaterial.color;
                //check if objectMaterial is transparent
                if (!FP_UtilityData.IsMaterialTransparent(objectMaterial))
                {
                    Debug.LogError($"Your material isn't set for transparency - please adjust");
                }
            }

            if (canvasImage != null)
            {
                imageStartColor = canvasImage.color;
            }
            if (SetupStart)
            {
                if (startFadeIn)
                {
                    StartFadeIn();
                }
                else
                {
                    StartFadeOut();
                }
            }
        }

        public virtual void StartFadeOut()
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(1f, 0f)); // Fade to transparent
        }

        public virtual void StartFadeIn()
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(0f, 1f)); // Fade to opaque
        }
        protected virtual IEnumerator FadeRoutine(float startAlpha, float endAlpha)
        {
            float elapsedTime = 0f;
            OnFadeStarted?.Invoke(objectRenderer, canvasImage);
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                
                float alpha = Mathf.Lerp(startAlpha, endAlpha, FadeCurve.Evaluate(elapsedTime / fadeDuration));

                if (objectMaterial != null)
                {
                    Color newColor = objectStartColor;
                    newColor.a = alpha;
                    objectMaterial.color = newColor;
                }

                if (canvasImage != null )
                {
                    Color newColor = imageStartColor;
                    newColor.a = alpha;
                    canvasImage.color = newColor;
                }

                yield return null;
            }

            // Ensure final state
            if (objectMaterial != null )
            {
                Color finalColor = objectStartColor;
                finalColor.a = endAlpha;
                objectMaterial.color = finalColor;
            }

            if (canvasImage != null)
            {
                Color finalColor = imageStartColor;
                finalColor.a = endAlpha;
                canvasImage.color = finalColor;
            }
            OnFadeEnded?.Invoke(objectRenderer, canvasImage);
        }
    }
}