namespace FuzzPhyte.Utility.Audio
{
    using System.Collections.Generic;
    using System.Collections;
    using UnityEngine.Audio;
    using UnityEngine;

    /// <summary>
    /// Duck audio based on needs.
    /// Uses coroutines and keeps tabs on who's running to allow for multiple and simutaneous mix needs
    /// </summary>
    public class FPAudioMixerController : MonoBehaviour
    {
        [SerializeField] protected AudioMixer mixer;
        [SerializeField] protected float fadeDuration = 1.0f;

        protected Dictionary<string, float> originalValues = new();
        protected Dictionary<string, Coroutine> activeCoroutines = new();

        /// <summary>
        /// Fades the volume of an exposed mixer parameter down to a target value.
        /// </summary>
        public void FadeDown(string exposedParam, float targetVolumeDb)
        {
            if (!originalValues.ContainsKey(exposedParam))
            {
                if (mixer.GetFloat(exposedParam, out float currentVal))
                    originalValues[exposedParam] = currentVal;
                else
                    Debug.LogWarning($"Exposed parameter '{exposedParam}' not found in mixer.");
            }

            StartFade(exposedParam, targetVolumeDb);
        }

        /// <summary>
        /// Restores the volume of the exposed parameter to its cached original value.
        /// </summary>
        public void RestoreVolume(string exposedParam)
        {
            if (originalValues.TryGetValue(exposedParam, out float originalVal))
            {
                StartFade(exposedParam, originalVal);
            }
            else
            {
                Debug.LogWarning($"No cached value for '{exposedParam}'. Cannot restore.");
            }
        }

        /// <summary>
        /// Starts a coroutine to fade a parameter to a given value.
        /// </summary>
        protected virtual void StartFade(string param, float target)
        {
            if (activeCoroutines.TryGetValue(param, out Coroutine existing))
                StopCoroutine(existing);

            Coroutine routine = StartCoroutine(FadeParameter(param, target));
            activeCoroutines[param] = routine;
        }

        protected virtual IEnumerator FadeParameter(string exposedParam, float targetVolume)
        {
            if (!mixer.GetFloat(exposedParam, out float startVolume))
                yield break;

            float time = 0f;

            while (time < fadeDuration)
            {
                float newVol = Mathf.Lerp(startVolume, targetVolume, time / fadeDuration);
                mixer.SetFloat(exposedParam, newVol);
                time += Time.unscaledDeltaTime; // unscaled in case timescale = 0
                yield return null;
            }

            mixer.SetFloat(exposedParam, targetVolume);
            activeCoroutines.Remove(exposedParam);
        }
    }
}
