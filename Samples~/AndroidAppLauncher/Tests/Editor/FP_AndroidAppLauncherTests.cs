// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.
namespace FuzzPhyte.Utility.Tests
{
    using NUnit.Framework;
    using UnityEngine;

    public class FP_AndroidAppLauncherTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("single")]
        [TestCase("com..app")]
        [TestCase("com.1app")]
        [TestCase("com.app-name")]
        [TestCase(" com.app")]
        [TestCase("com.app\n")]
        public void InvalidIdentifierIsRejected(string name)
        {
            Assert.That(FP_AndroidAppLauncher.IsValidPackageName(name), Is.False);
            Assert.That(FP_AndroidAppLauncher.TryLaunchApp(name).Status, Is.EqualTo(FP_AndroidLaunchStatus.InvalidPackageName));
        }

        [TestCase("com.fuzzphyte.experienceb")]
        [TestCase("com.Company.app_2")]
        public void ValidIdentifierReturnsUnsupportedInEditor(string name)
        {
            Assert.That(FP_AndroidAppLauncher.IsValidPackageName(name), Is.True);
            Assert.That(FP_AndroidAppLauncher.TryLaunchApp(name).Status, Is.EqualTo(FP_AndroidLaunchStatus.UnsupportedPlatform));
            Assert.That(FP_AndroidAppLauncher.LaunchApp(name), Is.False);
        }

        [Test]
        public void DefaultResultIsFailure()
        {
            Assert.That(default(FP_AndroidLaunchResult).Success, Is.False);
            Assert.That(new FP_AndroidLaunchResult(FP_AndroidLaunchStatus.Success, "requested").Success, Is.True);
        }

        [TestCase("hello")]
        [TestCase("{\"experienceId\":\"ExampleExperience\"}")]
        [TestCase("quotes \" slash \\ newline\nUnicode \u03bb")]
        public void PayloadRoundTrips(string data)
        {
            Assert.That(FP_AndroidHandoffPayload.TrySerialize(data, out var json, out var error), Is.True, error);
            Assert.That(FP_AndroidHandoffPayload.TryDeserialize(json, out var payload, out error), Is.True, error);
            Assert.That(payload.version, Is.EqualTo(1));
            Assert.That(payload.data, Is.EqualTo(data));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("[]")]
        [TestCase("{broken}")]
        [TestCase("{}")]
        [TestCase("{\"version\":2,\"data\":\"x\"}")]
        [TestCase("{\"data\":\"x\"}")]
        [TestCase("{\"version\":1}")]
        public void InvalidEnvelopeIsRejected(string json)
        {
            Assert.That(FP_AndroidHandoffPayload.TryDeserialize(json, out var payload, out var error), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void EmptyAndOversizedDataAreRejected()
        {
            Assert.That(FP_AndroidHandoffPayload.TrySerialize(" ", out _, out _), Is.False);
            Assert.That(FP_AndroidHandoffPayload.TrySerialize(new string('x', 16384), out _, out _), Is.False);
            Assert.That(FP_AndroidHandoffPayload.TryDeserialize(new string('x', 16385), out _, out _), Is.False);
            Assert.That(FP_AndroidAppLauncher.TryLaunchApp("com.example.app", " ").Status, Is.EqualTo(FP_AndroidLaunchStatus.InvalidPayload));
        }

        [Test]
        public void ReceivingInEditorIsSafe()
        {
            Assert.That(FP_AndroidAppLauncher.TryGetLaunchPayload(out var payload, out var error), Is.False);
            Assert.That(payload, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void BinderReportsConfiguredTargetFailure()
        {
            var go = new GameObject("Android launcher test");
            try
            {
                var binder = go.AddComponent<FP_AndroidAppLaunchBinder>();
                binder.TargetPackageName = "bad identifier";
                FP_AndroidLaunchResult received = default;
                int callbacks = 0;
                binder.LaunchCompleted += result => { received = result; callbacks++; };
                binder.LaunchTargetApplication();
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(received.Status, Is.EqualTo(FP_AndroidLaunchStatus.InvalidPackageName));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
