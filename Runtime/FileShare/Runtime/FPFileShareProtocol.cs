// Copyright (c) 2026 John B. Shull. FuzzPhyte LLC.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.FileShare
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;

    /// <summary>Versioned, file-only HTTP contract shared by senders and Editor receivers.</summary>
    public static class FPFileShareProtocol
    {
        public const string Route = "/fp-fileshare/v1/files/";
        public const string TokenHeader = "X-FP-Token";
        public const string NameHeader = "X-FP-Name";
        public const string HashHeader = "X-FP-SHA256";
        public const string IdHeader = "X-FP-Transfer";
        public const string LengthHeader = "X-FP-Length";
        public const long DefaultMaximumBytes = 128L * 1024 * 1024;
        // 32 equally likely symbols, excluding 0, 1, I and O: 12 characters = 60 random bits.
        private const string TokenAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        public static string ValidateFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || name == "." || name == ".."
                || name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal))
                throw new ArgumentException("Use a file name of 1–160 characters without a trailing dot or space.");
            foreach (char c in name)
                if (char.IsControl(c) || "<>:\"/\\|?*".IndexOf(c) >= 0)
                    throw new ArgumentException("The file name contains a path or an unsupported character.");
            return name;
        }

        public static string EncodeFileName(string name) => Convert.ToBase64String(Encoding.UTF8.GetBytes(ValidateFileName(name)));

        public static string DecodeFileName(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || encoded.Length > 1024)
                throw new ArgumentException("Missing or oversized file name.");
            return ValidateFileName(new UTF8Encoding(false, true).GetString(Convert.FromBase64String(encoded)));
        }

        public static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        public static string HashFile(string path, CancellationToken cancellationToken = default)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, System.IO.FileShare.Read))
            using (var hash = SHA256.Create())
            {
                var buffer = new byte[65536];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hash.TransformBlock(buffer, 0, count, buffer, 0);
                }
                cancellationToken.ThrowIfCancellationRequested();
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string CreateToken()
        {
            var bytes = new byte[12];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var token = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++) token[i] = TokenAlphabet[bytes[i] % TokenAlphabet.Length];
            return new string(token);
        }

        /// <summary>Accept lower-case and optional spaces/dashes for short tokens. Preserve legacy Base64 tokens exactly.</summary>
        public static string NormalizeToken(string value)
        {
            if (value == null || value.Length > 128) return value;
            string compact = value.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            if (compact.Length != 12) return value;
            foreach (char c in compact) if (TokenAlphabet.IndexOf(c) < 0) return value;
            return compact;
        }
    }

    public sealed class FPFileShareReceipt
    {
        public Guid TransferId { get; }
        public string FileName { get; }
        public long ByteLength { get; }
        public string Sha256 { get; }
        public bool AlreadyReceived { get; }

        public FPFileShareReceipt(Guid transferId, string fileName, long byteLength, string sha256, bool alreadyReceived)
        {
            TransferId = transferId;
            FileName = fileName;
            ByteLength = byteLength;
            Sha256 = sha256;
            AlreadyReceived = alreadyReceived;
        }
    }
}
