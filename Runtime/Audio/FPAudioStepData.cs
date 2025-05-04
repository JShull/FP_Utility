namespace FuzzPhyte.Utility.Audio 
{
    using UnityEngine.Events;
    using UnityEngine;
    using System;
    [Serializable]
    [CreateAssetMenu(fileName = "FPAudioStepData", menuName = "FuzzPhyte/Audio/AudioStepData")]
    public class FPAudioStepData : FP_Data
    {
        [Tooltip("Clip we want to play")]
        public AudioClip TheClip;
        [Tooltip("If we want to start at a different time than 0")]
        public float ClipStartTime=0;
        [Tooltip("Float fed into our other system for a delay after this")]
        public float ClipDelayAfter = 2f;
    }

    /// <summary>
    /// Struct for Mono use
    /// </summary>
    [Serializable]
    public struct FPAudioSequenceStep
    {
        public FPAudioStepData StepData;
        public UnityEvent StepEvent;
    }
}
