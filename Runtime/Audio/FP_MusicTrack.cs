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
