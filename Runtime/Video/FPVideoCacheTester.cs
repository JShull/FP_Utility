namespace FuzzPhyte.Utility.Video
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    public class FPVideoCacheTester : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FPVideoCacheBootstrap bootstrap;

        [Header("Startup Tests")]
        [SerializeField] private bool initializeBootstrapOnStart = true;
        [SerializeField] private bool requestConfiguredVideoOnStart;
        [SerializeField] private bool preloadAllVideosOnStart;

        [Header("Request")]
        [SerializeField] private string videoId;

        [Header("Last Result")]
        [SerializeField] private bool lastSuccess;
        [SerializeField] private bool lastSourceWasCache;
        [SerializeField] private string lastResolvedLocalPath;
        [SerializeField] private string lastErrorMessage;

        private async void Start()
        {
            if (bootstrap == null)
            {
                Debug.LogWarning("[FPVideoCacheTester] No bootstrap assigned.");
                return;
            }

            if (initializeBootstrapOnStart)
            {
                await InitializeBootstrapAsync();
            }

            if (preloadAllVideosOnStart)
            {
                await PreloadAllVideosAsync();
            }

            if (requestConfiguredVideoOnStart)
            {
                await RequestConfiguredVideoAsync();
            }
        }

        [ContextMenu("Initialize Bootstrap")]
        public void InitializeBootstrapFromContextMenu()
        {
            _ = InitializeBootstrapAsync();
        }

        [ContextMenu("Preload All Videos")]
        public void PreloadAllVideosFromContextMenu()
        {
            _ = PreloadAllVideosAsync();
        }

        [ContextMenu("Request Configured Video")]
        public void RequestConfiguredVideoFromContextMenu()
        {
            _ = RequestConfiguredVideoAsync();
        }

        [ContextMenu("Check Cached Path")]
        public void CheckCachedPathFromContextMenu()
        {
            CheckCachedPath();
        }

        [ContextMenu("Clear Last Result")]
        public void ClearLastResult()
        {
            lastSuccess = false;
            lastSourceWasCache = false;
            lastResolvedLocalPath = string.Empty;
            lastErrorMessage = string.Empty;
        }

        private async Task InitializeBootstrapAsync()
        {
            if (bootstrap == null)
            {
                Debug.LogWarning("[FPVideoCacheTester] Cannot initialize because bootstrap is missing.");
                return;
            }

            bool initialized = await bootstrap.InitializeAsync();
            Debug.Log($"[FPVideoCacheTester] Bootstrap initialize result: {initialized}");
        }

        private async Task PreloadAllVideosAsync()
        {
            if (bootstrap == null)
            {
                Debug.LogWarning("[FPVideoCacheTester] Cannot preload because bootstrap is missing.");
                return;
            }

            IReadOnlyList<FPVideoRequestResult> results = await bootstrap.PreloadAllVideosAsync();
            Debug.Log($"[FPVideoCacheTester] Preload complete. Results: {results.Count}");
        }

        private async Task RequestConfiguredVideoAsync()
        {
            if (bootstrap == null)
            {
                Debug.LogWarning("[FPVideoCacheTester] Cannot request video because bootstrap is missing.");
                return;
            }

            if (string.IsNullOrWhiteSpace(videoId))
            {
                Debug.LogWarning("[FPVideoCacheTester] Video ID is empty.");
                return;
            }

            FPVideoRequestResult result = await bootstrap.RequestVideoAsync(videoId);
            ApplyLastResult(result);

            Debug.Log($"[FPVideoCacheTester] Request '{videoId}' success={result.Success} cache={result.SourceWasCache} path='{result.ResolvedLocalPath}' error='{result.ErrorMessage}'");
        }

        private void CheckCachedPath()
        {
            if (bootstrap == null)
            {
                Debug.LogWarning("[FPVideoCacheTester] Cannot check cache because bootstrap is missing.");
                return;
            }

            if (bootstrap.CacheManager == null)
            {
                Debug.LogWarning("[FPVideoCacheTester] Cache manager is not initialized yet.");
                return;
            }

            if (string.IsNullOrWhiteSpace(videoId))
            {
                Debug.LogWarning("[FPVideoCacheTester] Video ID is empty.");
                return;
            }

            bool found = bootstrap.CacheManager.TryGetCachedVideoPath(videoId, out string cachedPath, out FPVideoLocalMeta localMeta);
            if (!found)
            {
                Debug.Log($"[FPVideoCacheTester] No cached path found for '{videoId}'.");
                return;
            }

            lastSuccess = true;
            lastSourceWasCache = true;
            lastResolvedLocalPath = cachedPath;
            lastErrorMessage = string.Empty;

            string metaVersion = localMeta != null ? localMeta.version : "n/a";
            Debug.Log($"[FPVideoCacheTester] Cached path for '{videoId}': '{cachedPath}' (version {metaVersion})");
        }

        private void ApplyLastResult(FPVideoRequestResult result)
        {
            if (result == null)
            {
                lastSuccess = false;
                lastSourceWasCache = false;
                lastResolvedLocalPath = string.Empty;
                lastErrorMessage = "Result was null.";
                return;
            }

            lastSuccess = result.Success;
            lastSourceWasCache = result.SourceWasCache;
            lastResolvedLocalPath = result.ResolvedLocalPath;
            lastErrorMessage = result.ErrorMessage;
        }
    }
}
