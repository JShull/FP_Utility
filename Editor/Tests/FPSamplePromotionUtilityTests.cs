// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.IO;
    using System.Linq;
    using NUnit.Framework;

    public class FPSamplePromotionUtilityTests
    {
        [Test]
        public void UpsertSampleEntry_AppendsAndPreservesExistingManifestContent()
        {
            const string manifest =
                "{\n" +
                "  \"name\": \"com.fuzzphyte.utility\",\n" +
                "  \"dependencies\": { \"com.unity.timeline\": \"1.0.0\" },\n" +
                "  \"samples\": [\n" +
                "    { \"displayName\": \"Existing\", \"description\": \"Existing sample\", \"path\": \"Samples~/Existing\" }\n" +
                "  ]\n" +
                "}";

            string result = FPSamplePromotionUtility.UpsertSampleEntry(
                manifest,
                new FPSampleManifestEntry("New Sample", "New description", "Samples~/NewSample"));

            Assert.That(result, Does.Contain("\"name\": \"com.fuzzphyte.utility\""));
            Assert.That(result, Does.Contain("\"com.unity.timeline\": \"1.0.0\""));
            Assert.That(FPSamplePromotionUtility.ReadSampleEntries(result).Select(entry => entry.path),
                Is.EquivalentTo(new[] { "Samples~/Existing", "Samples~/NewSample" }));
        }

        [Test]
        public void UpsertSampleEntry_NoSamplesProperty_AddsArray()
        {
            const string manifest = "{\n  \"name\": \"com.fuzzphyte.utility\"\n}";

            string result = FPSamplePromotionUtility.UpsertSampleEntry(
                manifest,
                new FPSampleManifestEntry("Sample", "Description", "Samples~/Sample"));

            Assert.That(FPSamplePromotionUtility.ReadSampleEntries(result).Single().displayName, Is.EqualTo("Sample"));
            Assert.That(result, Does.Contain("\"name\": \"com.fuzzphyte.utility\""));
        }

        [Test]
        public void UpsertSampleEntry_ExistingPath_UpdatesWithoutDuplicate()
        {
            const string manifest =
                "{\"samples\":[{\"displayName\":\"Old\",\"description\":\"Old\",\"path\":\"Samples~/Shared\"}]}";

            string result = FPSamplePromotionUtility.UpsertSampleEntry(
                manifest,
                new FPSampleManifestEntry("Updated", "Updated description", "Samples~/Shared"));

            var entries = FPSamplePromotionUtility.ReadSampleEntries(result);
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].displayName, Is.EqualTo("Updated"));
            Assert.That(entries[0].description, Is.EqualTo("Updated description"));
        }

        [Test]
        public void UpsertSampleEntry_MalformedSamplesArray_Throws()
        {
            const string manifest = "{\"samples\": {\"path\":\"Samples~/Wrong\"}}";

            Assert.Throws<InvalidDataException>(() =>
                FPSamplePromotionUtility.UpsertSampleEntry(
                    manifest,
                    new FPSampleManifestEntry("Sample", "Description", "Samples~/Sample")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PromoteAndRollback_MoveFolderAndRestoreManifest(bool unsafeScript)
        {
            string rootAssetPath = $"Temp/FPSamplePromotionTests_{System.Guid.NewGuid():N}";
            string rootFullPath = FPScriptHeaderUtility.GetFullProjectPath(rootAssetPath);
            string packageAssetPath = $"{rootAssetPath}/Package";
            string sourceAssetPath = $"{rootAssetPath}/StagingSample";
            string packageFullPath = FPScriptHeaderUtility.GetFullProjectPath(packageAssetPath);
            string sourceFullPath = FPScriptHeaderUtility.GetFullProjectPath(sourceAssetPath);
            const string originalManifest = "{\n  \"name\": \"com.fuzzphyte.test\"\n}";
            FPSamplePromotionRecord record = null;

            try
            {
                Directory.CreateDirectory(packageFullPath);
                Directory.CreateDirectory(sourceFullPath);
                File.WriteAllText(Path.Combine(packageFullPath, "package.json"), originalManifest);
                File.WriteAllText(Path.Combine(sourceFullPath, "Sample.txt"), "sample");
                if (unsafeScript) File.WriteAllText(Path.Combine(sourceFullPath, "Example.cs"), "class Example {}");

                FPContributionValidationOptions options = DisabledOptions(sourceAssetPath);
                FPContributionValidationReport report = FPContributionValidationUtility.Run(options);
                var request = new FPSamplePromotionRequest
                {
                    SourceAssetPath = sourceAssetPath,
                    PackageRootAssetPath = packageAssetPath,
                    DestinationFolderName = "PromotedSample",
                    DisplayName = "Promoted Sample",
                    Description = "Promotion integration test",
                    ExpectedValidationFingerprint = report.Fingerprint,
                    ValidationOptions = options
                };

                FPSamplePromotionResult promotion = FPSamplePromotionUtility.Promote(request);
                record = promotion.Record;

                if (unsafeScript)
                {
                    Assert.That(promotion.Success, Is.False);
                    Assert.That(promotion.Message, Does.Contain("owning assembly"));
                    Assert.That(record, Is.Null);
                    Assert.That(Directory.Exists(sourceFullPath), Is.True);
                    Assert.That(Directory.Exists(Path.Combine(packageFullPath, "Samples~")), Is.False);
                    Assert.That(File.ReadAllText(Path.Combine(packageFullPath, "package.json")), Is.EqualTo(originalManifest));
                    return;
                }

                Assert.That(promotion.Success, Is.True, promotion.Message);
                Assert.That(Directory.Exists(sourceFullPath), Is.False);
                Assert.That(Directory.Exists(Path.Combine(packageFullPath, "Samples~", "PromotedSample")), Is.True);
                Assert.That(FPSamplePromotionUtility.ReadSampleEntries(
                    File.ReadAllText(Path.Combine(packageFullPath, "package.json"))).Single().path,
                    Is.EqualTo("Samples~/PromotedSample"));

                FPSamplePromotionResult rollback = FPSamplePromotionUtility.Rollback(record);

                Assert.That(rollback.Success, Is.True, rollback.Message);
                Assert.That(Directory.Exists(sourceFullPath), Is.True);
                Assert.That(Directory.Exists(Path.Combine(packageFullPath, "Samples~", "PromotedSample")), Is.False);
                Assert.That(File.ReadAllText(Path.Combine(packageFullPath, "package.json")), Is.EqualTo(originalManifest));
            }
            finally
            {
                if (Directory.Exists(rootFullPath))
                {
                    Directory.Delete(rootFullPath, true);
                }
                if (record != null && File.Exists(record.ManifestBackupPath))
                {
                    string backupFolder = Path.GetDirectoryName(record.ManifestBackupPath);
                    Directory.Delete(backupFolder, true);
                }
            }
        }

        private static FPContributionValidationOptions DisabledOptions(string rootAssetPath)
        {
            return new FPContributionValidationOptions
            {
                RootAssetPath = rootAssetPath,
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
