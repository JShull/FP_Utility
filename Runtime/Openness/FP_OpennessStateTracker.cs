namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;
    using UnityEngine.Events;

    public class FP_OpennessStateTracker : MonoBehaviour
    {
        [Header("Raw Range (only used when calling UpdateRaw)")]
        [SerializeField] private float rawClosed = 0f;
        [SerializeField] private float rawFullyOpen = 90f;

        [Header("Thresholds (Normalized 0..1)")]
        [Range(0f, 1f)]
        [SerializeField] private float openThreshold = 0.05f;

        [Range(0f, 1f)]
        [SerializeField] private float partialThreshold = 0.50f;

        [Range(0f, 1f)]
        [SerializeField] private float fullyOpenThreshold = 0.95f;

        [Header("Hysteresis (prevents jitter at boundaries)")]
        [Tooltip("When closing, we only reset back to Closed when <= (openThreshold - hysteresis).")]
        [Range(0f, 0.2f)]
        [SerializeField] private float closeHysteresis = 0.02f;

        [Header("One-shot Events (per OPENING cycle; reset when item returns to Closed)")]
        public UnityEvent OnCrossOpenUp;
        public UnityEvent OnCrossPartialUp;
        public UnityEvent OnCrossFullyOpenUp;

        [Header("Optional Directional Events (can fire multiple times, not one-shot)")]
        public UnityEvent OnCrossPartialDown;
        public UnityEvent OnCrossOpenDown;       // crossing below partial -> open
        public UnityEvent OnReturnClosed;        // when we hit Closed (and reset cycle)

        [Header("State Changed")]
        public UnityEvent OnStateChangedUnity;   // use CurrentState/Direction getters if needed

        public event Action<OpennessState, OpennessState> StateChanged;
        public event Action<OpennessDirection> DirectionChanged;

        public bool SessionActive => _sessionActive;
        public bool IsRunning => _sessionActive && !_paused; // similar idea to FP_MotionBase.IsRunning :contentReference[oaicite:3]{index=3}

        public OpennessState CurrentState => _state;
        public OpennessDirection CurrentDirection => _direction;
        public float CurrentNormalized => _currentNormalized;

        private bool _sessionActive;
        private bool _paused;

        private float _currentNormalized;
        private float _lastNormalized;

        private OpennessState _state = OpennessState.Closed;
        private OpennessDirection _direction = OpennessDirection.NA;

        // one-shots per opening cycle
        private bool _firedOpenUp;
        private bool _firedPartialUp;
        private bool _firedFullyOpenUp;

        #region IFPMotionController lifecycle wrappers
        public void SetupMotion()
        {
            // Make sure we start clean.
            ResetMotion();
        }

        public void StartMotion()
        {
            // "Begin session"
            _sessionActive = true;
            _paused = false;
            _lastNormalized = _currentNormalized;
            UpdateDirection(_currentNormalized); // sets None
        }

        public void PauseMotion() => _paused = true;

        public void ResumeMotion()
        {
            if (_sessionActive) _paused = false;
        }

        public void ResetMotion()
        {
            _sessionActive = false;
            _paused = true;

            _currentNormalized = 0f;
            _lastNormalized = 0f;

            SetState(OpennessState.Closed);
            SetDirection(OpennessDirection.NA);

            ResetOneShots();
        }

        public void EndMotion()
        {
            // "End session" but do NOT force reset of one-shots/state unless you want it.
            _sessionActive = false;
            _paused = true;
            SetDirection(OpennessDirection.NA);
        }

        public void OnDrawGizmos() { }
        #endregion

        /// <summary>
        /// Feed a RAW openness measure (door angle, drawer distance, etc).
        /// </summary>
        public void UpdateRaw(float rawValue)
        {
            if (!IsRunning) return;
            UpdateNormalized(NormalizeRaw(rawValue));
        }

        /// <summary>
        /// Feed a NORMALIZED openness in [0..1].
        /// Call this from your grab/drag callbacks while interacting.
        /// </summary>
        public void UpdateNormalized(float normalizedValue)
        {
            if (!IsRunning) return;

            _currentNormalized = Mathf.Clamp01(normalizedValue);

            // direction first (opening vs closing)
            UpdateDirection(_currentNormalized);

            // Evaluate state with hysteresis-aware "Closed" return.
            var nextState = EvaluateState(_currentNormalized);

            // Handle one-shot cycle reset when we truly return to Closed
            if (nextState == OpennessState.Closed && _state != OpennessState.Closed)
            {
                SetState(OpennessState.Closed);
                ResetOneShots();
                OnReturnClosed?.Invoke();
                OnStateChangedUnity?.Invoke();
                _lastNormalized = _currentNormalized;
                return;
            }

            // Fire threshold crossings (upwards one-shot per cycle)
            if (!_firedOpenUp && _currentNormalized >= openThreshold)
            {
                _firedOpenUp = true;
                OnCrossOpenUp?.Invoke();
            }
            if (!_firedPartialUp && _currentNormalized >= partialThreshold)
            {
                _firedPartialUp = true;
                OnCrossPartialUp?.Invoke();
            }
            if (!_firedFullyOpenUp && _currentNormalized >= fullyOpenThreshold)
            {
                _firedFullyOpenUp = true;
                OnCrossFullyOpenUp?.Invoke();
            }

            // Optional: fire downward crossings (not one-shot)
            // These are useful if you want logic while closing.
            if (_direction == OpennessDirection.Closing)
            {
                // Cross below fully-open -> partial (no explicit event here unless you want one)
                if (_lastNormalized >= partialThreshold && _currentNormalized < partialThreshold)
                {
                    OnCrossPartialDown?.Invoke();
                }
                if (_lastNormalized >= openThreshold && _currentNormalized < openThreshold)
                {
                    OnCrossOpenDown?.Invoke();
                }
            }

            // State change notification
            if (nextState != _state)
            {
                SetState(nextState);
                OnStateChangedUnity?.Invoke();
            }

            _lastNormalized = _currentNormalized;
        }

        public void UpdateFromProvider(IFPOpennessProvider provider, bool preferNormalized = true)
        {
            if (provider == null) return;
            if (!IsRunning) return;

            if (preferNormalized)
                UpdateNormalized(provider.GetOpennessNormalized());
            else
                UpdateRaw(provider.GetOpennessRaw());
        }
        private float NormalizeRaw(float raw)
        {
            if (Mathf.Approximately(rawClosed, rawFullyOpen)) return 0f;
            return Mathf.InverseLerp(rawClosed, rawFullyOpen, raw);
        }

        private OpennessState EvaluateState(float norm)
        {
            // Hysteresis for returning to Closed so we don't flap at openThreshold.
            float closeResetPoint = Mathf.Clamp01(openThreshold - closeHysteresis);

            if (norm <= closeResetPoint) return OpennessState.Closed;
            if (norm >= fullyOpenThreshold) return OpennessState.FullyOpen;
            if (norm >= partialThreshold) return OpennessState.Partial;
            return OpennessState.Open;
        }

        private void ResetOneShots()
        {
            _firedOpenUp = false;
            _firedPartialUp = false;
            _firedFullyOpenUp = false;
        }

        private void SetState(OpennessState next)
        {
            var prev = _state;
            _state = next;
            if (prev != next) StateChanged?.Invoke(prev, next);
        }

        private void UpdateDirection(float current)
        {
            // Use a tiny epsilon to avoid jitter.
            const float eps = 0.0005f;
            OpennessDirection nextDir = _direction;

            float delta = current - _lastNormalized;
            if (Mathf.Abs(delta) <= eps)
                nextDir = OpennessDirection.NA;
            else
                nextDir = delta > 0f ? OpennessDirection.Opening : OpennessDirection.Closing;

            if (nextDir != _direction)
                SetDirection(nextDir);
        }

        private void SetDirection(OpennessDirection next)
        {
            _direction = next;
            DirectionChanged?.Invoke(next);
        }
    }
}
