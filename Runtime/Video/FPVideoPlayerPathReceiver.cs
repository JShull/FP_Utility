// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Video
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Video;

    public class FPVideoPlayerPathReceiver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VideoPlayer targetVideoPlayer;

        [Header("Behavior")]
        [SerializeField] private bool prepareOnSet = true;
        [SerializeField] private bool playOnPrepared;
        [SerializeField] private bool stopBeforeAssigning = true;
        [SerializeField] private bool clearUrlWhenPathEmpty = true;
        [SerializeField] private bool allowFrameSkipping;
        [SerializeField, Min(0.1f)] private float playbackProgressCheckDelay = 1f;

        [Header("State")]
        [SerializeField] private string lastAssignedPath;

        [Header("Inspector Events")]
        [SerializeField] private UnityEvent onPathAssigned = new UnityEvent();
        [SerializeField] private UnityEvent onPrepared = new UnityEvent();
        [SerializeField] private UnityEvent onStartedPlaying = new UnityEvent();
        [SerializeField] private UnityEvent<string> onPathAssignmentFailed = new UnityEvent<string>();

        private Coroutine playbackProgressCheck;
        private int droppedFrameCount;

        public string LastAssignedPath => lastAssignedPath;

        private void OnEnable()
        {
            if (targetVideoPlayer == null)
            {
                return;
            }

            targetVideoPlayer.prepareCompleted += HandlePrepareCompleted;
            targetVideoPlayer.started += HandleStarted;
            targetVideoPlayer.errorReceived += HandlePlaybackError;
            targetVideoPlayer.loopPointReached += HandleLoopPointReached;
            targetVideoPlayer.frameDropped += HandleFrameDropped;
        }

        private void OnDisable()
        {
            if (targetVideoPlayer == null)
            {
                return;
            }

            targetVideoPlayer.prepareCompleted -= HandlePrepareCompleted;
            targetVideoPlayer.started -= HandleStarted;
            targetVideoPlayer.errorReceived -= HandlePlaybackError;
            targetVideoPlayer.loopPointReached -= HandleLoopPointReached;
            targetVideoPlayer.frameDropped -= HandleFrameDropped;

            if (playbackProgressCheck != null)
            {
                StopCoroutine(playbackProgressCheck);
                playbackProgressCheck = null;
            }
        }

        [ContextMenu("Prepare Current Video")]
        public void PrepareCurrentVideo()
        {
            if (targetVideoPlayer == null)
            {
                Debug.LogWarning("[FPVideoPlayerPathReceiver] No VideoPlayer assigned.");
                return;
            }

            targetVideoPlayer.Prepare();
        }

        [ContextMenu("Play Current Video")]
        public void PlayCurrentVideo()
        {
            if (targetVideoPlayer == null)
            {
                Debug.LogWarning("[FPVideoPlayerPathReceiver] No VideoPlayer assigned.");
                return;
            }

            playOnPrepared = true;

            if (!targetVideoPlayer.isPrepared || HasReachedEnd(targetVideoPlayer))
            {
                targetVideoPlayer.Stop();
                targetVideoPlayer.Prepare();
                Debug.Log($"[FPVideoPlayerPathReceiver] Play requested through preparation. {DescribePlaybackState(targetVideoPlayer)}");
                return;
            }

            targetVideoPlayer.Play();
            Debug.Log($"[FPVideoPlayerPathReceiver] Play requested. {DescribePlaybackState(targetVideoPlayer)}");
        }

        [ContextMenu("Stop Current Video")]
        public void StopCurrentVideo()
        {
            if (targetVideoPlayer == null)
            {
                Debug.LogWarning("[FPVideoPlayerPathReceiver] No VideoPlayer assigned.");
                return;
            }

            targetVideoPlayer.Stop();
        }

        [ContextMenu("Log Current Video State")]
        public void LogCurrentVideoState()
        {
            if (targetVideoPlayer == null)
            {
                Debug.LogWarning("[FPVideoPlayerPathReceiver] No VideoPlayer assigned.");
                return;
            }

            Debug.Log($"[FPVideoPlayerPathReceiver] {DescribePlaybackState(targetVideoPlayer)}");
        }

        [ContextMenu("Clear Assigned Path")]
        public void ClearAssignedPath()
        {
            lastAssignedPath = string.Empty;

            if (targetVideoPlayer == null)
            {
                return;
            }

            if (stopBeforeAssigning)
            {
                targetVideoPlayer.Stop();
            }

            targetVideoPlayer.url = string.Empty;
        }

        public void SetVideoPath(string localPath)
        {
            if (targetVideoPlayer == null)
            {
                ReportFailure("No VideoPlayer assigned.");
                return;
            }

            if (string.IsNullOrWhiteSpace(localPath))
            {
                lastAssignedPath = string.Empty;

                if (clearUrlWhenPathEmpty)
                {
                    if (stopBeforeAssigning)
                    {
                        targetVideoPlayer.Stop();
                    }

                    targetVideoPlayer.url = string.Empty;
                }

                ReportFailure("Resolved video path was empty.");
                return;
            }

            if (stopBeforeAssigning)
            {
                targetVideoPlayer.Stop();
            }

            lastAssignedPath = localPath;
            targetVideoPlayer.source = VideoSource.Url;
            targetVideoPlayer.url = localPath;

            Debug.Log($"[FPVideoPlayerPathReceiver] Assigned VideoPlayer path '{localPath}'.");
            onPathAssigned?.Invoke();

            if (prepareOnSet)
            {
                targetVideoPlayer.Prepare();
            }
        }

        public void SetVideoPathAndPlay(string localPath)
        {
            playOnPrepared = true;
            SetVideoPath(localPath);
        }

        private void HandlePrepareCompleted(VideoPlayer source)
        {
            if (source.canSetSkipOnDrop)
            {
                source.skipOnDrop = allowFrameSkipping;
            }

            Debug.Log($"[FPVideoPlayerPathReceiver] VideoPlayer prepared. {DescribePlaybackState(source)}");
            onPrepared?.Invoke();

            if (playOnPrepared)
            {
                source.Play();
            }
        }

        private void HandleStarted(VideoPlayer source)
        {
            droppedFrameCount = 0;
            Debug.Log($"[FPVideoPlayerPathReceiver] VideoPlayer started. {DescribePlaybackState(source)}");
            onStartedPlaying?.Invoke();

            if (playbackProgressCheck != null)
            {
                StopCoroutine(playbackProgressCheck);
            }

            playbackProgressCheck = StartCoroutine(CheckPlaybackProgress(source));
        }

        private void HandlePlaybackError(VideoPlayer source, string message)
        {
            string error = $"VideoPlayer error for '{lastAssignedPath}': {message}";
            Debug.LogError($"[FPVideoPlayerPathReceiver] {error}");
            onPathAssignmentFailed?.Invoke(error);
        }

        private void HandleLoopPointReached(VideoPlayer source)
        {
            Debug.Log($"[FPVideoPlayerPathReceiver] VideoPlayer reached the end. {DescribePlaybackState(source)}");
        }

        private void HandleFrameDropped(VideoPlayer source)
        {
            droppedFrameCount++;
            if (droppedFrameCount == 1 || droppedFrameCount % 30 == 0)
            {
                Debug.LogWarning($"[FPVideoPlayerPathReceiver] VideoPlayer dropped {droppedFrameCount} frame(s). {DescribePlaybackState(source)}");
            }
        }

        private IEnumerator CheckPlaybackProgress(VideoPlayer source)
        {
            long startingFrame = source.frame;
            double startingTime = source.time;
            yield return new WaitForSecondsRealtime(playbackProgressCheckDelay);
            playbackProgressCheck = null;

            if (source == null)
            {
                yield break;
            }

            bool frameAdvanced = source.frame > startingFrame;
            bool timeAdvanced = source.time > startingTime + 0.05d;
            string state = DescribePlaybackState(source);

            if (frameAdvanced || timeAdvanced)
            {
                Debug.Log($"[FPVideoPlayerPathReceiver] Playback progression confirmed. {state}");
            }
            else
            {
                Debug.LogWarning($"[FPVideoPlayerPathReceiver] VideoPlayer started but frame/time did not advance. {state}");
            }
        }

        private static bool HasReachedEnd(VideoPlayer source)
        {
            return HasReachedEnd(
                source.isLooping,
                source.frame,
                source.frameCount,
                source.time,
                source.length);
        }

        private static bool HasReachedEnd(
            bool isLooping,
            long frame,
            ulong frameCount,
            double time,
            double length)
        {
            bool lastFrameReached =
                frameCount > 0 && frame >= (long)frameCount - 1;
            bool endTimeReached =
                length > 0d && time >= length - 0.05d;
            return !isLooping && (lastFrameReached || endTimeReached);
        }

        private string DescribePlaybackState(VideoPlayer source)
        {
            string target = source.targetTexture == null
                ? "none"
                : $"{source.targetTexture.name}({source.targetTexture.width}x{source.targetTexture.height})";
            return
                $"path='{source.url}' prepared={source.isPrepared} playing={source.isPlaying} " +
                $"paused={source.isPaused} frame={source.frame}/{source.frameCount} " +
                $"time={source.time:F3}/{source.length:F3} speed={source.playbackSpeed:F2} " +
                $"skipOnDrop={source.skipOnDrop} dropped={droppedFrameCount} " +
                $"renderMode={source.renderMode} target={target}";
        }

        private void ReportFailure(string message)
        {
            Debug.LogWarning($"[FPVideoPlayerPathReceiver] {message}");
            onPathAssignmentFailed?.Invoke(message);
        }
    }
}
