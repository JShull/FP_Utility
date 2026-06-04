// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Video
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine.Networking;

    public static class FPVideoDownloadUtility
    {
        public static async Task DownloadToFileAsync(string url, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Download URL is required.", nameof(url));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("Destination path is required.", nameof(destinationPath));
            }

            string parentDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(destinationPath, true);
                request.disposeDownloadHandlerOnDispose = true;

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
                    throw new InvalidOperationException($"Download failed for '{url}': {request.error}");
                }
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
    }
}
