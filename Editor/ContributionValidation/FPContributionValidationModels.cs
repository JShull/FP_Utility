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

    internal enum FPContributionValidationSeverity
    {
        Pass,
        Warning,
        Failure,
        ManualReview
    }

    [Serializable]
    internal sealed class FPContributionValidationFinding
    {
        public string Rule;
        public FPContributionValidationSeverity Severity;
        public string Message;
        public string AssetPath;
        public int LineNumber;
        public string Remediation;

        public FPContributionValidationFinding(
            string rule,
            FPContributionValidationSeverity severity,
            string message,
            string assetPath = "",
            int lineNumber = 0,
            string remediation = "")
        {
            Rule = rule;
            Severity = severity;
            Message = message;
            AssetPath = assetPath ?? string.Empty;
            LineNumber = lineNumber;
            Remediation = remediation ?? string.Empty;
        }
    }

    [Serializable]
    internal sealed class FPContributionValidationOptions
    {
        public string RootAssetPath;
        public string SampleRootAssetPath;
        public bool IncludeRuntime = true;
        public bool IncludeEditor = true;
        public bool IncludeTests = true;
        public bool IncludeSamples = true;
        public bool UseRandomSample;
        public int RandomFileCount = 25;
        public int RandomSeed = 12345;

        public bool CheckNaming = true;
        public bool CheckAssemblies = true;
        public bool CheckNamespaces = true;
        public bool CheckHeaders = true;
        public bool CheckRuntimeEditorSeparation = true;
        public bool CheckObjectIdentity = true;
        public bool CheckRepositoryCleanliness = true;

        public string NamespaceRoot = "FuzzPhyte";
        public string AssemblyNamePrefix = "com.fuzzphyte.";
        public string ForbiddenTerms = string.Empty;
        public bool RequireInterfacePrefix = true;
        public bool RequireUsingsInsideNamespace = true;
        public bool RequireAutoReferenced;
        public bool RequireAllowUnsafeCode;
        public string ExpectedHeader;
    }

    internal sealed class FPContributionValidationReport
    {
        public readonly List<string> EligibleAssetPaths = new List<string>();
        public readonly List<string> ScannedAssetPaths = new List<string>();
        public readonly List<FPContributionValidationFinding> Findings = new List<FPContributionValidationFinding>();

        public string RootAssetPath;
        public string Fingerprint;
        public bool WasCancelled;

        public int Count(FPContributionValidationSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < Findings.Count; i++)
            {
                if (Findings[i].Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }

        public bool HasFailures => Count(FPContributionValidationSeverity.Failure) > 0;
    }
}
