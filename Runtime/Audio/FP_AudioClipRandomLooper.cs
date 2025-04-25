namespace FuzzPhyte.Utility.Audio
{
    using UnityEngine;
    using Unity.Mathematics;
    
    public class FP_AudioClipRandomLooper : MonoBehaviour
    {
        [Tooltip("List of clips to randomly choose from")]
        public AudioClip[] clips;
        public bool PlayOnStart;
        [SerializeField] protected AudioSource audioSource;
        [SerializeField] protected FP_RampAudio rampAudio;
        protected bool usingRampAudio;
        protected bool stopAction = false;
        [SerializeField] protected Unity.Mathematics.Random rng;
        [Tooltip("Optional seed for deterministic randomness. Set to 0 to use time-based seed.")]
        public uint seed = 0;
        protected virtual void Awake()
        {
            if (audioSource==null)
            {
                Debug.LogError($"Missing an audio source! - maybe one is on me?");
                audioSource = GetComponent<AudioSource>();
            }
            if (rampAudio != null)
            {
                usingRampAudio = true;
                //we control ramp audio on when to start
                rampAudio.PlayOnStart = false;
            }
            // Initialize the random number generator
            seed = (seed == 0) ? (uint)System.DateTime.Now.Ticks : seed;
            rng = new Unity.Mathematics.Random(seed);
        }

        protected virtual void Start()
        {
            if(PlayOnStart)
            {
                PlayNextClip();
            }
        }
        public virtual void StopAudioSystem()
        {
            stopAction = true;
            if (usingRampAudio)
            {
                rampAudio.AbruptlyStopAudio();
            }
            else
            {
                audioSource.Stop();
            }
        }

        protected virtual void PlayNextClip()
        {
            if (stopAction)
            {
                return;
            }
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning("No audio clips assigned.");
                return;
            }

            int index = rng.NextInt(clips.Length);
            AudioClip clipToPlay = clips[index];

            if (usingRampAudio)
            {
                audioSource.clip = clipToPlay;
                rampAudio.StartTime = 0f;
                //make sure we are setup on the ramp audio
                var effectiveLength = rampAudio.SetupRampAudioRemote() + 0.1f;
                
                //make sure that we are not using the loop feature of the Ramp Audio
                rampAudio.BreakLoop();
                rampAudio.ActivateRamp();

                FP_Timer.CCTimer?.StartTimer(effectiveLength, PlayNextClip);
            }
            else
            {
                audioSource.clip = clipToPlay;
                audioSource.Play();
                FP_Timer.CCTimer?.StartTimer(clipToPlay.length, PlayNextClip);
            }
        }
    }
}
