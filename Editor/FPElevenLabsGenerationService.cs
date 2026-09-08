// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;

    [Serializable]
    public sealed class FPElevenLabsGenerationRequest
    {
        public string operation = "speech";
        public string text;
        public string voiceId;
        public string modelId = "eleven_v3";
        public string sourceLanguage = "English";
        public string targetLanguage = "French";
        public string outputAssetPath;
        // Explicit caller assertion that this existing clip is the desired result for this exact request.
        public string existingSha256;
    }

    [Serializable]
    public sealed class FPElevenLabsGenerationManifest
    {
        public int version = 1;
        public string projectPath;
        public int maxRequests;
        public int maxCharacters;
        public FPElevenLabsGenerationRequest[] requests;
        public string hash;
        public FPElevenLabsGenerationStatus[] items;
        public int newRequests;
        public int newCharacters;
    }

    [Serializable]
    public sealed class FPElevenLabsGenerationStatus
    {
        public string requestHash;
        public string state;
        public string outputAssetPath;
        public bool existingFile;
        public string existingSha256;
        public string resultText;
    }

    /// <summary>
    /// Main-thread Editor service. Prepare is offline; Execute and Resume share the same
    /// authorization and durable reservation path. Limits count submitted attempts, including failures.
    /// </summary>
    public sealed class FPElevenLabsGenerationService
    {
        private readonly string projectPath;
        private readonly string storagePath;
        private readonly Func<FPElevenLabsGenerationRequest, Task<byte[]>> send;
        private readonly Action<string> import;
        private readonly Action<FPElevenLabsGenerationRequest> validateCredentials;

        public FPElevenLabsGenerationService() : this(
            Path.GetDirectoryName(Application.dataPath), SendAsync,
            path =>
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                    throw new InvalidOperationException("Audio saved, but Unity import failed. Resume to retry import.");
            }, ValidateCredentials) { }

        internal FPElevenLabsGenerationService(string projectPath,
            Func<FPElevenLabsGenerationRequest, Task<byte[]>> send, Action<string> import,
            Action<FPElevenLabsGenerationRequest> validateCredentials = null)
        {
            this.projectPath = Path.GetFullPath(projectPath).TrimEnd('/', '\\');
            storagePath = Path.Combine(this.projectPath, "UserSettings", "FPElevenLabs");
            this.send = send;
            this.import = import;
            this.validateCredentials = validateCredentials ?? (_ => { });
        }

        public FPElevenLabsGenerationManifest Prepare(FPElevenLabsGenerationRequest[] requests,
            int maxRequests, int maxCharacters)
        {
            // Snapshot caller-owned inputs, so edits during awaits cannot change an approved request.
            var manifest = new FPElevenLabsGenerationManifest
            {
                projectPath = projectPath, requests = requests,
                maxRequests = maxRequests, maxCharacters = maxCharacters
            };
            manifest = JsonUtility.FromJson<FPElevenLabsGenerationManifest>(JsonUtility.ToJson(manifest));
            Validate(manifest);
            manifest.hash = ManifestHash(manifest);
            return Inspect(manifest, ReadLedger());
        }

        public FPElevenLabsGenerationManifest Status(FPElevenLabsGenerationManifest manifest)
        {
            manifest = Snapshot(manifest);
            return Inspect(manifest, ReadLedger());
        }

        public Task<FPElevenLabsGenerationManifest> ResumeAsync(FPElevenLabsGenerationManifest manifest,
            string approvedHash) => ExecuteAsync(manifest, approvedHash);

        public async Task<FPElevenLabsGenerationManifest> ExecuteAsync(
            FPElevenLabsGenerationManifest manifest, string approvedHash)
        {
            manifest = Snapshot(manifest);
            if (string.IsNullOrEmpty(approvedHash) || !string.Equals(approvedHash, manifest.hash, StringComparison.Ordinal))
                throw new InvalidOperationException("Explicit approval of the exact prepared manifest hash is required.");

            Directory.CreateDirectory(storagePath);
            // One lease covers every window/CLI route, including asynchronous network and import work.
            using (new FileStream(Path.Combine(storagePath, "execution.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None))
            {
                Ledger ledger = ReadLedger();
                Inspect(manifest, ledger);
                if (manifest.items.Any(item => item.state == "uncertain" || item.state == "conflict"))
                    throw new InvalidOperationException("Manifest contains uncertain requests or output conflicts. Inspect status; automatic retry is blocked.");
                int spentRequests = ledger.entries.Count(entry => entry.manifestHash == manifest.hash);
                long spentCharacters = ledger.entries.Where(entry => entry.manifestHash == manifest.hash).Sum(entry => (long)entry.characters);
                if ((long)spentRequests + manifest.newRequests > manifest.maxRequests ||
                    spentCharacters + manifest.newCharacters > manifest.maxCharacters)
                    throw new InvalidOperationException("Manifest request/character limits would be exceeded.");
                foreach (var request in manifest.requests)
                    if (string.IsNullOrEmpty(request.existingSha256) && !ledger.entries.Any(entry => entry.requestHash == RequestHash(request))) validateCredentials(request);

                WriteAtomic(Path.Combine(storagePath, manifest.hash + ".manifest.json"),
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest, true)));

                EditorApplication.LockReloadAssemblies();
                try
                {
                    foreach (var request in manifest.requests)
                    {
                        string key = RequestHash(request);
                        Entry entry = ledger.entries.Find(item => item.requestHash == key);
                        string cache = Path.Combine(storagePath, key + ".response");
                        if (!string.IsNullOrEmpty(request.existingSha256) && (entry == null || entry.state != "saved"))
                        {
                            byte[] existingBytes = File.ReadAllBytes(ResolveOutput(request.outputAssetPath));
                            if (Hash(existingBytes) != request.existingSha256) throw new IOException("Reviewed existing clip changed.");
                            WriteAtomic(cache, existingBytes);
                            if (entry == null)
                            {
                                entry = new Entry { requestHash = key, utc = DateTime.UtcNow.ToString("O") };
                                ledger.entries.Add(entry);
                            }
                            entry.sha256 = request.existingSha256;
                            entry.state = "saved";
                            WriteLedger(ledger);
                        }
                        if (entry == null)
                        {
                            // Reserve budget and mark uncertainty BEFORE sending. No hidden retry on errors.
                            entry = new Entry { requestHash = key, manifestHash = manifest.hash,
                                characters = request.text.Length, state = "uncertain", utc = DateTime.UtcNow.ToString("O") };
                            ledger.entries.Add(entry);
                            WriteLedger(ledger);
                            byte[] bytes = await send(request);
                            if (bytes == null || bytes.Length == 0) throw new InvalidOperationException("Provider returned an empty response.");
                            WriteAtomic(cache, bytes);
                            entry.sha256 = Hash(bytes);
                            entry.state = "saved";
                            WriteLedger(ledger);
                        }
                        // A crash after the atomic response save can be recovered without contacting a provider.
                        if (entry.state == "uncertain" && File.Exists(cache))
                        {
                            entry.sha256 = Hash(File.ReadAllBytes(cache));
                            entry.state = "saved";
                            WriteLedger(ledger);
                        }
                        if (entry.state != "saved" || !File.Exists(cache) || Hash(File.ReadAllBytes(cache)) != entry.sha256)
                            throw new InvalidOperationException("Saved response is missing or changed; generation is blocked to prevent repurchase.");
                        if (request.operation == "speech")
                        {
                            string output = ResolveOutput(request.outputAssetPath);
                            if (File.Exists(output))
                            {
                                if (Hash(File.ReadAllBytes(output)) != entry.sha256)
                                    throw new IOException("Output differs from the saved response: " + request.outputAssetPath);
                            }
                            else WriteAtomic(output, File.ReadAllBytes(cache), false);
                            import(request.outputAssetPath);
                        }
                    }
                }
                finally { EditorApplication.UnlockReloadAssemblies(); }
                return Inspect(manifest, ledger);
            }
        }

        private FPElevenLabsGenerationManifest Snapshot(FPElevenLabsGenerationManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            manifest = JsonUtility.FromJson<FPElevenLabsGenerationManifest>(JsonUtility.ToJson(manifest));
            Validate(manifest);
            if (manifest.hash != ManifestHash(manifest)) throw new InvalidOperationException("Manifest changed. Prepare and approve it again.");
            return manifest;
        }

        private void Validate(FPElevenLabsGenerationManifest manifest)
        {
            if (manifest.version != 1 || manifest.projectPath != projectPath)
                throw new InvalidOperationException("Manifest version or Unity project does not match.");
            if (manifest.maxRequests < 0 || manifest.maxCharacters < 0 || manifest.requests == null || manifest.requests.Length == 0)
                throw new ArgumentException("Provide requests and non-negative limits; zero authorizes no new requests.");
            var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var request in manifest.requests)
            {
                if (request == null || string.IsNullOrWhiteSpace(request.text) || string.IsNullOrWhiteSpace(request.modelId))
                    throw new ArgumentException("Every request needs exact text and a model ID.");
                if (request.operation == "speech")
                {
                    if (string.IsNullOrWhiteSpace(request.voiceId)) throw new ArgumentException("Speech requires a voice ID.");
                    if (!string.IsNullOrEmpty(request.existingSha256) &&
                        (request.existingSha256.Length != 64 || request.existingSha256.Any(c => !"0123456789abcdef".Contains(c.ToString()))))
                        throw new ArgumentException("existingSha256 must be a lowercase SHA-256 digest.");
                    string output = ResolveOutput(request.outputAssetPath);
                    if (!outputs.Add(output)) throw new ArgumentException("Duplicate output path: " + request.outputAssetPath);
                }
                else if (request.operation == "translation")
                {
                    ParseLanguage(request.sourceLanguage);
                    ParseLanguage(request.targetLanguage);
                    if (request.sourceLanguage == request.targetLanguage) throw new ArgumentException("Translation needs different languages.");
                    if (!string.IsNullOrEmpty(request.outputAssetPath)) throw new ArgumentException("Translations are stored in the ledger cache, not an audio asset.");
                    if (!string.IsNullOrEmpty(request.existingSha256)) throw new ArgumentException("Existing clip adoption is speech-only.");
                }
                else throw new ArgumentException("Operation must be speech or translation.");
            }
        }

        private string ResolveOutput(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("\\") || !path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Speech output must be an Assets/.../*.mp3 path.");
            string full = Path.GetFullPath(Path.Combine(projectPath, path));
            string assets = Path.Combine(projectPath, "Assets") + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(Path.GetDirectoryName(full)))
                throw new ArgumentException("Output must be inside an existing Assets folder.");
            // Reject linked directories to keep writes inside the reviewed project location.
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(full)); directory != null; directory = directory.Parent)
            {
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Linked output directories are not supported.");
                if (directory.FullName == projectPath) break;
            }
            if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Linked output files are not supported.");
            return full;
        }

        private FPElevenLabsGenerationManifest Inspect(FPElevenLabsGenerationManifest manifest, Ledger ledger)
        {
            manifest.newRequests = 0;
            manifest.newCharacters = 0;
            var counted = new HashSet<string>();
            manifest.items = manifest.requests.Select(request =>
            {
                string key = RequestHash(request);
                Entry entry = ledger.entries.Find(item => item.requestHash == key);
                string cache = Path.Combine(storagePath, key + ".response");
                bool cached = File.Exists(cache) && new FileInfo(cache).Length > 0 && entry != null &&
                    (entry.state == "uncertain" || Hash(File.ReadAllBytes(cache)) == entry.sha256);
                string output = request.operation == "speech" ? ResolveOutput(request.outputAssetPath) : null;
                bool existing = output != null && File.Exists(output);
                string existingHash = existing ? Hash(File.ReadAllBytes(output)) : null;
                var status = new FPElevenLabsGenerationStatus { requestHash = key,
                    outputAssetPath = request.outputAssetPath, existingFile = existing, existingSha256 = existingHash,
                    state = entry == null ? "new" : cached ? "cached" : "uncertain" };
                if (existing && (!cached || existingHash != Hash(File.ReadAllBytes(cache)))) status.state = "conflict";
                if (!string.IsNullOrEmpty(request.existingSha256))
                    status.state = existing && existingHash == request.existingSha256 &&
                        (entry == null || entry.state == "uncertain" || (cached && entry.sha256 == existingHash)) ? "existing" : "conflict";
                if (cached && request.operation == "translation") status.resultText = Encoding.UTF8.GetString(File.ReadAllBytes(cache));
                if (status.state == "new" && counted.Add(key))
                {
                    manifest.newRequests++;
                    manifest.newCharacters = checked(manifest.newCharacters + request.text.Length);
                }
                return status;
            }).ToArray();
            return manifest;
        }

        internal static string RequestHash(FPElevenLabsGenerationRequest request)
        {
            // Version pins the provider payload contract, including format and translation output limit.
            return Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RequestIdentity {
                operation = request.operation, text = request.text, model = request.modelId,
                voice = request.operation == "speech" ? request.voiceId : null,
                source = request.operation == "translation" ? request.sourceLanguage : null,
                target = request.operation == "translation" ? request.targetLanguage : null })));
        }

        private static string ManifestHash(FPElevenLabsGenerationManifest manifest)
        {
            return Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new FPElevenLabsGenerationManifest {
                version = manifest.version, projectPath = manifest.projectPath, requests = manifest.requests,
                maxRequests = manifest.maxRequests, maxCharacters = manifest.maxCharacters })));
        }

        private Ledger ReadLedger()
        {
            string path = Path.Combine(storagePath, "ledger.json");
            if (!File.Exists(path)) return new Ledger();
            var ledger = JsonUtility.FromJson<Ledger>(File.ReadAllText(path));
            if (ledger == null || ledger.version != 1 || ledger.entries == null ||
                ledger.entries.Any(entry => entry == null || string.IsNullOrEmpty(entry.requestHash)) ||
                ledger.entries.Select(entry => entry.requestHash).Distinct().Count() != ledger.entries.Count)
                throw new InvalidDataException("Invalid request ledger; restore a backup before generation.");
            return ledger;
        }

        private void WriteLedger(Ledger ledger) => WriteAtomic(Path.Combine(storagePath, "ledger.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(ledger, true)));

        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void WriteAtomic(string path, byte[] bytes, bool replace = true)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (replace && File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static FPTranslationLanguage ParseLanguage(string language)
        {
            if (!Enum.TryParse(language, out FPTranslationLanguage parsed) || !Enum.IsDefined(typeof(FPTranslationLanguage), parsed))
                throw new ArgumentException("Language must be English, Spanish, or French.");
            return parsed;
        }

        private static void ValidateCredentials(FPElevenLabsGenerationRequest item)
        {
            string preference = item.operation == "speech" ? FP_UtilityKeys.ElevenLabsApiKeyPreference : FP_UtilityKeys.ChatGptApiKeyPreference;
            if (string.IsNullOrWhiteSpace(EditorPrefs.GetString(preference))) throw new InvalidOperationException("Save provider credentials in FP Keys Manager first.");
            if (item.operation == "translation" && (string.IsNullOrWhiteSpace(EditorPrefs.GetString(FP_UtilityKeys.ChatGptOrganizationIdPreference)) ||
                string.IsNullOrWhiteSpace(EditorPrefs.GetString(FP_UtilityKeys.ChatGptProjectIdPreference))))
                throw new InvalidOperationException("Save OpenAI organization and project in FP Keys Manager first.");
        }

        private static async Task<byte[]> SendAsync(FPElevenLabsGenerationRequest item)
        {
            bool speech = item.operation == "speech";
            string url = speech ? "https://api.elevenlabs.io/v1/text-to-speech/" + Uri.EscapeDataString(item.voiceId) + "?output_format=mp3_44100_128"
                : "https://api.openai.com/v1/responses";
            string payload = speech ? JsonUtility.ToJson(new SpeechPayload { text = item.text, model_id = item.modelId })
                : JsonUtility.ToJson(new TranslationPayload { model = item.modelId, input = item.text,
                    instructions = FPElevenLabsEditorUtility.BuildTranslationInstructions(ParseLanguage(item.sourceLanguage), ParseLanguage(item.targetLanguage)) });
            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (speech)
                {
                    request.SetRequestHeader("xi-api-key", EditorPrefs.GetString(FP_UtilityKeys.ElevenLabsApiKeyPreference));
                    request.SetRequestHeader("Accept", "audio/mpeg");
                }
                else
                {
                    request.SetRequestHeader("Authorization", "Bearer " + EditorPrefs.GetString(FP_UtilityKeys.ChatGptApiKeyPreference));
                    request.SetRequestHeader("OpenAI-Organization", EditorPrefs.GetString(FP_UtilityKeys.ChatGptOrganizationIdPreference));
                    request.SetRequestHeader("OpenAI-Project", EditorPrefs.GetString(FP_UtilityKeys.ChatGptProjectIdPreference));
                }
                request.timeout = 120;
                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException("Provider request failed (HTTP " + request.responseCode + "). Outcome uncertain; inspect provider history before reconciliation.");
                return speech ? request.downloadHandler.data : Encoding.UTF8.GetBytes(FPElevenLabsEditorUtility.ExtractOpenAIOutputText(request.downloadHandler.text));
            }
        }

        [Serializable] private sealed class RequestIdentity
        { public int version = 1; public string operation, text, model, voice, source, target; }
        [Serializable] private sealed class Ledger
        { public int version = 1; public List<Entry> entries = new List<Entry>(); }
        [Serializable] private sealed class Entry
        { public string requestHash, manifestHash, state, sha256, utc; public int characters; }
        [Serializable] private sealed class SpeechPayload { public string text, model_id; }
        [Serializable] private sealed class TranslationPayload
        { public string model, instructions, input; public int max_output_tokens = 1024; }
    }
}
