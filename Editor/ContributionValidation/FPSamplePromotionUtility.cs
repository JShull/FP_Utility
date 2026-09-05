// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [Serializable]
    internal sealed class FPSamplePromotionRecord
    {
        public string SourceAssetPath;
        public string DestinationFullPath;
        public string ManifestFullPath;
        public string ManifestBackupPath;
        public string DisplayName;

        public bool IsValid =>
            !string.IsNullOrEmpty(SourceAssetPath) &&
            !string.IsNullOrEmpty(DestinationFullPath) &&
            !string.IsNullOrEmpty(ManifestBackupPath);
    }

    internal sealed class FPSamplePromotionRequest
    {
        public string SourceAssetPath;
        public string PackageRootAssetPath;
        public string DestinationFolderName;
        public string DisplayName;
        public string Description;
        public string ExpectedValidationFingerprint;
        public FPContributionValidationOptions ValidationOptions;
    }

    internal readonly struct FPSamplePromotionResult
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly FPSamplePromotionRecord Record;

        public FPSamplePromotionResult(bool success, string message, FPSamplePromotionRecord record = null)
        {
            Success = success;
            Message = message;
            Record = record;
        }
    }

    /// <summary>
    /// Performs a guarded move from an imported staging folder into package Samples~ content.
    /// </summary>
    internal static class FPSamplePromotionUtility
    {
        internal static FPSamplePromotionResult Promote(FPSamplePromotionRequest request)
        {
            string error = ValidateRequest(request);
            if (!string.IsNullOrEmpty(error))
            {
                return new FPSamplePromotionResult(false, error);
            }

            string sourceAssetPath = NormalizeAssetPath(request.SourceAssetPath);
            string packageRootAssetPath = NormalizeAssetPath(request.PackageRootAssetPath);
            string sourceFullPath = FPScriptHeaderUtility.GetFullProjectPath(sourceAssetPath);
            string destinationAssetPath = $"{packageRootAssetPath}/Samples~/{request.DestinationFolderName}";
            string destinationFullPath = FPScriptHeaderUtility.GetFullProjectPath(destinationAssetPath);
            string manifestFullPath = FPScriptHeaderUtility.GetFullProjectPath($"{packageRootAssetPath}/package.json");

            if (destinationFullPath.StartsWith(
                    sourceFullPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new FPSamplePromotionResult(false, "The sample destination cannot be inside the staging folder.");
            }

            if (Directory.Exists(destinationFullPath))
            {
                return new FPSamplePromotionResult(false, $"The destination already exists: {destinationAssetPath}");
            }

            if (!ValidationFingerprintMatches(request))
            {
                return new FPSamplePromotionResult(
                    false,
                    "The staging folder changed after validation. Run the selected checks again before promotion.");
            }

            var preflight = new FPContributionValidationReport { RootAssetPath = sourceAssetPath };
            FPContributionValidationUtility.ValidateSampleAssemblyBoundaries(sourceAssetPath, preflight);
            if (preflight.HasFailures)
            {
                return new FPSamplePromotionResult(false, FPContributionValidationUtility.ToMarkdown(preflight));
            }

            string originalManifest;
            Encoding manifestEncoding;
            try
            {
                originalManifest = FPScriptHeaderUtility.ReadText(manifestFullPath, out manifestEncoding);
            }
            catch (Exception exception)
            {
                return new FPSamplePromotionResult(false, $"package.json could not be read: {exception.Message}");
            }

            string samplePath = $"Samples~/{request.DestinationFolderName}";
            string updatedManifest;
            try
            {
                updatedManifest = UpsertSampleEntry(
                    originalManifest,
                    new FPSampleManifestEntry(request.DisplayName.Trim(), request.Description.Trim(), samplePath));
            }
            catch (Exception exception)
            {
                return new FPSamplePromotionResult(false, $"package.json could not be updated safely: {exception.Message}");
            }

            string backupFolder = CreateBackupFolder();
            string manifestBackupPath = Path.Combine(backupFolder, "package.json");
            File.Copy(manifestFullPath, manifestBackupPath, true);

            var record = new FPSamplePromotionRecord
            {
                SourceAssetPath = sourceAssetPath,
                DestinationFullPath = destinationFullPath,
                ManifestFullPath = manifestFullPath,
                ManifestBackupPath = manifestBackupPath,
                DisplayName = request.DisplayName.Trim()
            };

            AssetDatabase.StartAssetEditing();
            try
            {
                string samplesRoot = Path.GetDirectoryName(destinationFullPath);
                if (!Directory.Exists(samplesRoot))
                {
                    Directory.CreateDirectory(samplesRoot);
                }

                MoveDirectoryAndMetadata(sourceFullPath, destinationFullPath);
                File.WriteAllText(manifestFullPath, updatedManifest, manifestEncoding);

                if (!Directory.Exists(destinationFullPath))
                {
                    throw new IOException("The destination folder was not created.");
                }

                List<FPSampleManifestEntry> entries = ReadSampleEntries(updatedManifest);
                if (!entries.Any(entry => string.Equals(entry.path, samplePath, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException("The new sample entry could not be verified in package.json.");
                }
            }
            catch (Exception exception)
            {
                TryRestorePromotion(record);
                return new FPSamplePromotionResult(false, $"Sample promotion failed and was rolled back: {exception.Message}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            return new FPSamplePromotionResult(
                true,
                $"Promoted '{request.DisplayName}' to {samplePath} and updated package.json.",
                record);
        }

        internal static FPSamplePromotionResult Rollback(FPSamplePromotionRecord record)
        {
            if (record == null || !record.IsValid)
            {
                return new FPSamplePromotionResult(false, "There is no valid sample-promotion record to roll back.");
            }

            string sourceFullPath = FPScriptHeaderUtility.GetFullProjectPath(record.SourceAssetPath);
            if (!Directory.Exists(record.DestinationFullPath))
            {
                return new FPSamplePromotionResult(false, "The promoted sample folder no longer exists.");
            }
            if (Directory.Exists(sourceFullPath))
            {
                return new FPSamplePromotionResult(false, "The original staging path is already occupied.");
            }
            if (!File.Exists(record.ManifestBackupPath))
            {
                return new FPSamplePromotionResult(false, "The manifest backup is missing.");
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                MoveDirectoryAndMetadata(record.DestinationFullPath, sourceFullPath);
                File.Copy(record.ManifestBackupPath, record.ManifestFullPath, true);
            }
            catch (Exception exception)
            {
                return new FPSamplePromotionResult(false, $"Rollback could not complete: {exception.Message}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            return new FPSamplePromotionResult(true, $"Restored the staging sample to {record.SourceAssetPath}.");
        }

        internal static string UpsertSampleEntry(string manifestJson, FPSampleManifestEntry entry)
        {
            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                throw new InvalidDataException("The manifest is empty.");
            }
            if (entry == null || string.IsNullOrWhiteSpace(entry.displayName) || string.IsNullOrWhiteSpace(entry.path))
            {
                throw new ArgumentException("A sample display name and path are required.", nameof(entry));
            }

            int samplesProperty = FindJsonProperty(manifestJson, "samples");
            var entries = new List<FPSampleManifestEntry>();
            int arrayStart = -1;
            int arrayEnd = -1;

            if (samplesProperty >= 0)
            {
                int colon = manifestJson.IndexOf(':', samplesProperty);
                arrayStart = colon >= 0 ? manifestJson.IndexOf('[', colon) : -1;
                if (arrayStart < 0)
                {
                    throw new InvalidDataException("The samples property is not a JSON array.");
                }

                arrayEnd = FindMatchingBracket(manifestJson, arrayStart, '[', ']');
                if (arrayEnd < 0)
                {
                    throw new InvalidDataException("The samples array is not terminated.");
                }

                entries.AddRange(ParseSampleArray(manifestJson.Substring(arrayStart, arrayEnd - arrayStart + 1)));
            }

            int existingIndex = entries.FindIndex(existing =>
                string.Equals(existing.path, entry.path, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                entries[existingIndex] = entry;
            }
            else
            {
                entries.Add(entry);
            }

            string newArray = SerializeSampleArray(entries);
            if (samplesProperty >= 0)
            {
                return manifestJson.Substring(0, arrayStart) + newArray + manifestJson.Substring(arrayEnd + 1);
            }

            int rootEnd = FindLastRootBrace(manifestJson);
            if (rootEnd < 0)
            {
                throw new InvalidDataException("The package manifest has no closing root object.");
            }

            string beforeClose = manifestJson.Substring(0, rootEnd).TrimEnd();
            bool needsComma = !beforeClose.EndsWith("{", StringComparison.Ordinal);
            string indentedArray = newArray.Replace("\n", "\n  ");
            return beforeClose + (needsComma ? "," : string.Empty) +
                   "\n  \"samples\": " + indentedArray + "\n" +
                   manifestJson.Substring(rootEnd);
        }

        internal static List<FPSampleManifestEntry> ReadSampleEntries(string manifestJson)
        {
            int samplesProperty = FindJsonProperty(manifestJson, "samples");
            if (samplesProperty < 0)
            {
                return new List<FPSampleManifestEntry>();
            }

            int colon = manifestJson.IndexOf(':', samplesProperty);
            int arrayStart = colon >= 0 ? manifestJson.IndexOf('[', colon) : -1;
            int arrayEnd = arrayStart >= 0 ? FindMatchingBracket(manifestJson, arrayStart, '[', ']') : -1;
            if (arrayStart < 0 || arrayEnd < 0)
            {
                throw new InvalidDataException("The samples array is malformed.");
            }

            return ParseSampleArray(manifestJson.Substring(arrayStart, arrayEnd - arrayStart + 1));
        }

        private static string ValidateRequest(FPSamplePromotionRequest request)
        {
            if (request == null)
            {
                return "The sample-promotion request is missing.";
            }
            if (string.IsNullOrWhiteSpace(request.SourceAssetPath) ||
                !Directory.Exists(FPScriptHeaderUtility.GetFullProjectPath(request.SourceAssetPath)))
            {
                return "Choose an existing staging sample folder.";
            }
            if (NormalizeAssetPath(request.SourceAssetPath).IndexOf("/Samples~/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "The staging sample is already under a Samples~ folder.";
            }
            if (string.IsNullOrWhiteSpace(request.PackageRootAssetPath) ||
                !File.Exists(FPScriptHeaderUtility.GetFullProjectPath(
                    $"{NormalizeAssetPath(request.PackageRootAssetPath)}/package.json")))
            {
                return "Choose a package root containing package.json.";
            }
            if (!IsValidFolderName(request.DestinationFolderName))
            {
                return "Enter a destination folder name without path separators or invalid filename characters.";
            }
            if (string.IsNullOrWhiteSpace(request.DisplayName))
            {
                return "Enter the Package Manager sample display name.";
            }
            string normalizedSource = NormalizeAssetPath(request.SourceAssetPath) + "/";
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path) &&
                    NormalizeAssetPath(scene.path).StartsWith(normalizedSource, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Save or close the dirty sample scene before promotion: {scene.path}";
                }
            }
            if (request.ValidationOptions == null || string.IsNullOrWhiteSpace(request.ExpectedValidationFingerprint))
            {
                return "Run validation on the staging folder before promotion.";
            }
            return string.Empty;
        }

        private static bool ValidationFingerprintMatches(FPSamplePromotionRequest request)
        {
            FPContributionValidationOptions validationOptions = request.ValidationOptions;
            validationOptions.RootAssetPath = NormalizeAssetPath(request.SourceAssetPath);
            List<string> currentPaths = FPContributionValidationUtility.CollectEligibleAssetPaths(validationOptions);
            string currentFingerprint = FPContributionValidationUtility.ComputeFingerprint(currentPaths);
            return string.Equals(
                currentFingerprint,
                request.ExpectedValidationFingerprint,
                StringComparison.Ordinal);
        }

        private static void TryRestorePromotion(FPSamplePromotionRecord record)
        {
            string sourceFullPath = FPScriptHeaderUtility.GetFullProjectPath(record.SourceAssetPath);
            try
            {
                if (Directory.Exists(record.DestinationFullPath) && !Directory.Exists(sourceFullPath))
                {
                    MoveDirectoryAndMetadata(record.DestinationFullPath, sourceFullPath);
                }
                if (File.Exists(record.ManifestBackupPath))
                {
                    File.Copy(record.ManifestBackupPath, record.ManifestFullPath, true);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"FP Sample Promotion rollback requires manual attention: {exception}");
            }
        }

        private static void MoveDirectoryAndMetadata(string sourceFullPath, string destinationFullPath)
        {
            string destinationParent = Path.GetDirectoryName(destinationFullPath);
            if (!string.IsNullOrEmpty(destinationParent) && !Directory.Exists(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }

            FileUtil.MoveFileOrDirectory(sourceFullPath, destinationFullPath);
            string sourceMeta = sourceFullPath + ".meta";
            if (File.Exists(sourceMeta))
            {
                FileUtil.MoveFileOrDirectory(sourceMeta, destinationFullPath + ".meta");
            }
        }

        private static List<FPSampleManifestEntry> ParseSampleArray(string arrayJson)
        {
            FPSampleArrayWrapper wrapper = JsonUtility.FromJson<FPSampleArrayWrapper>($"{{\"items\":{arrayJson}}}");
            return wrapper?.items ?? new List<FPSampleManifestEntry>();
        }

        private static string SerializeSampleArray(List<FPSampleManifestEntry> entries)
        {
            var wrapper = new FPSampleArrayWrapper { items = entries };
            string wrapperJson = JsonUtility.ToJson(wrapper, true);
            int property = FindJsonProperty(wrapperJson, "items");
            int colon = wrapperJson.IndexOf(':', property);
            int start = wrapperJson.IndexOf('[', colon);
            int end = FindMatchingBracket(wrapperJson, start, '[', ']');
            return wrapperJson.Substring(start, end - start + 1);
        }

        private static int FindJsonProperty(string json, string propertyName)
        {
            string token = $"\"{propertyName}\"";
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i <= json.Length - token.Length; i++)
            {
                char character = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (character == '"')
                {
                    if (string.Compare(json, i, token, 0, token.Length, StringComparison.Ordinal) == 0)
                    {
                        return i;
                    }
                    inString = true;
                }
            }
            return -1;
        }

        private static int FindMatchingBracket(string text, int start, char open, char close)
        {
            if (start < 0 || start >= text.Length || text[start] != open)
            {
                return -1;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < text.Length; i++)
            {
                char character = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == open)
                {
                    depth++;
                }
                else if (character == close && --depth == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindLastRootBrace(string json)
        {
            bool inString = false;
            bool escaped = false;
            for (int i = json.Length - 1; i >= 0; i--)
            {
                char character = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                }
                else if (character == '}')
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool IsValidFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName) ||
                folderName.IndexOf('/') >= 0 || folderName.IndexOf('\\') >= 0)
            {
                return false;
            }

            return folderName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        private static string CreateBackupFolder()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string folder = Path.Combine(
                projectRoot,
                "Library",
                "FP_Utility",
                "ContributionValidationBackups",
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        [Serializable]
        private sealed class FPSampleArrayWrapper
        {
            public List<FPSampleManifestEntry> items = new List<FPSampleManifestEntry>();
        }
    }

    [Serializable]
    internal sealed class FPSampleManifestEntry
    {
        public string displayName;
        public string description;
        public string path;

        public FPSampleManifestEntry(string displayName, string description, string path)
        {
            this.displayName = displayName;
            this.description = description;
            this.path = path;
        }
    }
}
