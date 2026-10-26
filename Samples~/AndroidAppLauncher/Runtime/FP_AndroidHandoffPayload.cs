// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.
namespace FuzzPhyte.Utility
{
    using System;
    using System.Text;
    using UnityEngine;

    /// <summary>Domain-neutral envelope. Data may be plain text or caller-owned JSON.</summary>
    [Serializable]
    public sealed class FP_AndroidHandoffPayload
    {
        public const int CurrentVersion = 1;
        public const int MaxUtf8Bytes = 16384;
        public int version;
        public string data;

        public static bool TrySerialize(string data, out string json, out string error)
        {
            json = null;
            error = null;
            if (string.IsNullOrWhiteSpace(data) || Encoding.UTF8.GetByteCount(data) > MaxUtf8Bytes)
            {
                error = "Handoff data must be nonempty and at most 16 KiB UTF-8.";
                return false;
            }
            string candidate = JsonUtility.ToJson(new FP_AndroidHandoffPayload { version = CurrentVersion, data = data });
            if (Encoding.UTF8.GetByteCount(candidate) > MaxUtf8Bytes)
            {
                error = "Encoded handoff envelope exceeds 16 KiB UTF-8.";
                return false;
            }
            json = candidate;
            return true;
        }

        public static bool TryDeserialize(string json, out FP_AndroidHandoffPayload payload, out string error)
        {
            payload = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxUtf8Bytes)
            {
                error = "Missing or oversized handoff envelope.";
                return false;
            }
            string trimmed = json.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                error = "Expected a JSON object envelope.";
                return false;
            }
            try
            {
                var candidate = JsonUtility.FromJson<FP_AndroidHandoffPayload>(trimmed);
                if (candidate == null || candidate.version != CurrentVersion || string.IsNullOrWhiteSpace(candidate.data))
                {
                    error = "Unsupported/missing payload version or missing data.";
                    return false;
                }
                payload = candidate;
                return true;
            }
            catch (ArgumentException)
            {
                error = "Malformed handoff JSON.";
                return false;
            }
        }
    }
}
