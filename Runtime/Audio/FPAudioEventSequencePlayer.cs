namespace FuzzPhyte.Utility.Audio
{    
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Run a sequence of FPAudioSequenceSteps
    /// </summary>
    public class FPAudioEventSequencePlayer : MonoBehaviour,IFPOnStartSetup
    {
        [SerializeField] protected AudioSource audioSource;
        [SerializeField] protected FPAudioSequenceStep[] sequenceSteps;
        protected bool onStart;
        protected bool isRunning = false;
        protected Coroutine _sequenceCoroutine;
        public delegate void FPAudioSequenceDelegate(FPAudioEventSequencePlayer player);
        public event FPAudioSequenceDelegate OnSequenceStart;
        public event FPAudioSequenceDelegate OnSequenceFinished;
        public bool SetupStart { get => onStart; set => onStart=value; }

        public void Start()
        {
            if (SetupStart)
            {
                PlaySequence();
            }  
        }

        /// <summary>
        /// Call to start the sequence
        /// </summary>
        public virtual void PlaySequence()
        {
            if (_sequenceCoroutine != null)
            {
                //kill it off
                StopCoroutine(_sequenceCoroutine);
            }
            _sequenceCoroutine=StartCoroutine(PlaySequenceCoroutine());
        }
        public virtual void StopSequence()
        {
            if (_sequenceCoroutine != null)
            {
                isRunning = false;
            }
        }
        /// <summary>
        /// Internal Coroutine for the sequence
        /// </summary>
        /// <returns></returns>
        protected IEnumerator PlaySequenceCoroutine()
        {
            isRunning = true;
            int index = 0;
            int total = sequenceSteps.Length;
            OnSequenceStart?.Invoke(this);
            do
            {
                var step = sequenceSteps[index];
                step.StepEvent?.Invoke();

                var data = step.StepData;

                if (data != null && data.TheClip != null && audioSource != null)
                {
                    audioSource.clip = data.TheClip;
                    audioSource.time = data.ClipStartTime;
                    audioSource.Play();
                }

                float delay = data?.ClipDelayAfter ?? 2f;
                yield return new WaitForSecondsRealtime(delay);

                index++;

            } while (isRunning && index < total);
            isRunning = false;
            _sequenceCoroutine = null;
            OnSequenceFinished?.Invoke(this);
        }
    }
}
