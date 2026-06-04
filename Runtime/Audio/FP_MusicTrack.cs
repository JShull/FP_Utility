// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Audio
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "Music Track", menuName = "FuzzPhyte/Audio/Music Track", order = 6)]
    public class FP_MusicTrack : FP_Data
    {
        public string Name;
        [TextArea(2, 4)]
        public string Description;
        public AudioClip Clip;
        [Tooltip("Filter against this later if needed")]
        public EmotionalState MusicEmotionalState;
    }
}
