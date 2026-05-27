namespace FuzzPhyte.Utility.Audio
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName = "FPAudioCombineData", menuName = "FuzzPhyte/Audio/Audio Combine Data")]
    public class FPAudioCombineData : FP_Data
    {
        public int OutputFrequency = 44100;
        public int OutputChannels = 2;
        public bool NormalizeIfClipping = true;
        public float DefaultGapSeconds = 2f;
        public float NudgeSeconds = 0.25f;
        public string ExportFileName = "AudioCombined";
        public string ExportFolder = "Assets/_FPUtility/AudioExports";

        public bool HasExportStartBookend;
        public float ExportStartBookend;
        public bool HasExportEndBookend;
        public float ExportEndBookend;

        public List<FPAudioCombineClipData> Clips = new List<FPAudioCombineClipData>();
    }

    [Serializable]
    public class FPAudioCombineClipData
    {
        public AudioClip Clip;
        public float InTime;
        public float OutTime;
        public float TimelineStart;
        public Color TrackColor = Color.white;
        public float Gain = 1f;
        public bool FadeInEnabled;
        public float FadeInDuration;
        public float FadeInPower = 1f;
        public bool FadeOutEnabled;
        public float FadeOutDuration;
        public float FadeOutPower = 1f;
        public bool Locked;
        public bool Muted;
    }
}
