namespace FuzzPhyte.Utility.Audio
{
    using UnityEngine;

    [DisallowMultipleComponent]
    public class FP_AudioRotationBased : MonoBehaviour
    {
        [Header("Door / Angle")]
        [Tooltip("Transform whose local rotation represents the door. Usually the door root or hinge node.")]
        public Transform RootRotation;
        [Tooltip("Local axis the door rotates around (Y for most hinges).")]
        public Vector3 localHingeAxis = Vector3.up;
        [Tooltip("Reference rotation considered 'closed'. Leave empty to capture at Awake().")]
        [SerializeField] protected Quaternion closedLocalRotation;
        
        [Space]
        [Header("Grain Triggering")]
        [Tooltip("Play a short creak every time the door accumulates this many degrees of motion.")]
        [Min(0.5f)] public float AngleStepDegrees = 3f;
        [Tooltip("Minimum angular speed (deg/sec) required to trigger audio.")]
        [Min(0f)] public float MinSpeedDegPerSec = 5f;
        [Tooltip("Optional: minimum time between grains (sec). Set 0 to disable.")]
        [Min(0f)] public float MinInterval = 0.025f;
        
        [Space]
        [Header("Clips & Mapping")]
        [Tooltip("Creak/hinge clips sorted from lightest to heaviest sound.")]
        public AudioClip[] AudioClips;
        [Tooltip("Curve input: normalized speed (0-1). Output: 0-1 used to pick clip index.")]
        public AnimationCurve RotationSpeedToClipCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Max speed used for normalization (deg/sec). Tune to your doors.")]
        [Min(1f)] public float MaxRotationSpeedDegPerSec = 300f;
        
        [Space]
        [Header("Volume / Pitch by Speed")]
        [Tooltip("Map normalized speed to volume (0-1).")]
        public AnimationCurve RotationSpeedToVolume = AnimationCurve.Linear(0, 0.15f, 1, 1f);
        [Tooltip("Map normalized speed to pitch (e.g., 0.8–1.3).")]
        public AnimationCurve RotationSpeedToPitch = AnimationCurve.Linear(0, 0.9f, 1, 1.2f);

        [Header("Smoothing")]
        [Tooltip("Exponential smoothing factor for angular speed (higher = snappier).")]
        [Range(0.0f, 1.0f)] public float RotationSpeedLerp = 0.3f;

        [Header("AudioSource")]
        [Tooltip("If null, will auto-grab from this GameObject.")]
        public AudioSource RotationAudioSource;

        // state variable to reduce Update usage
        [SerializeField]
        protected bool _doorActive;
        
        protected float _lastAngle;
        protected float _angleAccumulator;
        protected float _smoothedSpeed;
        protected float _lastGrainTime;

        #region Public Accessors for Door Active or Not
        public void RotationActive()
        {
            _doorActive = true;
        }
        public void RotationInactive()
        {
            _doorActive = false;
        }
        #endregion
        protected virtual void Awake()
        {
            if (!RootRotation) RootRotation = transform;
            if (closedLocalRotation == default) closedLocalRotation = RootRotation.localRotation;
            if (!RotationAudioSource) RotationAudioSource = GetComponent<AudioSource>();
            if(RotationAudioSource == null)
            {
                Debug.LogWarning($"[{nameof(FP_AudioRotationBased)}] No AudioSource found on {name}. Please assign one.");
            }
            if(AudioClips.Length == 0)
            {
                Debug.LogWarning($"[{nameof(FP_AudioRotationBased)}] No AudioClips assigned on {name}. Please assign some.");
            }
            _lastAngle = GetSignedDoorAngle();
        }

        protected virtual void Update()
        {
            if (!_doorActive)
            {
                return;
            }
            float dt = Mathf.Max(Time.deltaTime, 1e-6f);

            // Current signed angle around hinge axis
            float angle = GetSignedDoorAngle();

            // Delta considering wrap (use Mathf.DeltaAngle to be robust)
            float dAngle = Mathf.DeltaAngle(_lastAngle, angle);
            _lastAngle = angle;

            float speedDegPerSec = Mathf.Abs(dAngle) / dt;

            // Smooth speed to avoid jittery mapping
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speedDegPerSec, 1f - Mathf.Pow(1f - RotationSpeedLerp, dt * 60f));

            // Accumulate absolute rotation to decide when to fire a grain
            _angleAccumulator += Mathf.Abs(dAngle);
            //Debug.Log($"angle = {angle}, _angleAccumulator: {_angleAccumulator}, _smoothedSpeed: {_smoothedSpeed}");
            if (_angleAccumulator >= AngleStepDegrees && _smoothedSpeed >= MinSpeedDegPerSec)
            {
                // Optional minimum interval between grains
                if (MinInterval <= 0f || (Time.time - _lastGrainTime) >= MinInterval)
                {
                    PlayGrain(_smoothedSpeed);
                    _lastGrainTime = Time.time;
                    _angleAccumulator %= AngleStepDegrees; // keep remainder to preserve cadence
                }
            }
        }

        protected virtual void PlayGrain(float speedDegPerSec)
        {
            if (AudioClips == null || AudioClips.Length == 0 || RotationAudioSource == null) return;

            // Normalize speed and map to selection index (borrowed pattern from RandomAudioPlayer).
            float norm = Mathf.Clamp01(speedDegPerSec / Mathf.Max(1f, MaxRotationSpeedDegPerSec));
            float curve01 = Mathf.Clamp01(RotationSpeedToClipCurve.Evaluate(norm));
            int idx = Mathf.Clamp(Mathf.FloorToInt(curve01 * AudioClips.Length), 0, AudioClips.Length - 1); // like RandomAudioPlayer
            var clip = AudioClips[idx];

            // Volume / Pitch scaling from speed
            float vol = Mathf.Clamp01(RotationSpeedToVolume.Evaluate(norm));
            float pitch = Mathf.Max(0.01f, RotationSpeedToPitch.Evaluate(norm));

            // Brief randomization to avoid machine-gun effect
            float tinyVar = 1f + Random.Range(-0.03f, 0.03f);
            RotationAudioSource.pitch = pitch * tinyVar;
            RotationAudioSource.PlayOneShot(clip, vol);
        }

        /// <summary>
        /// Returns signed angle in degrees around the local hinge axis,
        /// relative to the closedLocalRotation.
        /// </summary>
        private float GetSignedDoorAngle()
        {
            // Determine a “reference direction” vector in hinge-local space.
            // e.g., pick a direction at closed position (e.g., door’s forward in closed-locally).
            Vector3 refDir = closedLocalRotation * Vector3.forward;
            // Current door direction vector in local space (projected into world or local consistently)
            Vector3 currentDir = RootRotation.localRotation * Vector3.forward;

            // Use the hinge axis (in local space) as the axis of rotation
            Vector3 axis = RootRotation.localRotation * localHingeAxis.normalized;

            float signedAngle = Vector3.SignedAngle(refDir, currentDir, axis);
            return signedAngle;
        }
    }
}
