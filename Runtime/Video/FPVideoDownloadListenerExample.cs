namespace FuzzPhyte.Utility.Video
{
    using UnityEngine;

    public class FPVideoDownloadListenerExample : MonoBehaviour
    {
        [SerializeField] private FPVideoCacheBootstrap bootstrap;

        private void OnEnable()
        {
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.VideoDownloadStarted += OnVideoDownloadStarted;
            bootstrap.VideoDownloadCompleted += OnVideoDownloadCompleted;
            bootstrap.VideoRequestCompleted += OnVideoRequestCompleted;
        }

        private void OnDisable()
        {
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.VideoDownloadStarted -= OnVideoDownloadStarted;
            bootstrap.VideoDownloadCompleted -= OnVideoDownloadCompleted;
            bootstrap.VideoRequestCompleted -= OnVideoRequestCompleted;
        }

        private void OnVideoDownloadStarted(FPVideoManifestItem item)
        {
            string id = item != null ? item.id : "unknown";
            Debug.Log($"[FPVideoDownloadListenerExample] Download started for '{id}'.");
        }

        private void OnVideoDownloadCompleted(FPVideoRequestResult result)
        {
            if (result == null)
            {
                Debug.LogWarning("[FPVideoDownloadListenerExample] Download completed event returned null result.");
                return;
            }

            Debug.Log($"[FPVideoDownloadListenerExample] Download completed for '{result.VideoId}'. Success={result.Success} Path='{result.ResolvedLocalPath}' Error='{result.ErrorMessage}'");
        }

        private void OnVideoRequestCompleted(FPVideoRequestResult result)
        {
            if (result == null)
            {
                Debug.LogWarning("[FPVideoDownloadListenerExample] Request completed event returned null result.");
                return;
            }

            Debug.Log($"[FPVideoDownloadListenerExample] Request completed for '{result.VideoId}'. Success={result.Success} Cache={result.SourceWasCache} Downloaded={result.DownloadWasPerformed}");
        }
    }
}
