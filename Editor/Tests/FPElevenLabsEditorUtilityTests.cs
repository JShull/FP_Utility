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
    using NUnit.Framework;

    public class FPElevenLabsEditorUtilityTests
    {
        [TestCase("Narration", "Narration.mp3")]
        [TestCase("Narration.mp3", "Narration.mp3")]
        [TestCase("Scene/Intro: One", "Scene_Intro_ One.mp3")]
        [TestCase("", "ElevenLabsSpeech.mp3")]
        public void SanitizeMp3FileName_ReturnsPortableMp3Name(string input, string expected)
        {
            Assert.That(FPElevenLabsEditorUtility.SanitizeMp3FileName(input), Is.EqualTo(expected));
        }

        [Test]
        public void BuildLanguageMp3FileName_AddsVariantLanguageAndVoiceName()
        {
            string fileName = FPElevenLabsEditorUtility.BuildLanguageMp3FileName(
                "Museum Welcome.mp3",
                "Translation",
                FPTranslationLanguage.French,
                "Marie");

            Assert.That(fileName, Is.EqualTo("Museum Welcome_Translation_French_Marie.mp3"));
        }

        [Test]
        public void BuildLanguageMp3FileName_SanitizesVoiceName()
        {
            string fileName = FPElevenLabsEditorUtility.BuildLanguageMp3FileName(
                "Welcome",
                "Original",
                FPTranslationLanguage.English,
                "Marie/Studio");

            Assert.That(fileName, Is.EqualTo("Welcome_Original_English_Marie_Studio.mp3"));
        }

        [Test]
        public void BuildLanguageMp3FileName_ColorItemPrefixesColor()
        {
            string fileName = FPElevenLabsEditorUtility.BuildLanguageMp3FileName(
                "Blue",
                "Original",
                FPTranslationLanguage.Spanish,
                "Alex",
                true);

            Assert.That(fileName, Is.EqualTo("Color_Blue_Original_Spanish_Alex.mp3"));
        }

        [Test]
        public void GetEnglishBaseFileName_SelectsTheEnglishForm()
        {
            Assert.That(
                FPElevenLabsEditorUtility.GetEnglishBaseFileName(
                    FPTranslationLanguage.English,
                    FPTranslationLanguage.French,
                    "Blue",
                    "Bleu",
                    string.Empty),
                Is.EqualTo("Blue"));
            Assert.That(
                FPElevenLabsEditorUtility.GetEnglishBaseFileName(
                    FPTranslationLanguage.Spanish,
                    FPTranslationLanguage.English,
                    "Azul",
                    "Blue",
                    string.Empty),
                Is.EqualTo("Blue"));
            Assert.That(
                FPElevenLabsEditorUtility.GetEnglishBaseFileName(
                    FPTranslationLanguage.Spanish,
                    FPTranslationLanguage.French,
                    "Azul",
                    "Bleu",
                    "Blue"),
                Is.EqualTo("Blue"));
        }

        [Test]
        public void BuildVocabAssetFileName_UsesAudioFileNameWithoutMp3Extension()
        {
            string fileName = FPElevenLabsVocabAssetUtility.BuildVocabAssetFileName(
                "Assets/Generated/Blue_Original_English_Alex.mp3");

            Assert.That(fileName, Is.EqualTo("Blue_Original_English_Alex.asset"));
        }

        [Test]
        public void MarkdownParser_ReadsPersonLanguagePairAndColorItems()
        {
            const string markdown = "# Eleven Labs\n\n"
                + "## Person\n\n* Alex\n\n"
                + "## Translation Model\n\n* Spanish to English\n\n"
                + "## Language\n\n* La maleta\n* ¡Es dificil!\n\n"
                + "## Color\n\n* azul\n* blanco\n";

            bool parsed = FPElevenLabsMarkdownUtility.TryParse(
                markdown,
                out FPElevenLabsMarkdownDocument document,
                out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(document.Person, Is.EqualTo("Alex"));
            Assert.That(document.SourceLanguage, Is.EqualTo(FPTranslationLanguage.Spanish));
            Assert.That(document.TargetLanguage, Is.EqualTo(FPTranslationLanguage.English));
            Assert.That(document.Items.Count, Is.EqualTo(4));
            Assert.That(document.Items[0].Text, Is.EqualTo("La maleta"));
            Assert.That(document.Items[0].IsColor, Is.False);
            Assert.That(document.Items[2].Text, Is.EqualTo("azul"));
            Assert.That(document.Items[2].IsColor, Is.True);
        }

        [Test]
        public void MarkdownParser_AllowsMissingColorSection()
        {
            const string markdown = "## Person\n* Marie\n"
                + "## Translation Model\n* English to French\n"
                + "## Language\n* Welcome\n";

            bool parsed = FPElevenLabsMarkdownUtility.TryParse(
                markdown,
                out FPElevenLabsMarkdownDocument document,
                out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(document.Items.Count, Is.EqualTo(1));
            Assert.That(document.Items[0].IsColor, Is.False);
        }

        [Test]
        public void MarkdownBuilder_RoundTripsSupportedFields()
        {
            var source = new FPElevenLabsMarkdownDocument
            {
                Person = "Alex",
                SourceLanguage = FPTranslationLanguage.Spanish,
                TargetLanguage = FPTranslationLanguage.English
            };
            source.Items.Add(new FPElevenLabsMarkdownItem("La maleta", false));
            source.Items.Add(new FPElevenLabsMarkdownItem("azul", true));

            string markdown = FPElevenLabsMarkdownUtility.BuildMarkdown(source);
            bool parsed = FPElevenLabsMarkdownUtility.TryParse(
                markdown,
                out FPElevenLabsMarkdownDocument result,
                out string error);

            Assert.That(parsed, Is.True, error);
            Assert.That(result.Person, Is.EqualTo(source.Person));
            Assert.That(result.SourceLanguage, Is.EqualTo(source.SourceLanguage));
            Assert.That(result.TargetLanguage, Is.EqualTo(source.TargetLanguage));
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items[1].IsColor, Is.True);
            Assert.That(result.Items[1].Text, Is.EqualTo("azul"));
        }

        [Test]
        public void ExtractOpenAIOutputText_SkipsNonTextItemsAndCombinesOutputText()
        {
            const string responseJson = "{\"output\":["
                + "{\"type\":\"reasoning\"},"
                + "{\"type\":\"message\",\"content\":["
                + "{\"type\":\"output_text\",\"text\":\"Bonjour\"},"
                + "{\"type\":\"refusal\",\"text\":\"ignored\"}]},"
                + "{\"type\":\"message\",\"content\":["
                + "{\"type\":\"output_text\",\"text\":\"le monde\"}]}]}";

            string outputText = FPElevenLabsEditorUtility.ExtractOpenAIOutputText(responseJson);

            Assert.That(outputText, Is.EqualTo("Bonjour" + System.Environment.NewLine + "le monde"));
        }

        [Test]
        public void TryConvertAbsoluteFolderToAssetPath_ConvertsAssetsChild()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "FPUtilityElevenLabsProject");
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            string selectedFolder = Path.Combine(assetsRoot, "Generated", "Voices");

            bool converted = FPElevenLabsEditorUtility.TryConvertAbsoluteFolderToAssetPath(
                selectedFolder,
                assetsRoot,
                out string assetFolder);

            Assert.That(converted, Is.True);
            Assert.That(assetFolder, Is.EqualTo("Assets/Generated/Voices"));
        }

        [Test]
        public void TryConvertAbsoluteFolderToAssetPath_RejectsFolderOutsideAssets()
        {
            string projectRoot = Path.Combine(Path.GetTempPath(), "FPUtilityElevenLabsProject");
            string assetsRoot = Path.Combine(projectRoot, "Assets");
            string selectedFolder = Path.Combine(projectRoot, "AudioOutsideAssets");

            bool converted = FPElevenLabsEditorUtility.TryConvertAbsoluteFolderToAssetPath(
                selectedFolder,
                assetsRoot,
                out string assetFolder);

            Assert.That(converted, Is.False);
            Assert.That(assetFolder, Is.Empty);
        }

        [Test]
        public void GetAbsoluteFolderPath_PathTraversalFallsBackToAssetsRoot()
        {
            string assetsRoot = Path.Combine(Path.GetTempPath(), "FPUtilityElevenLabsProject", "Assets");

            string absoluteFolder = FPElevenLabsEditorUtility.GetAbsoluteFolderPath(
                "Assets/../AudioOutsideAssets",
                assetsRoot);

            Assert.That(absoluteFolder, Is.EqualTo(Path.GetFullPath(assetsRoot)));
        }
    }
}
