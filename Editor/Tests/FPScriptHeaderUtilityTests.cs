// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using NUnit.Framework;

    public class FPScriptHeaderUtilityTests
    {
        private const string Header = "// Copyright Test\n// License Test";

        [Test]
        public void InspectText_ExactHeader_ReturnsMatch()
        {
            FPScriptHeaderInspection inspection = FPScriptHeaderUtility.InspectText(
                Header + "\n\nnamespace FuzzPhyte.Utility {}",
                Header);

            Assert.That(inspection.Status, Is.EqualTo(FPScriptHeaderStatus.Match));
        }

        [Test]
        public void InspectText_NoLeadingComment_ReturnsMissing()
        {
            FPScriptHeaderInspection inspection = FPScriptHeaderUtility.InspectText(
                "namespace FuzzPhyte.Utility {}",
                Header);

            Assert.That(inspection.Status, Is.EqualTo(FPScriptHeaderStatus.Missing));
        }

        [Test]
        public void InspectText_ExternalCopyright_ReturnsManualReviewStatus()
        {
            FPScriptHeaderInspection inspection = FPScriptHeaderUtility.InspectText(
                "// Copyright (c) Another Studio\n\nnamespace Student.Project {}",
                Header);

            Assert.That(inspection.Status, Is.EqualTo(FPScriptHeaderStatus.ExternalCopyright));
        }

        [Test]
        public void InspectText_UnterminatedBlockComment_ReturnsMalformed()
        {
            FPScriptHeaderInspection inspection = FPScriptHeaderUtility.InspectText(
                "/* Copyright Test\nnamespace FuzzPhyte.Utility {}",
                Header);

            Assert.That(inspection.Status, Is.EqualTo(FPScriptHeaderStatus.Malformed));
        }

        [Test]
        public void BuildHeaderContent_ReplacesHeaderAndPreservesCrLfAndBody()
        {
            string original = "// Old Header\r\n\r\nnamespace FuzzPhyte.Utility\r\n{\r\n}\r\n";

            string result = FPScriptHeaderUtility.BuildHeaderContent(original, Header, true);

            Assert.That(result, Does.StartWith("// Copyright Test\r\n// License Test\r\n\r\n"));
            Assert.That(result, Does.Contain("namespace FuzzPhyte.Utility\r\n{\r\n}"));
            Assert.That(result, Does.Not.Contain("Old Header"));
            Assert.That(result.Replace("\r\n", string.Empty), Does.Not.Contain("\n"));
        }

        [Test]
        public void BuildHeaderContent_AppendModePreservesExistingHeaderAsBody()
        {
            string original = "// Existing Header\n\nnamespace FuzzPhyte.Utility {}";

            string result = FPScriptHeaderUtility.BuildHeaderContent(original, Header, false);

            Assert.That(result, Does.Contain("// Existing Header"));
            Assert.That(result, Does.EndWith("namespace FuzzPhyte.Utility {}"));
        }
    }
}
