namespace FuzzPhyte.Utility.Video
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;

    public class FPVideoCacheBootstrap : MonoBehaviour
    {
        public event System.Action<FPVideoManifestItem> VideoDownloadStarted;
        public event System.Action<FPVideoRequestResult> VideoDownloadCompleted;
        public event System.Action<FPVideoRequestResult> VideoRequestCompleted;

        [SerializeField] private FPVideoRuntimeConfig runtimeConfig;
        [SerializeField] private bool initializeOnAwake = true;
        [Header("Inspector Events")]
        [SerializeField] private FPVideoManifestItemEvent onVideoDownloadStarted = new FPVideoManifestItemEvent();
        [SerializeField] private FPVideoRequestResultEvent onVideoDownloadCompleted = new FPVideoRequestResultEvent();
        [SerializeField] private FPVideoRequestResultEvent onVideoRequestCompleted = new FPVideoRequestResultEvent();

        private FPVideoCacheManager cacheManager;
        private CancellationTokenSource cancellationSource;

        public FPVideoCacheManager CacheManager => cacheManager;
        public FPVideoManifestItemEvent OnVideoDownloadStarted => onVideoDownloadStarted;
        public FPVideoRequestResultEvent OnVideoDownloadCompleted => onVideoDownloadCompleted;
        public FPVideoRequestResultEvent OnVideoRequestCompleted => onVideoRequestCompleted;

        private async void Awake()
        {
            if (!initializeOnAwake)
            {
                return;
            }

            await InitializeAsync();
        }

        private void OnDestroy()
        {
            UnsubscribeFromCacheManager(cacheManager);

            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
                cancellationSource.Dispose();
                cancellationSource = null;
            }
        }

        public async Task<bool> InitializeAsync()
        {
            if (runtimeConfig == null)
            {
                Debug.LogError("[FPVideoCacheBootstrap] Runtime config is not assigned.");
                return false;
            }

            cancellationSource?.Cancel();
            cancellationSource?.Dispose();
            cancellationSource = new CancellationTokenSource();

            UnsubscribeFromCacheManager(cacheManager);
            cacheManager = new FPVideoCacheManager(runtimeConfig);
            SubscribeToCacheManager(cacheManager);
            return await cacheManager.InitializeAsync(cancellationSource.Token);
        }

        public async Task<FPVideoRequestResult> RequestVideoAsync(string videoId)
        {
            if (cacheManager == null)
            {
                bool initialized = await InitializeAsync();
                if (!initialized && runtimeConfig != null && !runtimeConfig.PreloadAllVideosOnInitialize)
                {
                    Debug.LogWarning("[FPVideoCacheBootstrap] Initialization completed without a manifest. Request will still try cached fallback.");
                }
            }

            if (cacheManager == null)
            {
                return new FPVideoRequestResult
                {
                    Success = false,
                    VideoId = videoId,
                    ErrorMessage = "Video cache manager is not available because runtime config is missing."
                };
            }

            return await cacheManager.RequestVideoAsync(videoId, cancellationSource != null ? cancellationSource.Token : CancellationToken.None);
        }

        public async Task<IReadOnlyList<FPVideoRequestResult>> PreloadAllVideosAsync()
        {
            if (cacheManager == null)
            {
                bool initialized = await InitializeAsync();
                if (!initialized && cacheManager == null)
                {
                    return new List<FPVideoRequestResult>();
                }
            }

            return await cacheManager.PreloadAllVideosAsync(cancellationSource != null ? cancellationSource.Token : CancellationToken.None);
        }

        private void HandleVideoDownloadStarted(FPVideoManifestItem manifestItem)
        {
            VideoDownloadStarted?.Invoke(manifestItem);
            onVideoDownloadStarted?.Invoke(manifestItem);
        }

        private void HandleVideoDownloadCompleted(FPVideoRequestResult result)
        {
            VideoDownloadCompleted?.Invoke(result);
            onVideoDownloadCompleted?.Invoke(result);
        }

        private void HandleVideoRequestCompleted(FPVideoRequestResult result)
        {
            VideoRequestCompleted?.Invoke(result);
            onVideoRequestCompleted?.Invoke(result);
        }

        private void SubscribeToCacheManager(FPVideoCacheManager manager)
        {
            if (manager == null)
            {
                return;
            }

            manager.VideoDownloadStarted += HandleVideoDownloadStarted;
            manager.VideoDownloadCompleted += HandleVideoDownloadCompleted;
            manager.VideoRequestCompleted += HandleVideoRequestCompleted;
        }

        private void UnsubscribeFromCacheManager(FPVideoCacheManager manager)
        {
            if (manager == null)
            {
                return;
            }

            manager.VideoDownloadStarted -= HandleVideoDownloadStarted;
            manager.VideoDownloadCompleted -= HandleVideoDownloadCompleted;
            manager.VideoRequestCompleted -= HandleVideoRequestCompleted;
        }
    }
}
