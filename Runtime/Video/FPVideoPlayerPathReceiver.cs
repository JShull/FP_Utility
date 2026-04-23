namespace FuzzPhyte.Utility.Video
{
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

        [Header("State")]
        [SerializeField] private string lastAssignedPath;

        [Header("Inspector Events")]
        [SerializeField] private UnityEvent onPathAssigned = new UnityEvent();
        [SerializeField] private UnityEvent onPrepared = new UnityEvent();
        [SerializeField] private UnityEvent onStartedPlaying = new UnityEvent();
        [SerializeField] private UnityEvent<string> onPathAssignmentFailed = new UnityEvent<string>();

        public string LastAssignedPath => lastAssignedPath;

        private void OnEnable()
        {
            if (targetVideoPlayer == null)
            {
                return;
            }

            targetVideoPlayer.prepareCompleted += HandlePrepareCompleted;
        }

        private void OnDisable()
        {
            if (targetVideoPlayer == null)
            {
                return;
            }

            targetVideoPlayer.prepareCompleted -= HandlePrepareCompleted;
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

            targetVideoPlayer.Play();
            onStartedPlaying?.Invoke();
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
            onPrepared?.Invoke();

            if (playOnPrepared)
            {
                source.Play();
                onStartedPlaying?.Invoke();
            }
        }

        private void ReportFailure(string message)
        {
            Debug.LogWarning($"[FPVideoPlayerPathReceiver] {message}");
            onPathAssignmentFailed?.Invoke(message);
        }
    }
}
