// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEngine;

    /// <summary>JSON entry points for Unity CLI eval_file; no Pipeline package dependency.</summary>
    public static class FPElevenLabsGenerationCli
    {
        public static string Prepare(string requestJson)
        {
            var input = JsonUtility.FromJson<FPElevenLabsGenerationManifest>(requestJson);
            return JsonUtility.ToJson(new FPElevenLabsGenerationService().Prepare(
                input.requests, input.maxRequests, input.maxCharacters), true);
        }

        public static string Status(string manifestJson) => JsonUtility.ToJson(
            new FPElevenLabsGenerationService().Status(JsonUtility.FromJson<FPElevenLabsGenerationManifest>(manifestJson)), true);

        public static async Task<string> Execute(string manifestJson, string approvedHash) => JsonUtility.ToJson(
            await new FPElevenLabsGenerationService().ExecuteAsync(
                JsonUtility.FromJson<FPElevenLabsGenerationManifest>(manifestJson), approvedHash), true);

        public static Task<string> Resume(string manifestJson, string approvedHash) => Execute(manifestJson, approvedHash);

        /// <summary>Starts asynchronous work from synchronous eval hosts; resultPath is a new local report file.</summary>
        public static string Start(string manifestJson, string approvedHash, string resultPath)
        {
            var stream = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            _ = WriteResultAsync(manifestJson, approvedHash, stream);
            return "Started. Result file remains empty until completion. Status reads the durable ledger; do not retry on a client timeout.";
        }

        private static async Task WriteResultAsync(string manifestJson, string approvedHash, FileStream stream)
        {
            using (stream)
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                string result;
                try { result = await Execute(manifestJson, approvedHash); }
                catch (Exception exception) { result = BuildFailureJson(exception); }
                await writer.WriteAsync(result);
            }
        }

        internal static string BuildFailureJson(Exception exception)
        {
            var failure = new Failure { error = exception.Message };
            // Full filesystem diagnostics are local-only and contain no provider request/credential data.
            if (exception is FPElevenLabsGenerationService.AtomicSaveException save)
            {
                failure.destination = save.Destination;
                failure.temporary = save.Temporary;
                failure.operation = save.Operation;
                failure.attempts = save.Attempts;
                failure.destinationAttributes = save.DestinationAttributes;
                failure.exceptionType = save.InnerException.GetType().FullName;
                failure.hResult = "0x" + save.InnerException.HResult.ToString("X8");
                failure.stack = save.ToString();
            }
            return JsonUtility.ToJson(failure, true);
        }

        [Serializable] private sealed class Failure
        {
            public string error, destination, temporary, operation, destinationAttributes, exceptionType, hResult, stack;
            public int attempts;
        }
    }
}
