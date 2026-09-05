// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    public class FPContributionValidationUtilityTests
    {
        private string temporaryAssetPath;
        private string temporaryFullPath;

        [SetUp]
        public void SetUp()
        {
            temporaryAssetPath = $"Temp/FPContributionValidationTests_{Guid.NewGuid():N}";
            temporaryFullPath = FPScriptHeaderUtility.GetFullProjectPath(temporaryAssetPath);
            Directory.CreateDirectory(temporaryFullPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryFullPath))
            {
                Directory.Delete(temporaryFullPath, true);
            }
        }

        [Test]
        public void SelectSample_SameSeedProducesSamePathsAndKeepsCriticalFiles()
        {
            var paths = new List<string>
            {
                "Assets/Feature/A.cs",
                "Assets/Feature/B.cs",
                "Assets/Feature/C.prefab",
                "Assets/Feature/feature.asmdef",
                "Assets/Feature/package.json"
            };
            var options = new FPContributionValidationOptions
            {
                UseRandomSample = true,
                RandomFileCount = 1,
                RandomSeed = 42
            };

            List<string> first = FPContributionValidationUtility.SelectSample(paths, options);
            List<string> second = FPContributionValidationUtility.SelectSample(paths, options);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("Assets/Feature/feature.asmdef"));
            Assert.That(first, Does.Contain("Assets/Feature/package.json"));
            Assert.That(first.Count, Is.EqualTo(3));
        }

        [Test]
        public void Run_NamingMismatchAndRuntimeEditorLeak_ReturnsEvidence()
        {
            string runtimeFolder = Path.Combine(temporaryFullPath, "Runtime");
            Directory.CreateDirectory(runtimeFolder);
            File.WriteAllText(
                Path.Combine(runtimeFolder, "WrongFileName.cs"),
                "// Header\nnamespace FuzzPhyte.Feature { using UnityEditor; public class CorrectTypeName {} }");

            var options = DisabledOptions();
            options.RootAssetPath = temporaryAssetPath;
            options.CheckNaming = true;
            options.CheckRuntimeEditorSeparation = true;

            FPContributionValidationReport report = FPContributionValidationUtility.Run(options);

            Assert.That(report.Findings.Any(finding =>
                finding.Rule == "Naming and Organization" &&
                finding.Severity == FPContributionValidationSeverity.Warning), Is.True);
            Assert.That(report.Findings.Any(finding =>
                finding.Rule == "Runtime and Editor Separation" &&
                finding.Severity == FPContributionValidationSeverity.Failure &&
                finding.LineNumber > 0), Is.True);
        }

        [Test]
        public void Run_HeaderMatchProducesPassAndFingerprintChangesWithFile()
        {
            const string header = "// Header";
            string scriptPath = Path.Combine(temporaryFullPath, "Feature.cs");
            File.WriteAllText(scriptPath, header + "\n\nnamespace FuzzPhyte.Feature { public class Feature {} }");

            var options = DisabledOptions();
            options.RootAssetPath = temporaryAssetPath;
            options.CheckHeaders = true;
            options.ExpectedHeader = header;

            FPContributionValidationReport first = FPContributionValidationUtility.Run(options);
            File.AppendAllText(scriptPath, "\n// Changed");
            File.SetLastWriteTimeUtc(scriptPath, DateTime.UtcNow.AddSeconds(1));
            FPContributionValidationReport second = FPContributionValidationUtility.Run(options);

            Assert.That(first.Findings.Any(finding =>
                finding.Rule == "Script Headers" &&
                finding.Severity == FPContributionValidationSeverity.Pass), Is.True);
            Assert.That(second.Fingerprint, Is.Not.EqualTo(first.Fingerprint));
        }

        [Test]
        public void Run_AssemblyDefinitionChecksConfiguredValues()
        {
            File.WriteAllText(
                Path.Combine(temporaryFullPath, "feature.asmdef"),
                "{\"name\":\"student.feature\",\"rootNamespace\":\"Student.Feature\",\"references\":[]," +
                "\"includePlatforms\":[],\"allowUnsafeCode\":false,\"autoReferenced\":false}");

            var options = DisabledOptions();
            options.RootAssetPath = temporaryAssetPath;
            options.CheckAssemblies = true;
            options.NamespaceRoot = "FuzzPhyte";
            options.AssemblyNamePrefix = "com.fuzzphyte.";
            options.RequireAutoReferenced = true;
            options.RequireAllowUnsafeCode = true;

            FPContributionValidationReport report = FPContributionValidationUtility.Run(options);

            Assert.That(report.Count(FPContributionValidationSeverity.Failure), Is.GreaterThanOrEqualTo(3));
            Assert.That(report.Count(FPContributionValidationSeverity.Warning), Is.GreaterThanOrEqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Run_SampleAssemblyBoundaryDoesNotInheritParentOrSibling(bool localDefinition)
        {
            string sample = Path.Combine(temporaryFullPath, "Sample");
            string runtime = Path.Combine(temporaryFullPath, "Runtime");
            Directory.CreateDirectory(sample);
            Directory.CreateDirectory(runtime);
            const string definition = "{\"name\":\"com.fuzzphyte.sample\"}";
            File.WriteAllText(Path.Combine(temporaryFullPath, "parent.asmdef"), definition);
            File.WriteAllText(Path.Combine(runtime, "runtime.asmdef"), definition);
            File.WriteAllText(Path.Combine(sample, "Example.cs"), "class Example {}");
            if (localDefinition) File.WriteAllText(Path.Combine(sample, "sample.asmdef"), definition);
            var options = DisabledOptions();
            options.RootAssetPath = temporaryAssetPath;
            options.SampleRootAssetPath = temporaryAssetPath + "/Sample";
            options.IncludeSamples = false;
            options.UseRandomSample = true;
            options.RandomFileCount = 0;

            FPContributionValidationReport report = FPContributionValidationUtility.Run(options);

            Assert.That(report.HasFailures, Is.EqualTo(!localDefinition));
            if (!localDefinition)
                Assert.That(report.Findings.Any(f => f.AssetPath.EndsWith("Example.cs") && f.Message.Contains("owning assembly")), Is.True);
        }

        [TestCase("com.fuzzphyte.sample", false)]
        [TestCase("com.fuzzphyte.external", true)]
        public void Run_SampleAssemblyReferenceMustResolveInsideSample(string reference, bool fails)
        {
            string shared = Path.Combine(temporaryFullPath, "Shared");
            string linked = Path.Combine(temporaryFullPath, "Linked");
            Directory.CreateDirectory(shared);
            Directory.CreateDirectory(linked);
            File.WriteAllText(Path.Combine(shared, "sample.asmdef"), "{\"name\":\"com.fuzzphyte.sample\"}");
            File.WriteAllText(Path.Combine(linked, "sample.asmref"), "{\"reference\":\"" + reference + "\"}");
            File.WriteAllText(Path.Combine(linked, "Example.cs"), "class Example {}");
            var options = DisabledOptions();
            options.RootAssetPath = temporaryAssetPath;
            options.SampleRootAssetPath = temporaryAssetPath;

            Assert.That(FPContributionValidationUtility.Run(options).HasFailures, Is.EqualTo(fails));
        }

        private static FPContributionValidationOptions DisabledOptions()
        {
            return new FPContributionValidationOptions
            {
                IncludeRuntime = true,
                IncludeEditor = true,
                IncludeTests = true,
                IncludeSamples = true,
                CheckNaming = false,
                CheckAssemblies = false,
                CheckNamespaces = false,
                CheckHeaders = false,
                CheckRuntimeEditorSeparation = false,
                CheckObjectIdentity = false,
                CheckRepositoryCleanliness = false
            };
        }
    }
}
