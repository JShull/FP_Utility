namespace FuzzPhyte.Utility.Animation
{
    using UnityEngine;
    using UnityEngine.Playables;
    public class FPSplineFollowBehaviour : PlayableBehaviour
    {
        public float startT, endT;
        public AnimationCurve ProgressCurve;
        public bool ResyncOnTimeJump;
        public bool IgnoreGatesInPreview;
        public bool CompleteBeforeExit = false;
        public FPSplineLoopMode LoopMode = FPSplineLoopMode.None;
        public double FiniteDurationHint = 0;

        protected FPSplineFollower follower;
        protected float progress01;
        protected double lastPlayableTime = double.NaN;
        //private double _finiteClipDuration = double.NaN;

        protected float ApplyLoop(float p)
        {
            switch (LoopMode)
            {
                case FPSplineLoopMode.Loop: return Mathf.Repeat(p, 1f);
                case FPSplineLoopMode.PingPong: return Mathf.PingPong(p, 1f);
                default: return Mathf.Clamp01(p);
            }
        }
        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            // When the clip begins playing, initialize progress to the current normalized time,
            // so late joins or blends start at the right point.
            double dur = playable.GetDuration();
            double t = playable.GetTime();
            progress01 = (dur > 0.0) ? Mathf.Clamp01((float)(t / dur)) : 0f;
            lastPlayableTime = t;
        }
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (playerData is FPSplineFollower f) follower = f;
            if (follower == null) return;

            // 1) Get duration; treat sentinel/huge values as "infinite"
            double rawDur = playable.GetDuration();
            bool durLooksInfinite = double.IsInfinity(rawDur) || rawDur >= 1_000_000.0 || rawDur <= 0.0;

            // 2) Choose a finite duration
            double dur = durLooksInfinite
                ? (FiniteDurationHint > 0.0 ? FiniteDurationHint : 0.0)
                : rawDur;

            if (dur <= 0.0) return; // still nothing to do

            double now = playable.GetTime();

            // ---- robust preview detection (as we implemented earlier) ----
            bool appPlaying = Application.isPlaying;
            bool graphPlaying = playable.GetGraph().IsPlaying();
            bool isPreview = !appPlaying || !graphPlaying || info.timeHeld || info.deltaTime <= 0f
                             || info.evaluationType == FrameData.EvaluationType.Evaluate;

            if (isPreview)
            {
                float baseProgress = (float)(now / dur);               // not clamped
                float previewProgress =
                    (LoopMode == FPSplineLoopMode.None) ? Mathf.Clamp01(baseProgress)
                                                         : ApplyLoop(baseProgress);

                if (CompleteBeforeExit && LoopMode == FPSplineLoopMode.None && now > dur)
                {
                    float extra = (float)((now - dur) / dur) * Mathf.Max(0f, follower.SpeedMultiplier);
                    previewProgress = Mathf.Min(1f, previewProgress + extra);
                }

                float curvedPrev = (ProgressCurve != null) ? ProgressCurve.Evaluate(previewProgress) : previewProgress;
                float tPreview = Mathf.Lerp(startT, endT, Mathf.Clamp01(curvedPrev));

                if (IgnoreGatesInPreview)
                {
                    follower.ForcePreviewIgnoreGates = true;
                    follower.WarpToNormalizedT(tPreview);
                    follower.ForcePreviewIgnoreGates = false;
                }
                else
                {
                    follower.SetNormalizedT(tPreview);
                }

                progress01 = previewProgress;
                lastPlayableTime = now;
                return;
            }



            // ------------------------------------------------------------
            // Playing: accumulate progress with deltaTime, respect pause/stop,
            // and optionally resync on big time jumps (skips).
            // ------------------------------------------------------------

            if (double.IsNaN(lastPlayableTime))
            {
                lastPlayableTime = now;
                progress01 = Mathf.Clamp01((float)(now / dur));
            }
            else
            {
                double dtPlayable = now - lastPlayableTime;
                lastPlayableTime = now;

                bool timeJumped = dtPlayable < -0.0001 || dtPlayable > 1.0; // heuristic: negative or big forward jump
                if (ResyncOnTimeJump && timeJumped&&!CompleteBeforeExit)
                {
                    // Snap progress to where the playhead currently is
                    progress01 = Mathf.Clamp01((float)(now / dur));
                }
                else
                {
                    // Advance progress only when not paused/stopped.
                    if (!follower.IsPaused && !follower.IsStopped)
                    {
                        // NEW: fallback to Time.deltaTime if Timeline holds time (delta==0)
                        double effectiveDt = info.deltaTime > 0f ? info.deltaTime : (double)Time.deltaTime;

                        float step = (float)(effectiveDt / dur) * Mathf.Max(0f, follower.SpeedMultiplier);

                        // Keep advancing after out-point when CompleteBeforeExit is on
                        if (CompleteBeforeExit && LoopMode == FPSplineLoopMode.None && now >= dur)
                        {
                            progress01 += step;  // unbounded; we'll clamp/map below
                        }
                        else
                        {
                            progress01 += step;
                        }
                    }
                }
            }
            // Apply loop mode - even if we are none the ApplyLoop function will return default
            float looped = ApplyLoop(progress01);

            // Map progress through the curve, then to [startT,endT]
            float curved = (ProgressCurve != null) ? ProgressCurve.Evaluate(looped):looped;
            float tParam = Mathf.Lerp(startT, endT, Mathf.Clamp01(curved));

            // This call itself respects pause/stop (it early-outs), which is fine:
            follower.SetNormalizedT(tParam);
        }
    }
}
