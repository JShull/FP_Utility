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
                catch (Exception exception) { result = JsonUtility.ToJson(new Failure { error = exception.Message }); }
                await writer.WriteAsync(result);
            }
        }

        [Serializable] private sealed class Failure { public string error; }
    }
}
