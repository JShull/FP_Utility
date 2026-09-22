// Copyright (c) 2026 John B. Shull. FuzzPhyte LLC.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.FileShare
{
    using System;
    using System.IO;

    public enum FPFileShareFailure
    {
        TokenRejected,
        Conflict,
        SizeLimit,
        ChecksumMismatch,
        ConnectionFailure,
        Timeout,
        PolicyRejected,
        ReceiptMismatch,
        HttpError
    }

    /// <summary>Transfer failure safe to present without exposing request credentials or file contents.</summary>
    public sealed class FPFileShareException : IOException
    {
        public FPFileShareFailure Failure { get; }
        /// <summary>HTTP status when received; zero means no HTTP response was available.</summary>
        public long HttpStatusCode { get; }

        public FPFileShareException(FPFileShareFailure failure, string message, long httpStatusCode = 0,
            Exception innerException = null) : base(message, innerException)
        {
            Failure = failure;
            HttpStatusCode = httpStatusCode;
        }

        internal static FPFileShareException FromResponse(long status, string error)
        {
            FPFileShareFailure failure;
            string detail;
            switch (status)
            {
                case 401: failure = FPFileShareFailure.TokenRejected; detail = "Pairing token rejected. Copy the current token from the receiver."; break;
                case 409: failure = FPFileShareFailure.Conflict; detail = "Transfer ID already belongs to a different file or contents."; break;
                case 413: failure = FPFileShareFailure.SizeLimit; detail = "File exceeds the receiver size limit."; break;
                case 422: failure = FPFileShareFailure.ChecksumMismatch; detail = "Receiver rejected the checksum. Keep the source unchanged while sending."; break;
                default:
                    if (IsPolicyError(error)) return Policy();
                    bool timeout = error != null && (error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0
                        || error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0);
                    failure = status > 0 ? FPFileShareFailure.HttpError : timeout ? FPFileShareFailure.Timeout : FPFileShareFailure.ConnectionFailure;
                    detail = failure == FPFileShareFailure.Timeout ? "Transfer timed out. Retry with the same transfer ID."
                        : status > 0 ? "Receiver rejected the request."
                        : "Could not reach the receiver. Check its address, listener, local-network permission and firewall.";
                    break;
            }
            return new FPFileShareException(failure, $"Transfer failed (HTTP {status}): {detail} The local file is retained.", status);
        }

        internal static bool IsPolicyError(string message) => message != null
            && (message.IndexOf("non-secure", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("insecure connection", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("cleartext", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("App Transport Security", StringComparison.OrdinalIgnoreCase) >= 0);

        internal static FPFileShareException Policy(Exception inner = null) => new FPFileShareException(
            FPFileShareFailure.PolicyRejected,
            "The application blocked this connection under its HTTP/security policy. Check Unity Allow downloads over HTTP and the iOS ATS build configuration. FileShare does not change these settings. The local file is retained.",
            innerException: inner);
    }
}
