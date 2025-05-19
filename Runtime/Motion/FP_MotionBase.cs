namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Events;

    public abstract class FP_MotionBase : MonoBehaviour, IFPMotionController
    {
        [SerializeField]
        protected Transform targetObject;  // The target transform to affect (can be overridden)
        public Transform TargetObject { get=>targetObject; set=>targetObject = value; }
        [SerializeField]
        protected UnityEvent onStartMotion;
        public UnityEvent OnStartMotion { get => onStartMotion; }
        [SerializeField]
        protected UnityEvent onEndMotion;
        public UnityEvent OnEndMotion { get => onEndMotion; }
        public delegate void MotionEventHandler();
        public event MotionEventHandler OnMotionEnded;
        public event MotionEventHandler OnMotionStarted;
        [Header("Base Motion Settings")]
        [SerializeField]
        protected float lerpDuration = 3.0f;
        [SerializeField]
        protected bool loop = false;
        [SerializeField]
        protected bool PlayOnStart = false;
        [Space]
        [SerializeField]
        protected bool playOnSetup = true;
        protected bool isPaused = true;
        
        protected Coroutine motionCoroutine;

        protected virtual void Start()
        { 
            if (PlayOnStart)
            {
                playOnSetup = true;
                SetupMotion();
            }
        }

        public virtual void SetupMotion()
        {
            if (targetObject == null)
            {
                targetObject = transform;
            }
            ResetMotion();
            if(playOnSetup)
            {
                StartMotion();
            }
        }

        public virtual void StartMotion()
        {
            if (motionCoroutine != null)
            {
                StopCoroutine(motionCoroutine);
            }
            isPaused = false;
            OnMotionStarted?.Invoke();
            onStartMotion?.Invoke();
            motionCoroutine = StartCoroutine(MotionRoutine());
        }

        public virtual void PauseMotion()
        {
            isPaused = true;
        }

        public virtual void ResumeMotion()
        {
            if (isPaused && motionCoroutine != null)
            {
                isPaused = false;
            }
        }

        public virtual void ResetMotion()
        {
            if (motionCoroutine != null)
            {
                StopCoroutine(motionCoroutine);
            }
            isPaused = true;
        }

        public virtual void EndMotion()
        {
            if (motionCoroutine != null)
            {
                StopCoroutine(motionCoroutine);
            }
            isPaused = true;
            OnMotionEnded?.Invoke();
            onEndMotion?.Invoke();
        }
        public virtual void OnDisable()
        {
            if (motionCoroutine != null)
            {
                StopCoroutine(motionCoroutine);
            }
        }

        protected abstract IEnumerator MotionRoutine(); // Force derived classes to implement specific motion

        public virtual void OnDrawGizmos() { } // Optional override for visual aids
    }
}
