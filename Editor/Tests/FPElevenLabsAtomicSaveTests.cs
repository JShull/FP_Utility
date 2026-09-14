// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.IO;
    using NUnit.Framework;

    public class FPElevenLabsAtomicSaveTests
    {
        private string folder;
        private string destination;
        private readonly byte[] original = { 1, 2, 3 };
        private readonly byte[] replacement = { 4, 5, 6 };

        [SetUp] public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "FPAtomicSave-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            destination = Path.Combine(folder, "ledger.json");
            File.WriteAllBytes(destination, original);
        }

        [TearDown] public void TearDown()
        {
            if (File.Exists(destination)) File.SetAttributes(destination, FileAttributes.Normal);
            Directory.Delete(folder, true);
        }

        [Test] public void TransientReplacement_RetriesSameStagedBytesAndPreservesOldUntilCommit()
        {
            int attempts = 0;
            int waits = 0;
            string staged = null;
            FPElevenLabsGenerationService.WriteAtomic(destination, replacement, true, (source, target) =>
            {
                attempts++;
                if (staged == null) staged = source;
                Assert.That(source, Is.EqualTo(staged));
                Assert.That(File.ReadAllBytes(target), Is.EqualTo(original));
                Assert.That(File.ReadAllBytes(source), Is.EqualTo(replacement));
                if (attempts < 3) throw new IOException("Simulated transient replacement failure");
                File.Replace(source, target, null);
            }, delay => { waits += delay; });
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(waits, Is.EqualTo(200));
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(replacement));
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }

        [Test] public void ExhaustedReplacement_PreservesDestinationAndStagingWithDiagnostics()
        {
            int attempts = 0;
            var exception = Assert.Throws<FPElevenLabsGenerationService.AtomicSaveException>(() =>
                FPElevenLabsGenerationService.WriteAtomic(destination, replacement, true,
                    (source, target) => { attempts++; throw new IOException("Simulated sharing failure"); }, _ => { }));
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(exception.Temporary), Is.EqualTo(replacement));
            Assert.That(exception.Destination, Is.EqualTo(destination));
            Assert.That(exception.Operation, Is.EqualTo("replace"));
            string report = FPElevenLabsGenerationCli.BuildFailureJson(exception);
            StringAssert.Contains("System.IO.IOException", report);
            StringAssert.Contains("hResult", report);
            StringAssert.Contains("ExhaustedReplacement", report);
        }

        [Test] public void ReadOnlyDestination_FailsWithoutChangingAttributesOrBytes()
        {
            File.SetAttributes(destination, FileAttributes.ReadOnly);
            var exception = Assert.Throws<FPElevenLabsGenerationService.AtomicSaveException>(() =>
                FPElevenLabsGenerationService.WriteAtomic(destination, replacement));
            Assert.That(exception.Attempts, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(original));
            Assert.That(File.GetAttributes(destination) & FileAttributes.ReadOnly, Is.Not.Zero);
        }

        [Test] public void WindowsSharingLock_PreservesOldFileThenAllowsExplicitLocalRetry()
        {
            if (Path.DirectorySeparatorChar != '\\') Assert.Ignore("Windows file-sharing semantics");
            using (var reader = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<FPElevenLabsGenerationService.AtomicSaveException>(() =>
                    FPElevenLabsGenerationService.WriteAtomic(destination, replacement));
                Assert.That(File.ReadAllBytes(destination), Is.EqualTo(original));
            }
            FPElevenLabsGenerationService.WriteAtomic(destination, replacement);
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(replacement));
        }

        [Test] public void NewFileCollision_NeverReplacesExistingOutput()
        {
            var exception = Assert.Throws<FPElevenLabsGenerationService.AtomicSaveException>(() =>
                FPElevenLabsGenerationService.WriteAtomic(destination, replacement, false));
            Assert.That(exception.Operation, Is.EqualTo("move-new"));
            Assert.That(exception.Attempts, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(original));
        }

        [Test] public void StageFailure_ReportsDestinationAndOriginalException()
        {
            var exception = Assert.Throws<FPElevenLabsGenerationService.AtomicSaveException>(() =>
                FPElevenLabsGenerationService.WriteAtomic(Path.Combine(folder, "missing", "ledger.json"), replacement));
            Assert.That(exception.Operation, Is.EqualTo("stage"));
            Assert.That(exception.InnerException, Is.TypeOf<DirectoryNotFoundException>());
        }
    }
}
