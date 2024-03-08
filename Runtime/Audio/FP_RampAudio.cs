using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FuzzPhyte.Utility.Audio
{
    public class FP_RampAudio : MonoBehaviour
    {
        [Header("Audio Ramp Settings")]
        public AudioSource FPAudioSource;
        public AnimationCurve FadeInCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve FadeOutCurve = AnimationCurve.Linear(0, 1, 1, 0);
        public bool FadeIn = false;
        public bool FadeOut = false;
        public float FadeInDuration = 2.0f;
        public float FadeOutDuration = 2.0f;
        [Space]
        public bool PlayOnStart = false;
        [Space]
        [Header("Clip Settings")]
        public float StartTime = 0f; // Start time of the audio clip to play
        public float EndTime = 0f; // End time of the audio clip to play, set to 0 to use full clip
        [Header("Override Loop with Clip?")]
        public bool CustomLoop = false; // Enable custom looping
        [Tooltip("If 0 = will loop indefinitely, 1 will only play once, 2 will play twice, etc")]
        public int TimesToLoop = 0;//if 0, loop indefinitely
        [Space]
        [Header("Additional Events")]
        public UnityEvent AudioStartEvent;
        public UnityEvent AudioFadeInEndEvent;
        public UnityEvent AudioFadeOutStartEvent;
        public UnityEvent AudioFinishedEvent;
        public UnityEvent LoopEvent;

        private float fadeInEndTime;
        private float fadeOutStartTime;
        private bool isFadingIn = true;
        private bool isFadingOut = false;
        private bool isActive = false;
        private bool audioEnded = false;
        private int loopCount = 0;

        void Start()
        {
            if (FPAudioSource == null)
            {
                FPAudioSource = GetComponent<AudioSource>();
                if (FPAudioSource == null)
                {
                    Debug.LogError($"No audio source associated with {gameObject.name} component");
                    return;
                }
            }

            // Validate and adjust EndTime if necessary
            if (EndTime <= 0f || EndTime > FPAudioSource.clip.length)
            {
                EndTime = FPAudioSource.clip.length;
            }

            // Adjust fadeOutStartTime based on EndTime
            fadeOutStartTime = EndTime - FadeOutDuration;
           
            
            if(FadeIn || FadeOut)
            {
                //if we are fading we want to at least start the volume at 0
                FPAudioSource.volume = 0;
            }
            if (PlayOnStart)
            {
                ActivateRamp();
            }
        }

        public void ActivateRamp()
        {
            isActive = true;
            FPAudioSource.time = StartTime; // Start playing from the specified start time
            if (FadeIn)
            {
                fadeInEndTime = Time.time + FadeInDuration;
            }
            SetupInitialVolume();
            FPAudioSource.Play();
            AudioStartEvent.Invoke();
        }
        public void BreakLoop()
        {
            CustomLoop = false;
        }
        void SetupInitialVolume()
        {
            if (FadeIn)
            {
                FPAudioSource.volume = FadeInCurve.Evaluate(0);
            }
            else if (FadeOut)
            {
                FPAudioSource.volume = FadeOutCurve.Evaluate(0); // Assume volume by anim clip start
            }
        }
        void Update()
        {
            if (!isActive || audioEnded) return;

            HandleFading();

            // Custom loop handling
            if (CustomLoop && FPAudioSource.time >= EndTime)
            {
                LoopAudio();
            }
            if(FPAudioSource.time >= EndTime && !CustomLoop)
            {
                audioEnded = true;
                AudioFinishedEvent.Invoke();
                FPAudioSource.Stop();
            }
            
        }
        void HandleFading()
        {
            // Fade in logic
            if (FadeIn && isFadingIn)
            {
                float normalizedTime = Mathf.InverseLerp((fadeInEndTime - FadeInDuration), fadeInEndTime, Time.time);
                FPAudioSource.volume = FadeInCurve.Evaluate(normalizedTime);

                if (Time.time >= fadeInEndTime)
                {
                    isFadingIn = false;
                    AudioFadeInEndEvent.Invoke();
                }
            }

            // Fade out logic
            if (FadeOut && isFadingOut)
            {
                float normalizedTime = Mathf.InverseLerp(fadeOutStartTime, EndTime, FPAudioSource.time);
                FPAudioSource.volume = FadeOutCurve.Evaluate(normalizedTime);
            }
            else if (!isFadingOut && FPAudioSource.time >= fadeOutStartTime)
            {
                isFadingOut = true;
                AudioFadeOutStartEvent.Invoke();
            }
        }

        void LoopAudio()
        {
            if(TimesToLoop > 0)
            {
                loopCount++;
                if (loopCount >= TimesToLoop)
                {
                    audioEnded = true;
                    FPAudioSource.Stop();
                    AudioFinishedEvent.Invoke();
                    LoopEvent.Invoke();
                    return;
                }
                
            }
            // Reset for loop
            FPAudioSource.Stop();
            //we don't want to fade in the loop again or do we?
            isFadingIn = FadeIn;
            isFadingOut = !FadeIn && FadeOut; // Only immediately fade out if not fading in
            ActivateRamp();
        }
        public void DebugTesting(string parameterTest)
        {
            Debug.Log($"Parameter test: {this.gameObject.name}: {parameterTest} at {Time.time}");
        }
    }
}
