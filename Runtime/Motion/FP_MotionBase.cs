namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Events;
    using System.Collections.Generic;
    [System.Serializable]
    public struct FP_MotionEntry
    {
        public FP_MotionBase motion;
        public AnimationCurve overrideCurve;
        public float overrideDuration;
        [Tooltip("This can be used by motion needs")]
        public Vector3 overrideParameterData;
    }
    [System.Serializable]
    public class FP_MotionBlock
    {
        [Tooltip("All motions in this block will run in parallel")]
        public List<FP_MotionEntry> Motions = new List<FP_MotionEntry>();
    }
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

        public bool IsRunning => motionCoroutine != null && !isPaused;
        protected Coroutine motionCoroutine;

        protected virtual void Start()
        {
            InternalSetup();
        }
        [ContextMenu("Test: SetupMotion")]
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
        [ContextMenu("Test: StartMotion")]
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
        public virtual void OnEnable()
        {
            
        }
        public virtual void SetOverrideCurve(AnimationCurve curve,float duration, Vector4 motionData)
        {
            //motionCurve = curve != null ? curve : motionCurve;
            //lerpDuration = duration > 0f ? duration : lerpDuration;
        }
        protected virtual void InternalSetup()
        {
            if (PlayOnStart)
            {
                playOnSetup = true;
                SetupMotion();
            }
        }

        protected abstract IEnumerator MotionRoutine(); // Force derived classes to implement specific motion

        public virtual void OnDrawGizmos() { } // Optional override for visual aids
    }
}
