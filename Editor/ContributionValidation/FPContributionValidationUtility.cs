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
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Collects contribution files and runs deterministic, read-only validation rules.
    /// </summary>
    internal static class FPContributionValidationUtility
    {
        private const string NamingRule = "Naming and Organization";
        private const string AssemblyRule = "Assembly Definitions";
        private const string NamespaceRule = "Namespaces";
        private const string HeaderRule = "Script Headers";
        private const string RuntimeEditorRule = "Runtime and Editor Separation";
        private const string IdentityRule = "Object Identity";
        private const string CleanlinessRule = "Repository Cleanliness";

        private static readonly Regex TypeDeclarationPattern = new Regex(
            @"\b(class|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)|\binterface\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);
        private static readonly Regex NamespacePattern = new Regex(
            @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)",
            RegexOptions.Compiled);
        private static readonly Regex InterfacePattern = new Regex(
            @"\binterface\s+(I[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        internal static FPContributionValidationReport Run(
            FPContributionValidationOptions options,
            Func<string, float, bool> cancelProgress = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var report = new FPContributionValidationReport
            {
                RootAssetPath = NormalizeAssetPath(options.RootAssetPath)
            };

            report.EligibleAssetPaths.AddRange(CollectEligibleAssetPaths(options));
            report.ScannedAssetPaths.AddRange(SelectSample(report.EligibleAssetPaths, options));
            report.Fingerprint = ComputeFingerprint(report.EligibleAssetPaths);

            var scriptContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int total = Math.Max(1, report.ScannedAssetPaths.Count);
            for (int i = 0; i < report.ScannedAssetPaths.Count; i++)
            {
                string assetPath = report.ScannedAssetPaths[i];
                if (cancelProgress != null && cancelProgress(assetPath, (float)i / total))
                {
                    report.WasCancelled = true;
                    return report;
                }

                if (assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    scriptContents[assetPath] = ReadAssetText(assetPath);
                }
            }

            if (options.CheckNaming)
            {
                RunNamingChecks(options, scriptContents, report);
            }
            if (options.CheckAssemblies)
            {
                RunAssemblyChecks(options, report);
            }
            if (options.CheckNamespaces)
            {
                RunNamespaceChecks(options, scriptContents, report);
            }
            if (options.CheckHeaders)
            {
                RunHeaderChecks(options, scriptContents.Keys, report);
            }
            if (options.CheckRuntimeEditorSeparation)
            {
                RunRuntimeEditorChecks(scriptContents, report);
            }
            if (options.CheckObjectIdentity)
            {
                RunIdentityChecks(scriptContents, report);
            }
            if (options.CheckRepositoryCleanliness)
            {
                RunCleanlinessChecks(report);
            }

            if (!string.IsNullOrWhiteSpace(options.SampleRootAssetPath))
            {
                ValidateSampleAssemblyBoundaries(options.SampleRootAssetPath, report);
            }

            cancelProgress?.Invoke("Complete", 1f);
            return report;
        }

        /// <summary>
        /// Inspects all sample scripts regardless of filters or random sampling. Moving a
        /// sample must not silently replace an inherited parent assembly with another one.
        /// </summary>
        internal static void ValidateSampleAssemblyBoundaries(string sampleAssetPath, FPContributionValidationReport report)
        {
            const string rule = "Sample Assembly Portability";
            int issuesBefore = report.Findings.Count;
            string root = Path.GetFullPath(FPScriptHeaderUtility.GetFullProjectPath(sampleAssetPath))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root))
            {
                Add(report, rule, FPContributionValidationSeverity.Failure, "The staging sample folder does not exist.", sampleAssetPath);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUtility.scriptCompilationFailed)
            {
                Add(report, rule, FPContributionValidationSeverity.Failure,
                    "Unity is importing, compiling, or has script compilation errors. Sample promotion is blocked.", sampleAssetPath,
                    0, "Complete asset import and resolve Console compiler errors, then validate the staging sample again.");
            }

            string[] definitions = Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories);
            foreach (string script in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                string directory = Path.GetDirectoryName(script);
                string boundary = null;
                while (directory != null)
                {
                    string[] boundaries = Directory.GetFiles(directory, "*.asmdef")
                        .Concat(Directory.GetFiles(directory, "*.asmref")).ToArray();
                    if (boundaries.Length > 0)
                    {
                        boundary = boundaries.Length == 1 ? boundaries[0] : null;
                        break;
                    }
                    if (string.Equals(directory, root, StringComparison.OrdinalIgnoreCase)) break;
                    directory = Path.GetDirectoryName(directory);
                }

                string assetPath = NormalizeAssetPath(sampleAssetPath) + "/" + NormalizeAssetPath(GetRelativePath(root, script));
                if (boundary == null)
                {
                    Add(report, rule, FPContributionValidationSeverity.Failure,
                        "Sample script has no unambiguous assembly boundary inside the sample; moving it can change its owning assembly.",
                        assetPath, 0, "Add a sample assembly definition referencing the required runtime assemblies, then compile the staged sample.");
                    continue;
                }
                try
                {
                    if (boundary.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase))
                    {
                        string reference = JsonUtility.FromJson<FPSampleAssemblyReference>(File.ReadAllText(boundary))?.reference;
                        boundary = definitions.FirstOrDefault(definition => SampleAssemblyReferenceMatches(reference, definition));
                        if (boundary == null) throw new InvalidDataException("Assembly reference does not resolve to an assembly definition inside the sample.");
                    }
                    if (string.IsNullOrWhiteSpace(JsonUtility.FromJson<FPAssemblyDefinitionData>(File.ReadAllText(boundary))?.name))
                        throw new InvalidDataException("Sample assembly definition has no name.");
                }
                catch (Exception exception)
                {
                    Add(report, rule, FPContributionValidationSeverity.Failure,
                        $"Sample assembly boundary could not be verified: {exception.Message}", assetPath,
                        0, "Use a sample-owned assembly definition with explicit runtime references and verify Unity compilation before promotion.");
                }
            }
            AddPassWhenNoIssues(report, rule, issuesBefore,
                "All sample scripts carry an assembly boundary; Unity reports no pending import or compilation errors. Consumer import and runtime behavior remain separate checks.");
        }

        private static bool SampleAssemblyReferenceMatches(string reference, string definition)
        {
            if (string.IsNullOrWhiteSpace(reference)) return false;
            if (!reference.StartsWith("GUID:", StringComparison.OrdinalIgnoreCase))
                return string.Equals(reference, JsonUtility.FromJson<FPAssemblyDefinitionData>(File.ReadAllText(definition))?.name, StringComparison.Ordinal);
            if (!File.Exists(definition + ".meta")) return false;
            Match guid = Regex.Match(File.ReadAllText(definition + ".meta"), @"(?m)^guid:\s*([a-fA-F0-9]+)\s*$");
            return guid.Success && string.Equals(reference.Substring(5), guid.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class FPSampleAssemblyReference
        {
            public string reference;
        }

        internal static List<string> CollectEligibleAssetPaths(FPContributionValidationOptions options)
        {
            string normalizedRoot = NormalizeAssetPath(options.RootAssetPath);
            if (string.IsNullOrWhiteSpace(normalizedRoot))
            {
                return new List<string>();
            }

            string fullRoot = FPScriptHeaderUtility.GetFullProjectPath(normalizedRoot);
            if (!Directory.Exists(fullRoot))
            {
                return new List<string>();
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Directory.GetFiles(fullRoot, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Select(path => NormalizeAssetPath(GetRelativePath(projectRoot, path)))
                .Where(path => IsIncludedPath(path, options))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static List<string> SelectSample(
            IReadOnlyList<string> eligiblePaths,
            FPContributionValidationOptions options)
        {
            var ordered = eligiblePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!options.UseRandomSample)
            {
                return ordered;
            }

            List<string> critical = ordered.Where(IsCriticalPath).ToList();
            List<string> optional = ordered.Where(path => !IsCriticalPath(path)).ToList();
            var random = new System.Random(options.RandomSeed);
            for (int i = optional.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (optional[i], optional[swapIndex]) = (optional[swapIndex], optional[i]);
            }

            int count = Mathf.Clamp(options.RandomFileCount, 0, optional.Count);
            critical.AddRange(optional.Take(count));
            return critical
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static string ComputeFingerprint(IReadOnlyList<string> assetPaths)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = NormalizeAssetPath(assetPaths[i]);
                string fullPath = FPScriptHeaderUtility.GetFullProjectPath(assetPath);
                var info = new FileInfo(fullPath);
                builder.Append(assetPath).Append('|');
                if (info.Exists)
                {
                    builder.Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks);
                }
                builder.Append('\n');
            }

            using SHA256 hash = SHA256.Create();
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                result.Append(bytes[i].ToString("x2"));
            }
            return result.ToString();
        }

        internal static string ToMarkdown(FPContributionValidationReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# FuzzPhyte Contribution Validation Report");
            builder.AppendLine();
            builder.AppendLine($"- Scope: `{report.RootAssetPath}`");
            builder.AppendLine($"- Eligible files: {report.EligibleAssetPaths.Count}");
            builder.AppendLine($"- Scanned files: {report.ScannedAssetPaths.Count}");
            builder.AppendLine($"- Passes: {report.Count(FPContributionValidationSeverity.Pass)}");
            builder.AppendLine($"- Warnings: {report.Count(FPContributionValidationSeverity.Warning)}");
            builder.AppendLine($"- Failures: {report.Count(FPContributionValidationSeverity.Failure)}");
            builder.AppendLine($"- Manual review: {report.Count(FPContributionValidationSeverity.ManualReview)}");
            builder.AppendLine();

            foreach (IGrouping<string, FPContributionValidationFinding> group in
                     report.Findings.GroupBy(finding => finding.Rule).OrderBy(group => group.Key))
            {
                builder.AppendLine($"## {group.Key}");
                builder.AppendLine();
                foreach (FPContributionValidationFinding finding in group)
                {
                    string location = string.IsNullOrEmpty(finding.AssetPath)
                        ? string.Empty
                        : $" — `{finding.AssetPath}`" + (finding.LineNumber > 0 ? $":{finding.LineNumber}" : string.Empty);
                    builder.AppendLine($"- **{finding.Severity}**: {finding.Message}{location}");
                    if (!string.IsNullOrEmpty(finding.Remediation))
                    {
                        builder.AppendLine($"  - Suggested action: {finding.Remediation}");
                    }
                }
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static void RunNamingChecks(
            FPContributionValidationOptions options,
            IReadOnlyDictionary<string, string> scripts,
            FPContributionValidationReport report)
        {
            int issuesBefore = report.Findings.Count;
            string[] forbiddenTerms = SplitTerms(options.ForbiddenTerms);

            foreach (KeyValuePair<string, string> pair in scripts)
            {
                string fileName = Path.GetFileNameWithoutExtension(pair.Key);
                MatchCollection declarations = TypeDeclarationPattern.Matches(pair.Value);
                if (declarations.Count > 0)
                {
                    bool fileNameMatches = declarations.Cast<Match>().Any(match =>
                    {
                        string typeName = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                        return string.Equals(typeName, fileName, StringComparison.Ordinal);
                    });
                    if (!fileNameMatches)
                    {
                        Add(report, NamingRule, FPContributionValidationSeverity.Warning,
                            $"No declared type matches the filename '{fileName}'.", pair.Key, 0,
                            "Rename the file or its primary type inside Unity after confirming the intended public type.");
                    }
                }

                if (options.RequireInterfacePrefix)
                {
                    foreach (Match match in InterfacePattern.Matches(pair.Value))
                    {
                        string interfaceName = match.Groups[1].Value;
                        if (!interfaceName.StartsWith("IFP", StringComparison.Ordinal))
                        {
                            Add(report, NamingRule, FPContributionValidationSeverity.Failure,
                                $"Interface '{interfaceName}' does not use the IFP prefix.", pair.Key,
                                GetLineNumber(pair.Value, match.Index), "Rename the framework interface with the IFP prefix.");
                        }
                    }
                }

                for (int i = 0; i < forbiddenTerms.Length; i++)
                {
                    string term = forbiddenTerms[i];
                    int contentIndex = pair.Value.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                    if (pair.Key.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || contentIndex >= 0)
                    {
                        Add(report, NamingRule, FPContributionValidationSeverity.Failure,
                            $"Forbidden term '{term}' was found.", pair.Key,
                            contentIndex >= 0 ? GetLineNumber(pair.Value, contentIndex) : 0,
                            "Replace legacy or project-specific terminology with the configured package terminology.");
                    }
                }
            }

            AddPassWhenNoIssues(report, NamingRule, issuesBefore, "Naming and organization checks found no issues.");
        }

        private static void RunAssemblyChecks(
            FPContributionValidationOptions options,
            FPContributionValidationReport report)
        {
            int issuesBefore = report.Findings.Count;
            List<string> assemblyPaths = report.ScannedAssetPaths
                .Where(path => path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                .ToList();

            for (int i = 0; i < assemblyPaths.Count; i++)
            {
                string path = assemblyPaths[i];
                FPAssemblyDefinitionData data;
                try
                {
                    data = JsonUtility.FromJson<FPAssemblyDefinitionData>(ReadAssetText(path));
                }
                catch (Exception exception)
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Failure,
                        $"Assembly definition JSON could not be parsed: {exception.Message}", path);
                    continue;
                }

                if (data == null || string.IsNullOrWhiteSpace(data.name))
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Failure,
                        "Assembly definition has no name.", path);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(options.AssemblyNamePrefix) &&
                    !data.name.StartsWith(options.AssemblyNamePrefix, StringComparison.Ordinal))
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Warning,
                        $"Assembly '{data.name}' does not start with '{options.AssemblyNamePrefix}'.", path);
                }

                if (!string.IsNullOrWhiteSpace(options.NamespaceRoot) &&
                    (string.IsNullOrWhiteSpace(data.rootNamespace) ||
                     !data.rootNamespace.StartsWith(options.NamespaceRoot, StringComparison.Ordinal)))
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Failure,
                        $"Root namespace must start with '{options.NamespaceRoot}'.", path);
                }

                bool isEditorPath = path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isEditorOnly = data.includePlatforms != null &&
                                    data.includePlatforms.Any(platform =>
                                        string.Equals(platform, "Editor", StringComparison.OrdinalIgnoreCase));
                if (isEditorPath && !isEditorOnly)
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Failure,
                        "An assembly under an Editor folder is not limited to the Editor platform.", path);
                }
                if (!isEditorPath && data.references != null && data.references.Any(reference =>
                        reference.IndexOf(".editor", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Failure,
                        "A non-Editor assembly references an Editor assembly.", path);
                }
                if (options.RequireAutoReferenced && !data.autoReferenced)
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Failure,
                        "Auto Referenced is disabled but is required by the current validation settings.", path);
                }
                if (options.RequireAllowUnsafeCode && !data.allowUnsafeCode)
                {
                    Add(report, AssemblyRule, FPContributionValidationSeverity.Failure,
                        "Allow Unsafe Code is disabled but is required by the current validation settings.", path);
                }
            }

            if (assemblyPaths.Count == 0)
            {
                Add(report, AssemblyRule, FPContributionValidationSeverity.Warning,
                    "No assembly definitions were included in the selected validation scope.");
            }
            else
            {
                AddPassWhenNoIssues(report, AssemblyRule, issuesBefore, $"Validated {assemblyPaths.Count} assembly definition(s).");
            }
        }

        private static void RunNamespaceChecks(
            FPContributionValidationOptions options,
            IReadOnlyDictionary<string, string> scripts,
            FPContributionValidationReport report)
        {
            int issuesBefore = report.Findings.Count;
            foreach (KeyValuePair<string, string> pair in scripts)
            {
                Match match = NamespacePattern.Match(pair.Value);
                if (!match.Success)
                {
                    Add(report, NamespaceRule, FPContributionValidationSeverity.Failure,
                        "No namespace declaration was found.", pair.Key, 0,
                        $"Place the script under a namespace beginning with {options.NamespaceRoot}.");
                    continue;
                }

                string namespaceName = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(options.NamespaceRoot) &&
                    !namespaceName.StartsWith(options.NamespaceRoot, StringComparison.Ordinal))
                {
                    Add(report, NamespaceRule, FPContributionValidationSeverity.Failure,
                        $"Namespace '{namespaceName}' does not begin with '{options.NamespaceRoot}'.", pair.Key,
                        GetLineNumber(pair.Value, match.Index));
                }

                if (options.RequireUsingsInsideNamespace)
                {
                    int firstUsing = pair.Value.IndexOf("using ", StringComparison.Ordinal);
                    if (firstUsing >= 0 && firstUsing < match.Index)
                    {
                        Add(report, NamespaceRule, FPContributionValidationSeverity.Warning,
                            "Using directives appear before the namespace declaration.", pair.Key,
                            GetLineNumber(pair.Value, firstUsing),
                            "Move using directives inside the namespace to match the FuzzPhyte source convention.");
                    }
                }
            }

            AddPassWhenNoIssues(report, NamespaceRule, issuesBefore, "Namespace checks found no issues.");
        }

        private static void RunHeaderChecks(
            FPContributionValidationOptions options,
            IEnumerable<string> scriptPaths,
            FPContributionValidationReport report)
        {
            int issuesBefore = report.Findings.Count;
            string expectedHeader = string.IsNullOrWhiteSpace(options.ExpectedHeader)
                ? FPScriptHeaderUtility.GetConfiguredHeaderText()
                : options.ExpectedHeader;

            foreach (string scriptPath in scriptPaths)
            {
                FPScriptHeaderInspection inspection = FPScriptHeaderUtility.InspectAsset(scriptPath, expectedHeader);
                if (inspection.Status == FPScriptHeaderStatus.Match)
                {
                    continue;
                }

                FPContributionValidationSeverity severity =
                    inspection.Status == FPScriptHeaderStatus.ExternalCopyright
                        ? FPContributionValidationSeverity.ManualReview
                        : FPContributionValidationSeverity.Failure;
                Add(report, HeaderRule, severity, inspection.Message, scriptPath, 1,
                    inspection.Status == FPScriptHeaderStatus.ExternalCopyright
                        ? "Review ownership before replacing this header."
                        : "Open the failed scripts in the FuzzPhyte Script Header Editor.");
            }

            AddPassWhenNoIssues(report, HeaderRule, issuesBefore, "All scanned scripts match the configured header.");
        }

        private static void RunRuntimeEditorChecks(
            IReadOnlyDictionary<string, string> scripts,
            FPContributionValidationReport report)
        {
            int issuesBefore = report.Findings.Count;
            foreach (KeyValuePair<string, string> pair in scripts)
            {
                if (pair.Key.IndexOf("/Runtime/", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int editorIndex = pair.Value.IndexOf("UnityEditor", StringComparison.Ordinal);
                if (editorIndex >= 0)
                {
                    Add(report, RuntimeEditorRule, FPContributionValidationSeverity.Failure,
                        "Runtime source references UnityEditor.", pair.Key,
                        GetLineNumber(pair.Value, editorIndex),
                        "Move editor behavior into an Editor folder and editor-only assembly.");
                }
            }

            AddPassWhenNoIssues(report, RuntimeEditorRule, issuesBefore, "No UnityEditor references were found in scanned Runtime scripts.");
        }

        private static void RunIdentityChecks(
            IReadOnlyDictionary<string, string> scripts,
            FPContributionValidationReport report)
        {
            int issuesBefore = report.Findings.Count;
            foreach (KeyValuePair<string, string> pair in scripts)
            {
                int identityIndex = pair.Value.IndexOf("GetInstanceID(", StringComparison.Ordinal);
                if (identityIndex >= 0)
                {
                    Add(report, IdentityRule, FPContributionValidationSeverity.Failure,
                        "GetInstanceID() is not permitted as a FuzzPhyte identity mechanism.", pair.Key,
                        GetLineNumber(pair.Value, identityIndex),
                        "Use the established EntityId or stable domain-identity architecture.");
                }
            }

            AddPassWhenNoIssues(report, IdentityRule, issuesBefore, "No GetInstanceID() usage was found in scanned scripts.");
        }

        private static void RunCleanlinessChecks(FPContributionValidationReport report)
        {
            int issuesBefore = report.Findings.Count;
            for (int i = 0; i < report.EligibleAssetPaths.Count; i++)
            {
                string path = report.EligibleAssetPaths[i];
                string extension = Path.GetExtension(path);
                string fileName = Path.GetFileName(path);
                bool generatedProjectFile =
                    string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".user", StringComparison.OrdinalIgnoreCase);
                bool generatedFolder = path.IndexOf("/.vs/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("/Library/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("/Temp/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("/Logs/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       path.IndexOf("/UserSettings/", StringComparison.OrdinalIgnoreCase) >= 0;
                bool temporaryFile = string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                                     fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
                if (generatedProjectFile || generatedFolder || temporaryFile)
                {
                    Add(report, CleanlinessRule, FPContributionValidationSeverity.Failure,
                        "Generated or temporary repository content was found.", path, 0,
                        "Remove unrelated generated content from the contribution diff.");
                }
            }

            AddPassWhenNoIssues(report, CleanlinessRule, issuesBefore, "No generated project junk was found in the selected scope.");
        }

        private static string ReadAssetText(string assetPath)
        {
            string fullPath = FPScriptHeaderUtility.GetFullProjectPath(assetPath);
            return File.Exists(fullPath) ? FPScriptHeaderUtility.ReadText(fullPath, out _) : string.Empty;
        }

        private static bool IsIncludedPath(string path, FPContributionValidationOptions options)
        {
            if (!options.IncludeRuntime && path.IndexOf("/Runtime/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
            if (!options.IncludeEditor && path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
            if (!options.IncludeTests && path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }
            if (!options.IncludeSamples &&
                (path.IndexOf("/Samples~/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("/Samples/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 path.IndexOf("/Sample/", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return false;
            }
            return true;
        }

        private static bool IsCriticalPath(string path)
        {
            string fileName = Path.GetFileName(path);
            string extension = Path.GetExtension(path);
            return string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileName, ".DS_Store", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(rootPath));
            Uri fileUri = new Uri(fullPath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string[] SplitTerms(string terms)
        {
            return string.IsNullOrWhiteSpace(terms)
                ? Array.Empty<string>()
                : terms.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(term => term.Trim())
                    .Where(term => term.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
        }

        private static int GetLineNumber(string text, int index)
        {
            int line = 1;
            int safeIndex = Mathf.Clamp(index, 0, text.Length);
            for (int i = 0; i < safeIndex; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }
            return line;
        }

        private static void AddPassWhenNoIssues(
            FPContributionValidationReport report,
            string rule,
            int issuesBefore,
            string message)
        {
            if (report.Findings.Count == issuesBefore)
            {
                Add(report, rule, FPContributionValidationSeverity.Pass, message);
            }
        }

        private static void Add(
            FPContributionValidationReport report,
            string rule,
            FPContributionValidationSeverity severity,
            string message,
            string assetPath = "",
            int lineNumber = 0,
            string remediation = "")
        {
            report.Findings.Add(new FPContributionValidationFinding(
                rule, severity, message, assetPath, lineNumber, remediation));
        }

        [Serializable]
        private sealed class FPAssemblyDefinitionData
        {
            public string name;
            public string rootNamespace;
            public string[] references;
            public string[] includePlatforms;
            public bool allowUnsafeCode;
            public bool autoReferenced;
        }
    }
}
