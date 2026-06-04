// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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
