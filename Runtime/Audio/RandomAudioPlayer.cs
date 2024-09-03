using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FuzzPhyte.Utility.Audio
{
  public class RandomAudioPlayer : MonoBehaviour
  {
     [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip[] audioClips;

    [SerializeField]
    private AnimationCurve probabilityCurve = AnimationCurve.Linear(0, 0, 1, 1);

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioClips == null || audioClips.Length == 0)
        {
            Debug.LogError("No audio clips assigned to the RandomAudioPlayer.");
        }

        if (probabilityCurve == null)
        {
            Debug.LogError("Probability curve not assigned. Using a default linear curve.");
            probabilityCurve = AnimationCurve.Linear(0, 0, 1, 1);
        }
    }

    public void PlayRandomClip()
    {
        if (audioClips != null && audioClips.Length > 0)
        {
            // Generate a random value between 0 and 1
            float randomValue = Random.Range(0f, 1f);

            // Evaluate the curve with the random value
            float curveValue = probabilityCurve.Evaluate(randomValue);

            // Map the curve value to an index in the audioClips array
            int selectedClipIndex = Mathf.FloorToInt(curveValue * audioClips.Length);

            // Ensure the index is within bounds
            selectedClipIndex = Mathf.Clamp(selectedClipIndex, 0, audioClips.Length - 1);

            // Play the selected clip
            audioSource.clip = audioClips[selectedClipIndex];
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No audio clips available to play.");
        }
    }
  }
}
