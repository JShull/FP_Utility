namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public class FP_OpennessTickDriver : MonoBehaviour, IFPTickable
    {
        [Header("Tick")]
        [SerializeField] private int tickGroup = 100;     // choose a group id you configure as LateUpdate
        [SerializeField] private int tickPriority = 0;

        [Header("Wiring")]
        [SerializeField] private FP_OpennessStateTracker tracker;
        [SerializeField] private MonoBehaviour providerBehaviour; // IFP_OpennessProvider
        [SerializeField] private bool preferNormalized = true;

        private IFPOpennessProvider _provider;
        private bool _active;

        public int TickGroup => tickGroup;
        public int TickPriority => tickPriority;

        private void Awake()
        {
            _provider = providerBehaviour as IFPOpennessProvider;
        }

        // Hook to your UnityEvent: OnGrab
        public void BeginMonitoring()
        {
            if (_active) return;
            _active = true;

            tracker?.StartMotion();
            FP_TickSystem.CCTick?.Register(this);
        }

        // Hook to your UnityEvent: OnDrop
        public void EndMonitoring()
        {
            if (!_active) return;
            _active = false;

            FP_TickSystem.CCTick?.Unregister(this);
            tracker?.EndMotion();
        }

        public void Tick(float dt)
        {
            if (!_active || tracker == null || _provider == null) return;

            // Pull -> compute -> update tracker
            tracker.UpdateFromProvider(_provider, preferNormalized);
        }

        public void OnTickRegistered() { }
        public void OnTickUnregistered() { }
    }
}
