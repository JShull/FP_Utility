namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public class FP_OpennessInteractionBridge : MonoBehaviour
    {
        [SerializeField] private FP_OpennessStateTracker tracker;
        [SerializeField] private MonoBehaviour providerBehaviour; // must implement IFP_OpennessProvider
        [SerializeField] private bool preferNormalized = true;

        private IFPOpennessProvider _provider;

        private void Awake()
        {
            _provider = providerBehaviour as IFPOpennessProvider;
        }

        // Hook to: OnGrab / OnSelectEnter
        public void OnInteractionStart()
        {
            tracker?.StartMotion();
        }

        // Hook to: your “while held” event (drag, value changed, joint moved, etc.)
        public void OnInteractionTick()
        {
            if (tracker == null || _provider == null) return;
            tracker.UpdateFromProvider(_provider, preferNormalized);
        }

        // Hook to: OnDrop / OnSelectExit
        public void OnInteractionEnd()
        {
            tracker?.EndMotion();
        }
    }
}
