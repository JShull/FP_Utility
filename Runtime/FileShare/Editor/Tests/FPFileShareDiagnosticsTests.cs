namespace FuzzPhyte.Utility.FileShare.Editor.Tests
{
    using System;
    using System.Reflection;
    using NUnit.Framework;

    public class FPFileShareDiagnosticsTests
    {
        [Test]
        public void ShortTokens_AvoidAmbiguousCharacters_AndLegacyTokensAreUnchanged()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 128; i++)
            {
                string token = FPFileShareProtocol.CreateToken();
                Assert.That(token, Does.Match("^[2-9A-HJ-NP-Z]{12}$"));
                Assert.That(seen.Add(token), Is.True);
            }
            const string legacy = "aBcD0123+/efGH4567ijKL890mnOPqrst";
            Assert.That(FPFileShareProtocol.NormalizeToken(legacy), Is.EqualTo(legacy));
            Assert.That(FPFileShareProtocol.NormalizeToken("abcd-efgh-jkmn"), Is.EqualTo("ABCDEFGHJKMN"));
            Assert.That(FPFileShareProtocol.NormalizeToken("abcd\r\nefghjkmn"), Does.Contain("\r\n"));
        }

        [TestCase(401, FPFileShareFailure.TokenRejected)]
        [TestCase(409, FPFileShareFailure.Conflict)]
        [TestCase(413, FPFileShareFailure.SizeLimit)]
        [TestCase(422, FPFileShareFailure.ChecksumMismatch)]
        [TestCase(500, FPFileShareFailure.HttpError)]
        [TestCase(0, FPFileShareFailure.ConnectionFailure)]
        public void HttpFailure_HasStableCodeAndStatus_WithoutRawServerDetails(int status, FPFileShareFailure expected)
        {
            var error = Classify(status, "untrusted details / token-value / file-contents");
            Assert.That(error.Failure, Is.EqualTo(expected));
            Assert.That(error.HttpStatusCode, Is.EqualTo(status));
            Assert.That(error.Message, Does.Not.Contain("token-value"));
            Assert.That(error.Message, Does.Not.Contain("file-contents"));
        }

        [TestCase("Non-secure network connections disabled in Player Settings")]
        [TestCase("Insecure connection not allowed")]
        [TestCase("App Transport Security requires a secure connection")]
        public void ReportedPolicyMessages_AreActionable(string message)
        {
            var error = Classify(0, message);
            Assert.That(error.Failure, Is.EqualTo(FPFileShareFailure.PolicyRejected));
            Assert.That(error.Message, Does.Contain("build configuration"));
            var original = new InvalidOperationException(message);
            var wrapped = (FPFileShareException)typeof(FPFileShareException).GetMethod("Policy", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { original });
            Assert.That(wrapped.InnerException, Is.SameAs(original));
        }

        [TestCase("Request timeout")]
        [TestCase("Request timed out")]
        public void Timeout_HasDistinctCode(string message)
            => Assert.That(Classify(0, message).Failure, Is.EqualTo(FPFileShareFailure.Timeout));

        private static FPFileShareException Classify(long status, string message)
            => (FPFileShareException)typeof(FPFileShareException).GetMethod("FromResponse", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { status, message });
    }
}
