// Copyright (c) 2026 John B. Shull. FuzzPhyte LLC.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.FileShare
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine.Networking;

    public static class FPFileShareSender
    {
        /// <summary>
        /// Call on Unity's main thread. Retain transferId for a manual retry of this same file.
        /// No automatic retries or deletion of the source file. Keep the source unchanged during transfer.
        /// Cancellation after server commit can have an uncertain outcome; retry with the same ID.
        /// </summary>
        public static async Task<FPFileShareReceipt> SendAsync(string filePath, string receiverUrl,
            string pairingToken, Guid transferId, IProgress<float> progress = null,
            CancellationToken cancellationToken = default, int timeoutSeconds = 120)
        {
            if (SynchronizationContext.Current == null)
                throw new InvalidOperationException("Start the sender on Unity's main thread.");
            if (!Uri.TryCreate(receiverUrl, UriKind.Absolute, out var endpoint)
                || (endpoint.Scheme != "http" && endpoint.Scheme != "https")
                || !string.IsNullOrEmpty(endpoint.UserInfo) || endpoint.AbsolutePath != "/"
                || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
                throw new ArgumentException("Enter the receiver origin, for example http://192.168.1.20:18765/.");
            if (string.IsNullOrWhiteSpace(pairingToken) || pairingToken.Length > 128
                || pairingToken.Contains("\r") || pairingToken.Contains("\n"))
                throw new ArgumentException("Enter a valid pairing token.");
            if (transferId == Guid.Empty) throw new ArgumentException("A non-empty transfer ID is required.");
            pairingToken = FPFileShareProtocol.NormalizeToken(pairingToken);
            if (timeoutSeconds < 1) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            string path = Path.GetFullPath(filePath);
            string name = FPFileShareProtocol.ValidateFileName(Path.GetFileName(path));
            long length = new FileInfo(path).Length;
            cancellationToken.ThrowIfCancellationRequested();
            string hash = await Task.Run(() => FPFileShareProtocol.HashFile(path, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            using (var request = new UnityWebRequest(new Uri(endpoint, FPFileShareProtocol.Route + transferId.ToString("N")), "POST"))
            {
                request.uploadHandler = new UploadHandlerFile(path);
                // The receipt uses headers; discard the response body rather than buffering arbitrary data.
                request.redirectLimit = 0;
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                request.SetRequestHeader(FPFileShareProtocol.TokenHeader, pairingToken);
                request.SetRequestHeader(FPFileShareProtocol.NameHeader, FPFileShareProtocol.EncodeFileName(name));
                request.SetRequestHeader(FPFileShareProtocol.HashHeader, hash);
                UnityWebRequestAsyncOperation operation;
                try { operation = request.SendWebRequest(); }
                catch (Exception ex) when (FPFileShareException.IsPolicyError(ex.Message))
                {
                    throw FPFileShareException.Policy(ex);
                }
                // Abort immediately on main-thread cancellation (including Editor teardown),
                // rather than depending on another frame to resume the polling loop.
                using var abortRegistration = cancellationToken.Register(request.Abort, useSynchronizationContext: true);
                try
                {
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(Math.Max(0f, request.uploadProgress));
                        await Task.Yield();
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (request.result != UnityWebRequest.Result.Success
                        || (request.responseCode != 200 && request.responseCode != 201))
                        throw FPFileShareException.FromResponse(request.responseCode, request.error);
                    if (request.GetResponseHeader(FPFileShareProtocol.IdHeader) != transferId.ToString("N")
                        || request.GetResponseHeader(FPFileShareProtocol.HashHeader) != hash
                        || request.GetResponseHeader(FPFileShareProtocol.LengthHeader) != length.ToString(CultureInfo.InvariantCulture)
                        || request.GetResponseHeader(FPFileShareProtocol.NameHeader) != FPFileShareProtocol.EncodeFileName(name))
                        throw new FPFileShareException(FPFileShareFailure.ReceiptMismatch,
                            "The receiver did not return a matching saved-file receipt. Retry with the same transfer ID.", request.responseCode);
                    progress?.Report(1f);
                    return new FPFileShareReceipt(transferId, name, length, hash, request.responseCode == 200);
                }
                catch
                {
                    request.Abort();
                    throw;
                }
            }
        }
    }
}
