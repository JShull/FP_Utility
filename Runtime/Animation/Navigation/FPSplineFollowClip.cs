namespace FuzzPhyte.Utility.Animation
{
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;

    [System.Serializable]
    public enum FPSplineLoopMode { None,Loop,PingPong}
    [System.Serializable]
    public class FPSplineFollowClip : PlayableAsset, ITimelineClipAsset
    {
        [Range(0f, 1f)] public float StartT = 0f;
        [Range(0f, 1f)] public float EndT = 1f;

        [Tooltip("Curve mapping normalized clip time (0..1) to travel progress (0..1).")]
        public AnimationCurve ProgressCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Tooltip("If true, when the playhead jumps (scrub/seek), progress is snapped to the new time.")]
        public bool ResyncOnTimeJump = true;

        [Tooltip("If true, preview in Timeline ignores pause/stop gates so pose matches playhead exactly.")]
        public bool IgnoreGatesInPreview = true;

        [Header("Looping / Completion")]
        public FPSplineLoopMode LoopMode = FPSplineLoopMode.None;
        [Tooltip("If true, keep advancing past the clip's out-point until reaching endT. Set Post-Extrapolation=Hold on this clip.")]
        public bool CompleteBeforeExit = false;
        [Tooltip("Fallback clip length in seconds (used when Timeline reports 'infinite' duration due to Post-Extrapolate). Leave 0 to auto-calc in editor.")]
        public double FiniteDurationHint = 0;


        public ClipCaps clipCaps => ClipCaps.Looping|ClipCaps.Extrapolation;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<FPSplineFollowBehaviour>.Create(graph);
            var bh = playable.GetBehaviour();
            bh.startT = StartT;
            bh.endT = EndT;
            bh.ProgressCurve = ProgressCurve ?? AnimationCurve.Linear(0, 0, 1, 1);
            bh.ResyncOnTimeJump = ResyncOnTimeJump;
            bh.IgnoreGatesInPreview = IgnoreGatesInPreview;
            bh.CompleteBeforeExit = CompleteBeforeExit;
            bh.LoopMode = LoopMode;
            bh.FiniteDurationHint = FiniteDurationHint;
            return playable;
        }
#if UNITY_EDITOR
        // when you adjust the clip.
        private void OnValidate()
        {
            // This is a best-effort editor nicety; safe to ignore if it can't resolve.
            var dir = UnityEditor.Selection.activeObject as PlayableDirector;
            if (dir == null) return;
            var asset = dir.playableAsset as TimelineAsset;
            if (asset == null) return;

            foreach (var track in asset.GetOutputTracks())
            {
                foreach (var clip in track.GetClips())
                {
                    if (clip.asset == this)
                    {
                        FiniteDurationHint = clip.duration; // seconds
                        return;
                    }
                }
            }
        }
#endif
    }
}
