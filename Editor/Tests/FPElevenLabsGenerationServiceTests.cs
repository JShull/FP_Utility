// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;

    public class FPElevenLabsGenerationServiceTests
    {
        private string root;
        private int calls;
        private FPElevenLabsGenerationService service;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "FPElevenLabsTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            calls = 0;
            service = new FPElevenLabsGenerationService(root, _ => { calls++; return Task.FromResult(new byte[] { 1, 2, 3 }); }, _ => { });
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

        private static FPElevenLabsGenerationRequest Speech(string output = "Assets/bonjour.mp3") =>
            new FPElevenLabsGenerationRequest { text = "Bonjour", voiceId = "approved-voice", outputAssetPath = output };

        [Test]
        public void Prepare_IsOfflineAndSnapshotsExactText()
        {
            var request = Speech();
            request.text = " Bonjour \n";
            var manifest = service.Prepare(new[] { request }, 1, 10);
            request.text = "changed";
            Assert.That(manifest.requests[0].text, Is.EqualTo(" Bonjour \n"));
            Assert.That(manifest.newCharacters, Is.EqualTo(10));
            Assert.That(calls, Is.Zero);
            Assert.That(Directory.Exists(Path.Combine(root, "UserSettings")), Is.False);
        }

        [Test]
        public void Execute_RejectsMissingApprovalAndChangedManifest()
        {
            var manifest = service.Prepare(new[] { Speech() }, 1, 7);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ExecuteAsync(manifest, ""));
            string approved = manifest.hash;
            manifest.requests[0].text = "Bonsoir";
            Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ExecuteAsync(manifest, approved));
            Assert.That(calls, Is.Zero);
        }

        [TestCase(0, 7)]
        [TestCase(1, 6)]
        public void Execute_RejectsInsufficientLimitsBeforeNetwork(int requests, int characters)
        {
            var manifest = service.Prepare(new[] { Speech() }, requests, characters);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ExecuteAsync(manifest, manifest.hash));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public async Task Resume_AfterImportFailureOrDeletedOutput_DoesNotRepurchase()
        {
            bool failImport = true;
            service = new FPElevenLabsGenerationService(root, _ => { calls++; return Task.FromResult(new byte[] { 1, 2, 3 }); },
                _ => { if (failImport) throw new IOException("Import failed"); });
            var manifest = service.Prepare(new[] { Speech() }, 1, 7);
            Assert.ThrowsAsync<IOException>(async () => await service.ExecuteAsync(manifest, manifest.hash));
            Assert.That(File.Exists(Path.Combine(root, "Assets/bonjour.mp3")), Is.True);
            failImport = false;
            await service.ResumeAsync(manifest, manifest.hash);
            File.Delete(Path.Combine(root, "Assets/bonjour.mp3"));
            await service.ResumeAsync(manifest, manifest.hash);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(Path.Combine(root, "Assets/bonjour.mp3")), Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void FailedProviderRequest_IsDurablyUncertainAcrossServiceRestart()
        {
            service = new FPElevenLabsGenerationService(root, _ => { calls++; throw new IOException("Timeout"); }, _ => { });
            var manifest = service.Prepare(new[] { Speech() }, 5, 100);
            Assert.ThrowsAsync<IOException>(async () => await service.ExecuteAsync(manifest, manifest.hash));
            var restarted = new FPElevenLabsGenerationService(root, _ => { calls++; return Task.FromResult(new byte[] { 1 }); }, _ => { });
            Assert.That(restarted.Status(manifest).items[0].state, Is.EqualTo("uncertain"));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await restarted.ResumeAsync(manifest, manifest.hash));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task IdenticalRequestsAtDifferentOutputs_UseOnePaidRequest()
        {
            var manifest = service.Prepare(new[] { Speech(), Speech("Assets/copy.mp3") }, 1, 7);
            Assert.That(manifest.newRequests, Is.EqualTo(1));
            await service.ExecuteAsync(manifest, manifest.hash);
            Assert.That(calls, Is.EqualTo(1));
            var reuse = service.Prepare(new[] { Speech("Assets/reused.mp3") }, 0, 0);
            await service.ExecuteAsync(reuse, reuse.hash);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ExistingUntrackedOutput_IsReportedAndNeverOverwritten()
        {
            File.WriteAllText(Path.Combine(root, "Assets/bonjour.mp3"), "existing");
            var manifest = service.Prepare(new[] { Speech() }, 1, 7);
            Assert.That(manifest.items[0].state, Is.EqualTo("conflict"));
            Assert.That(manifest.items[0].existingSha256, Is.Not.Empty);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ExecuteAsync(manifest, manifest.hash));
            Assert.That(calls, Is.Zero);
            Assert.That(File.ReadAllText(Path.Combine(root, "Assets/bonjour.mp3")), Is.EqualTo("existing"));
        }

        [TestCase("Assets/../outside.mp3")]
        [TestCase("Assets/missing/clip.mp3")]
        [TestCase("Packages/clip.mp3")]
        public void Prepare_RejectsInvalidOutput(string output)
        { Assert.Throws<ArgumentException>(() => service.Prepare(new[] { Speech(output) }, 1, 7)); }

        [Test]
        public void Prepare_RejectsDuplicateOutputPaths()
        { Assert.Throws<ArgumentException>(() => service.Prepare(new[] { Speech(), Speech() }, 2, 14)); }

        [Test]
        public async Task Translation_IsIndependentAndReusesDurableText()
        {
            service = new FPElevenLabsGenerationService(root, request =>
            { calls++; Assert.That(request.operation, Is.EqualTo("translation")); return Task.FromResult(Encoding.UTF8.GetBytes("Bonjour")); },
                _ => Assert.Fail("Translation must not import audio"));
            var request = new FPElevenLabsGenerationRequest { operation = "translation", text = "Hello", modelId = "translation-model" };
            var manifest = service.Prepare(new[] { request }, 1, 5);
            await service.ExecuteAsync(manifest, manifest.hash);
            var result = await service.ResumeAsync(manifest, manifest.hash);
            Assert.That(result.items[0].resultText, Is.EqualTo("Bonjour"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task ConcurrentExecution_IsRejectedWhileFirstRequestRuns()
        {
            var pending = new TaskCompletionSource<byte[]>();
            service = new FPElevenLabsGenerationService(root, _ => { calls++; return pending.Task; }, _ => { });
            var manifest = service.Prepare(new[] { Speech() }, 1, 7);
            var first = service.ExecuteAsync(manifest, manifest.hash);
            try { Assert.ThrowsAsync<IOException>(async () => await service.ExecuteAsync(manifest, manifest.hash)); }
            finally { pending.SetResult(new byte[] { 1 }); await first; }
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task ExplicitExistingHash_AdoptsClipWithoutProviderRequest()
        {
            byte[] bytes = new byte[] { 4, 5, 6 };
            File.WriteAllBytes(Path.Combine(root, "Assets/bonjour.mp3"), bytes);
            var request = Speech();
            request.existingSha256 = FPElevenLabsGenerationService.Hash(bytes);
            var manifest = service.Prepare(new[] { request }, 0, 0);
            Assert.That(manifest.items[0].state, Is.EqualTo("existing"));
            await service.ExecuteAsync(manifest, manifest.hash);
            var reuse = service.Prepare(new[] { Speech("Assets/copy.mp3") }, 0, 0);
            await service.ExecuteAsync(reuse, reuse.hash);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public async Task CorruptCache_BlocksRepurchase()
        {
            var manifest = service.Prepare(new[] { Speech() }, 1, 7);
            await service.ExecuteAsync(manifest, manifest.hash);
            string cache = Path.Combine(root, "UserSettings/FPElevenLabs", manifest.items[0].requestHash + ".response");
            File.WriteAllText(cache, "changed");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await service.ResumeAsync(manifest, manifest.hash));
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
