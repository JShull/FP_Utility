namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.Events;
    [System.Serializable]
    public struct FPFeedback
    {
        [TextArea(2, 4)]
        public string Description;      // Optional text label
        [Tooltip("Optional audio feedback")]
        public AudioClip FeedbackAClip;     // Optional audio feedback
        public UnityEvent VisualFeedback; // Optional UnityEvent for custom actions
        public UnityEvent AdditionalFeedback; // Optional for animations or tools
    }
}
