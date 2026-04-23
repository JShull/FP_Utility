namespace FuzzPhyte.Utility.Video
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Networking;

    public class FPVideoCacheManager
    {
        public event Action<FPVideoManifestItem> VideoDownloadStarted;
        public event Action<FPVideoRequestResult> VideoDownloadCompleted;
        public event Action<FPVideoRequestResult> VideoRequestCompleted;

        private readonly FPVideoRuntimeConfig config;
        private readonly Dictionary<string, FPVideoManifestItem> manifestLookup = new Dictionary<string, FPVideoManifestItem>();

        private FPVideoManifestCollection manifestCollection;
        private bool manifestLoaded;
        private bool manifestFetchAttempted;

        public FPVideoCacheManager(FPVideoRuntimeConfig runtimeConfig)
        {
            config = runtimeConfig;
            EnsureCacheDirectoriesExist();
        }

        public string CacheRootPath => Path.Combine(Application.persistentDataPath, config != null ? config.CacheRootFolderName : "VideoCache");
        public string VideosFolderPath => Path.Combine(CacheRootPath, config != null ? config.VideosFolderName : "videos");
        public string MetadataFolderPath => Path.Combine(CacheRootPath, config != null ? config.MetadataFolderName : "meta");

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            EnsureCacheDirectoriesExist();

            bool manifestSuccess = await LoadManifestAsync(cancellationToken);
            if (manifestSuccess && config != null && config.PreloadAllVideosOnInitialize)
            {
                await PreloadAllVideosAsync(cancellationToken);
            }

            return manifestSuccess;
        }

        public async Task<IReadOnlyList<FPVideoRequestResult>> PreloadAllVideosAsync(CancellationToken cancellationToken = default)
        {
            List<FPVideoRequestResult> results = new List<FPVideoRequestResult>();

            if (!manifestLoaded && !await LoadManifestAsync(cancellationToken))
            {
                Debug.LogWarning("[FPVideoCacheManager] Manifest unavailable during preload. Cached fallback remains available for individual requests.");
                return results;
            }

            if (manifestCollection?.videos == null)
            {
                return results;
            }

            for (int i = 0; i < manifestCollection.videos.Length; i++)
            {
                FPVideoManifestItem item = manifestCollection.videos[i];
                if (item == null || string.IsNullOrWhiteSpace(item.id))
                {
                    continue;
                }

                results.Add(await RequestVideoAsync(item.id, cancellationToken));
            }

            return results;
        }

        public async Task<FPVideoRequestResult> RequestVideoAsync(string videoId, CancellationToken cancellationToken = default)
        {
            EnsureCacheDirectoriesExist();

            if (string.IsNullOrWhiteSpace(videoId))
            {
                FPVideoRequestResult invalidRequest = CreateFailure(videoId, "Video id is required.");
                NotifyRequestCompleted(invalidRequest);
                return invalidRequest;
            }

            await EnsureManifestAttemptedAsync(cancellationToken);

            if (manifestLookup.TryGetValue(videoId, out FPVideoManifestItem manifestItem))
            {
                FPVideoRequestResult resolvedResult = await ResolveFromManifestAsync(manifestItem, cancellationToken);
                NotifyRequestCompleted(resolvedResult);
                return resolvedResult;
            }

            if (TryResolveCachedOffline(videoId, out FPVideoLocalMeta offlineMeta, out string cachedPath))
            {
                Debug.Log($"[FPVideoCacheManager] Manifest unavailable or missing '{videoId}', using offline cache at '{cachedPath}'.");
                FPVideoRequestResult offlineResult = new FPVideoRequestResult
                {
                    Success = true,
                    VideoId = videoId,
                    ResolvedLocalPath = cachedPath,
                    SourceWasCache = true,
                    DownloadWasPerformed = false,
                    LocalMeta = offlineMeta,
                    ErrorMessage = string.Empty
                };
                NotifyRequestCompleted(offlineResult);
                return offlineResult;
            }

            string error = manifestLoaded
                ? $"Video id '{videoId}' was not found in the manifest and no cached fallback exists."
                : $"Manifest is unavailable and no cached fallback exists for '{videoId}'.";
            FPVideoRequestResult missingResult = CreateFailure(videoId, error);
            NotifyRequestCompleted(missingResult);
            return missingResult;
        }

        public bool TryGetCachedVideoPath(string videoId, out string resolvedLocalPath, out FPVideoLocalMeta localMeta)
        {
            if (TryResolveCachedOffline(videoId, out localMeta, out resolvedLocalPath))
            {
                return true;
            }

            resolvedLocalPath = string.Empty;
            localMeta = null;
            return false;
        }

        private async Task EnsureManifestAttemptedAsync(CancellationToken cancellationToken)
        {
            if (!manifestFetchAttempted)
            {
                await LoadManifestAsync(cancellationToken);
            }
        }

        private async Task<bool> LoadManifestAsync(CancellationToken cancellationToken)
        {
            manifestFetchAttempted = true;

            if (config == null)
            {
                Debug.LogError("[FPVideoCacheManager] Runtime config is missing.");
                manifestLoaded = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.ManifestUrl))
            {
                Debug.LogWarning("[FPVideoCacheManager] Manifest URL is empty. Runtime will rely on local cache only.");
                manifestLoaded = false;
                return false;
            }

            Debug.Log($"[FPVideoCacheManager] Fetching manifest from '{config.ManifestUrl}'.");

            try
            {
                string json = await FetchTextAsync(config.ManifestUrl, cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.LogError("[FPVideoCacheManager] Manifest request succeeded but returned empty JSON.");
                    manifestLoaded = false;
                    return false;
                }

                FPVideoManifestCollection parsed = JsonUtility.FromJson<FPVideoManifestCollection>(json);
                if (parsed?.videos == null || parsed.videos.Length == 0)
                {
                    Debug.LogError("[FPVideoCacheManager] Manifest JSON is malformed or contains no video entries.");
                    manifestLoaded = false;
                    return false;
                }

                manifestCollection = parsed;
                manifestLookup.Clear();

                for (int i = 0; i < parsed.videos.Length; i++)
                {
                    FPVideoManifestItem item = parsed.videos[i];
                    if (!IsManifestItemValid(item))
                    {
                        Debug.LogWarning($"[FPVideoCacheManager] Skipping invalid manifest item at index {i}.");
                        continue;
                    }

                    manifestLookup[item.id] = item;
                }

                manifestLoaded = manifestLookup.Count > 0;
                Debug.Log($"[FPVideoCacheManager] Manifest fetch success. Loaded {manifestLookup.Count} video entries.");
                return manifestLoaded;
            }
            catch (Exception ex)
            {
                manifestLoaded = false;
                Debug.LogWarning($"[FPVideoCacheManager] Manifest fetch failed: {ex.Message}");
                return false;
            }
        }

        private async Task<FPVideoRequestResult> ResolveFromManifestAsync(FPVideoManifestItem manifestItem, CancellationToken cancellationToken)
        {
            string videoId = manifestItem.id;
            string finalVideoPath = GetVideoPath(manifestItem.fileName);
            string metadataPath = GetMetadataPath(videoId);

            if (await IsCacheValidAsync(manifestItem, finalVideoPath, metadataPath, cancellationToken))
            {
                FPVideoLocalMeta localMeta = LoadLocalMeta(metadataPath);
                Debug.Log($"[FPVideoCacheManager] Cache hit for '{videoId}' at '{finalVideoPath}'.");
                return new FPVideoRequestResult
                {
                    Success = true,
                    VideoId = videoId,
                    ResolvedLocalPath = finalVideoPath,
                    SourceWasCache = true,
                    DownloadWasPerformed = false,
                    ManifestItem = manifestItem,
                    LocalMeta = localMeta,
                    ErrorMessage = string.Empty
                };
            }

            if (string.IsNullOrWhiteSpace(manifestItem.downloadUrl))
            {
                return CreateFailure(videoId, $"Cache miss for '{videoId}' and manifest download URL is empty.");
            }

            bool downloadSuccess = await DownloadAndCacheAsync(manifestItem, finalVideoPath, metadataPath, cancellationToken);
            if (!downloadSuccess)
            {
                if (TryResolveCachedOffline(videoId, out FPVideoLocalMeta offlineMeta, out string cachedPath))
                {
                    Debug.LogWarning($"[FPVideoCacheManager] Download failed for '{videoId}', but an existing cache copy is still usable.");
                    return new FPVideoRequestResult
                    {
                        Success = true,
                        VideoId = videoId,
                        ResolvedLocalPath = cachedPath,
                        SourceWasCache = true,
                        DownloadWasPerformed = false,
                        ManifestItem = manifestItem,
                        LocalMeta = offlineMeta,
                        ErrorMessage = string.Empty
                    };
                }

                return CreateFailure(videoId, $"Failed to download or validate '{videoId}'.");
            }

            FPVideoLocalMeta updatedMeta = LoadLocalMeta(metadataPath);
            return new FPVideoRequestResult
            {
                Success = true,
                VideoId = videoId,
                ResolvedLocalPath = finalVideoPath,
                SourceWasCache = false,
                DownloadWasPerformed = true,
                ManifestItem = manifestItem,
                LocalMeta = updatedMeta,
                ErrorMessage = string.Empty
            };
        }

        private async Task<bool> DownloadAndCacheAsync(FPVideoManifestItem manifestItem, string finalVideoPath, string metadataPath, CancellationToken cancellationToken)
        {
            string tempFilePath = $"{finalVideoPath}.downloading";

            try
            {
                DeleteFileIfPresent(tempFilePath);

                Debug.Log($"[FPVideoCacheManager] Download start for '{manifestItem.id}' from '{manifestItem.downloadUrl}'.");
                VideoDownloadStarted?.Invoke(manifestItem);
                await FPVideoDownloadUtility.DownloadToFileAsync(manifestItem.downloadUrl, tempFilePath, cancellationToken);

                bool tempIsValid = await ValidateDownloadedFileAsync(manifestItem, tempFilePath, cancellationToken);
                if (!tempIsValid)
                {
                    Debug.LogError($"[FPVideoCacheManager] Downloaded file failed validation for '{manifestItem.id}'.");
                    DeleteFileIfPresent(tempFilePath);
                    return false;
                }

                DeleteFileIfPresent(finalVideoPath);
                File.Move(tempFilePath, finalVideoPath);

                FPVideoLocalMeta meta = new FPVideoLocalMeta
                {
                    id = manifestItem.id,
                    version = manifestItem.version,
                    sha256 = manifestItem.sha256,
                    fileName = manifestItem.fileName,
                    contentLength = manifestItem.contentLength,
                    cachedAtUtc = DateTime.UtcNow.ToString("o")
                };

                SaveLocalMeta(metadataPath, meta);
                Debug.Log($"[FPVideoCacheManager] Download success for '{manifestItem.id}'. Cached to '{finalVideoPath}'.");
                VideoDownloadCompleted?.Invoke(new FPVideoRequestResult
                {
                    Success = true,
                    VideoId = manifestItem.id,
                    ResolvedLocalPath = finalVideoPath,
                    SourceWasCache = false,
                    DownloadWasPerformed = true,
                    ManifestItem = manifestItem,
                    LocalMeta = meta,
                    ErrorMessage = string.Empty
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FPVideoCacheManager] Download failed for '{manifestItem.id}': {ex.Message}");
                DeleteFileIfPresent(tempFilePath);
                VideoDownloadCompleted?.Invoke(new FPVideoRequestResult
                {
                    Success = false,
                    VideoId = manifestItem.id,
                    ResolvedLocalPath = string.Empty,
                    SourceWasCache = false,
                    DownloadWasPerformed = false,
                    ManifestItem = manifestItem,
                    ErrorMessage = ex.Message
                });
                return false;
            }
        }

        private async Task<bool> IsCacheValidAsync(FPVideoManifestItem manifestItem, string videoPath, string metadataPath, CancellationToken cancellationToken)
        {
            if (!File.Exists(videoPath))
            {
                Debug.Log($"[FPVideoCacheManager] Cache miss for '{manifestItem.id}': video file missing.");
                return false;
            }

            if (!File.Exists(metadataPath))
            {
                Debug.Log($"[FPVideoCacheManager] Cache miss for '{manifestItem.id}': metadata missing.");
                return false;
            }

            FPVideoLocalMeta localMeta = LoadLocalMeta(metadataPath);
            if (localMeta == null)
            {
                Debug.Log($"[FPVideoCacheManager] Cache miss for '{manifestItem.id}': metadata unreadable.");
                return false;
            }

            if (!string.Equals(localMeta.version, manifestItem.version, StringComparison.Ordinal))
            {
                Debug.Log($"[FPVideoCacheManager] Version mismatch for '{manifestItem.id}'. Local '{localMeta.version}' vs remote '{manifestItem.version}'.");
                return false;
            }

            if (config != null && config.EnableSizeValidation && localMeta.contentLength != manifestItem.contentLength)
            {
                Debug.Log($"[FPVideoCacheManager] Metadata size mismatch for '{manifestItem.id}'.");
                return false;
            }

            if (config != null && config.EnableHashValidation &&
                !string.Equals(localMeta.sha256, manifestItem.sha256, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[FPVideoCacheManager] Metadata hash mismatch for '{manifestItem.id}'.");
                return false;
            }

            FileInfo fileInfo = new FileInfo(videoPath);
            if (config != null && config.EnableSizeValidation && fileInfo.Length != manifestItem.contentLength)
            {
                Debug.Log($"[FPVideoCacheManager] File size mismatch for '{manifestItem.id}'. Expected {manifestItem.contentLength}, got {fileInfo.Length}.");
                return false;
            }

            if (config != null && config.UseStrictValidation && config.EnableHashValidation)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool hashMatch = await Task.Run(() => FPVideoHashUtility.HashMatches(manifestItem.sha256, videoPath), cancellationToken);
                if (!hashMatch)
                {
                    Debug.LogWarning($"[FPVideoCacheManager] File hash mismatch for '{manifestItem.id}'. Removing invalid cache copy.");
                    DeleteInvalidCachedFiles(videoPath, metadataPath);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> ValidateDownloadedFileAsync(FPVideoManifestItem manifestItem, string tempFilePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(tempFilePath))
            {
                return false;
            }

            FileInfo fileInfo = new FileInfo(tempFilePath);
            if (manifestItem.contentLength > 0 && fileInfo.Length != manifestItem.contentLength)
            {
                Debug.LogError($"[FPVideoCacheManager] Downloaded size mismatch for '{manifestItem.id}'. Expected {manifestItem.contentLength}, got {fileInfo.Length}.");
                return false;
            }

            if (config != null && config.EnableHashValidation && !string.IsNullOrWhiteSpace(manifestItem.sha256))
            {
                bool hashMatches = await Task.Run(() => FPVideoHashUtility.HashMatches(manifestItem.sha256, tempFilePath), cancellationToken);
                if (!hashMatches)
                {
                    Debug.LogError($"[FPVideoCacheManager] SHA256 mismatch for downloaded '{manifestItem.id}'.");
                    return false;
                }
            }

            return true;
        }

        private bool TryResolveCachedOffline(string videoId, out FPVideoLocalMeta localMeta, out string cachedPath)
        {
            localMeta = LoadLocalMeta(GetMetadataPath(videoId));
            if (localMeta == null)
            {
                cachedPath = string.Empty;
                return false;
            }

            cachedPath = GetVideoPath(localMeta.fileName);
            if (!File.Exists(cachedPath))
            {
                cachedPath = string.Empty;
                return false;
            }

            if (config != null && config.EnableSizeValidation && localMeta.contentLength > 0)
            {
                FileInfo fileInfo = new FileInfo(cachedPath);
                if (fileInfo.Length != localMeta.contentLength)
                {
                    Debug.LogWarning($"[FPVideoCacheManager] Offline cache size mismatch for '{videoId}'.");
                    cachedPath = string.Empty;
                    return false;
                }
            }

            if (config != null && config.UseStrictValidation && config.EnableHashValidation && !string.IsNullOrWhiteSpace(localMeta.sha256))
            {
                if (!FPVideoHashUtility.HashMatches(localMeta.sha256, cachedPath))
                {
                    Debug.LogWarning($"[FPVideoCacheManager] Offline cache hash mismatch for '{videoId}'.");
                    DeleteInvalidCachedFiles(cachedPath, GetMetadataPath(videoId));
                    cachedPath = string.Empty;
                    localMeta = null;
                    return false;
                }
            }

            return true;
        }

        private static bool IsManifestItemValid(FPVideoManifestItem item)
        {
            if (item == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(item.id) &&
                   !string.IsNullOrWhiteSpace(item.version) &&
                   !string.IsNullOrWhiteSpace(item.fileName);
        }

        private void EnsureCacheDirectoriesExist()
        {
            Directory.CreateDirectory(CacheRootPath);
            Directory.CreateDirectory(VideosFolderPath);
            Directory.CreateDirectory(MetadataFolderPath);
        }

        private string GetVideoPath(string fileName)
        {
            string safeName = Path.GetFileName(fileName ?? string.Empty);
            return Path.Combine(VideosFolderPath, safeName);
        }

        private string GetMetadataPath(string videoId)
        {
            return Path.Combine(MetadataFolderPath, $"{MakeSafeId(videoId)}.json");
        }

        private static string MakeSafeId(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                return "video_item";
            }

            char[] chars = rawId.Trim().ToLowerInvariant().ToCharArray();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(chars.Length);

            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (char.IsLetterOrDigit(current))
                {
                    sb.Append(current);
                }
                else if (current == '_' || current == '-' || current == ' ')
                {
                    sb.Append('_');
                }
            }

            return sb.Length == 0 ? "video_item" : sb.ToString();
        }

        private static FPVideoLocalMeta LoadLocalMeta(string metadataPath)
        {
            try
            {
                if (!File.Exists(metadataPath))
                {
                    return null;
                }

                string json = File.ReadAllText(metadataPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonUtility.FromJson<FPVideoLocalMeta>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FPVideoCacheManager] Failed to load local metadata '{metadataPath}': {ex.Message}");
                return null;
            }
        }

        private static void SaveLocalMeta(string metadataPath, FPVideoLocalMeta localMeta)
        {
            string json = JsonUtility.ToJson(localMeta, true);
            File.WriteAllText(metadataPath, json);
        }

        private static void DeleteFileIfPresent(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static void DeleteInvalidCachedFiles(string videoPath, string metadataPath)
        {
            DeleteFileIfPresent(videoPath);
            DeleteFileIfPresent(metadataPath);
        }

        private static async Task<string> FetchTextAsync(string url, CancellationToken cancellationToken)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                using (cancellationToken.Register(request.Abort))
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!RequestSucceeded(request))
                {
                    throw new InvalidOperationException(request.error);
                }

                return request.downloadHandler.text;
            }
        }

        private static bool RequestSucceeded(UnityWebRequest request)
        {
#if UNITY_2020_2_OR_NEWER
            return request.result == UnityWebRequest.Result.Success;
#else
            return !request.isNetworkError && !request.isHttpError;
#endif
        }

        private static FPVideoRequestResult CreateFailure(string videoId, string errorMessage)
        {
            Debug.LogError($"[FPVideoCacheManager] {errorMessage}");
            return new FPVideoRequestResult
            {
                Success = false,
                VideoId = videoId,
                ResolvedLocalPath = string.Empty,
                SourceWasCache = false,
                DownloadWasPerformed = false,
                ErrorMessage = errorMessage
            };
        }

        private void NotifyRequestCompleted(FPVideoRequestResult result)
        {
            VideoRequestCompleted?.Invoke(result);
        }
    }
}
