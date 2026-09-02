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
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    internal enum FPScriptHeaderStatus
    {
        Match,
        Missing,
        Different,
        ExternalCopyright,
        Malformed,
        Unreadable
    }

    internal readonly struct FPScriptHeaderInspection
    {
        public readonly FPScriptHeaderStatus Status;
        public readonly string Message;

        public FPScriptHeaderInspection(FPScriptHeaderStatus status, string message)
        {
            Status = status;
            Message = message;
        }
    }

    /// <summary>
    /// Shared inspection and replacement behavior for FuzzPhyte script headers.
    /// </summary>
    internal static class FPScriptHeaderUtility
    {
        internal const string DefaultHeaderText =
            "// Copyright (c) 2026 John B. Shull\n" +
            "// FuzzPhyte LLC is a company associated with John B. Shull\n" +
            "// This file is part of FP_Utility Package.\n" +
            "//\n" +
            "// Public license: GNU GPLv3-or-later.\n" +
            "// Commercial/proprietary use requires a separate license from John B. Shull.\n" +
            "//\n" +
            "// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.";

        private const string HeaderEditorPrefsKey = "FP_ScriptHeaderEditor_HeaderText";

        internal static string GetConfiguredHeaderText()
        {
            return EditorPrefs.GetString(HeaderEditorPrefsKey, DefaultHeaderText);
        }

        internal static bool HasConfiguredHeaderText()
        {
            return EditorPrefs.HasKey(HeaderEditorPrefsKey);
        }

        internal static void SetConfiguredHeaderText(string headerText)
        {
            EditorPrefs.SetString(HeaderEditorPrefsKey, headerText ?? string.Empty);
        }

        internal static FPScriptHeaderInspection InspectAsset(string assetPath, string expectedHeader)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return new FPScriptHeaderInspection(FPScriptHeaderStatus.Unreadable, "The script path is empty.");
            }

            string fullPath = GetFullProjectPath(assetPath);
            if (!File.Exists(fullPath))
            {
                return new FPScriptHeaderInspection(FPScriptHeaderStatus.Unreadable, "The script file does not exist.");
            }

            try
            {
                string source = ReadText(fullPath, out _);
                return InspectText(source, expectedHeader);
            }
            catch (Exception exception)
            {
                return new FPScriptHeaderInspection(
                    FPScriptHeaderStatus.Unreadable,
                    $"The script could not be read: {exception.Message}");
            }
        }

        internal static FPScriptHeaderInspection InspectText(string source, string expectedHeader)
        {
            string normalizedSource = NormalizeLineEndings(source);
            if (normalizedSource.StartsWith("\uFEFF", StringComparison.Ordinal))
            {
                normalizedSource = normalizedSource.Substring(1);
            }

            string normalizedExpected = NormalizeHeader(expectedHeader);
            if (string.IsNullOrWhiteSpace(normalizedExpected))
            {
                return new FPScriptHeaderInspection(FPScriptHeaderStatus.Unreadable, "The expected header is empty.");
            }

            int firstContent = SkipBlankLines(normalizedSource, 0);
            if (firstContent >= normalizedSource.Length)
            {
                return new FPScriptHeaderInspection(FPScriptHeaderStatus.Missing, "The script is empty and has no header.");
            }

            bool beginsWithLineComment = StartsWithAt(normalizedSource, firstContent, "//");
            bool beginsWithBlockComment = StartsWithAt(normalizedSource, firstContent, "/*");
            if (!beginsWithLineComment && !beginsWithBlockComment)
            {
                return new FPScriptHeaderInspection(FPScriptHeaderStatus.Missing, "No leading script header was found.");
            }

            if (beginsWithBlockComment && normalizedSource.IndexOf("*/", firstContent, StringComparison.Ordinal) < 0)
            {
                return new FPScriptHeaderInspection(FPScriptHeaderStatus.Malformed, "The leading block-comment header is not terminated.");
            }

            int bodyStart = FindBodyStart(normalizedSource);
            string existingHeader = normalizedSource.Substring(firstContent, Math.Max(0, bodyStart - firstContent)).Trim();
            if (string.Equals(existingHeader, normalizedExpected, StringComparison.Ordinal))
            {
                return new FPScriptHeaderInspection(FPScriptHeaderStatus.Match, "The script header matches the configured header.");
            }

            bool containsCopyright = existingHeader.IndexOf("copyright", StringComparison.OrdinalIgnoreCase) >= 0;
            bool identifiesFuzzPhyte =
                existingHeader.IndexOf("FuzzPhyte", StringComparison.OrdinalIgnoreCase) >= 0 ||
                existingHeader.IndexOf("John B. Shull", StringComparison.OrdinalIgnoreCase) >= 0;
            if (containsCopyright && !identifiesFuzzPhyte)
            {
                return new FPScriptHeaderInspection(
                    FPScriptHeaderStatus.ExternalCopyright,
                    "The leading header contains non-FuzzPhyte copyright information and requires manual review.");
            }

            return new FPScriptHeaderInspection(
                FPScriptHeaderStatus.Different,
                "The leading header does not match the configured Script Header Editor text.");
        }

        internal static string BuildHeaderContent(string originalText, string normalizedHeader, bool replaceHeader)
        {
            string normalizedOriginal = NormalizeLineEndings(originalText);
            if (normalizedOriginal.StartsWith("\uFEFF", StringComparison.Ordinal))
            {
                normalizedOriginal = normalizedOriginal.Substring(1);
            }

            string body = replaceHeader
                ? normalizedOriginal.Substring(FindBodyStart(normalizedOriginal))
                : normalizedOriginal.TrimStart('\n');

            body = body.TrimStart('\n');
            string nextText = string.IsNullOrEmpty(body)
                ? NormalizeHeader(normalizedHeader) + "\n"
                : NormalizeHeader(normalizedHeader) + "\n\n" + body;

            return nextText.Replace("\n", DetectLineEnding(originalText));
        }

        internal static int FindBodyStart(string text)
        {
            int index = 0;

            while (index < text.Length)
            {
                int lineStart = index;
                int lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd < 0)
                {
                    lineEnd = text.Length;
                }

                string line = text.Substring(lineStart, lineEnd - lineStart);
                string trimmedLine = line.TrimStart();
                int nextLineStart = lineEnd < text.Length ? lineEnd + 1 : lineEnd;

                if (string.IsNullOrWhiteSpace(line) || trimmedLine.StartsWith("//", StringComparison.Ordinal))
                {
                    index = nextLineStart;
                    continue;
                }

                if (trimmedLine.StartsWith("/*", StringComparison.Ordinal))
                {
                    int blockEnd = text.IndexOf("*/", lineStart, StringComparison.Ordinal);
                    if (blockEnd < 0)
                    {
                        return lineStart;
                    }

                    index = blockEnd + 2;
                    if (index < text.Length && text[index] == '\n')
                    {
                        index++;
                    }

                    continue;
                }

                break;
            }

            return SkipBlankLines(text, index);
        }

        internal static string NormalizeHeader(string text)
        {
            return NormalizeLineEndings(text).Trim();
        }

        internal static string NormalizeLineEndings(string text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        internal static string DetectLineEnding(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains("\r\n") ? "\r\n" : "\n";
        }

        internal static string ReadText(string path, out Encoding encoding)
        {
            byte[] bytes = File.ReadAllBytes(path);
            encoding = DetectEncoding(bytes);
            string text = encoding.GetString(bytes);
            return text.StartsWith("\uFEFF", StringComparison.Ordinal) ? text.Substring(1) : text;
        }

        internal static bool TryGetScriptTarget(Object obj, out string assetPath, out string fullPath)
        {
            assetPath = string.Empty;
            fullPath = string.Empty;
            if (obj == null)
            {
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = GetFullProjectPath(assetPath);
            return true;
        }

        internal static string GetFullProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static int SkipBlankLines(string text, int index)
        {
            while (index < text.Length)
            {
                int lineEnd = text.IndexOf('\n', index);
                if (lineEnd < 0)
                {
                    lineEnd = text.Length;
                }

                if (!string.IsNullOrWhiteSpace(text.Substring(index, lineEnd - index)))
                {
                    return index;
                }

                index = lineEnd < text.Length ? lineEnd + 1 : lineEnd;
            }

            return index;
        }

        private static bool StartsWithAt(string text, int index, string value)
        {
            return index >= 0 && index + value.Length <= text.Length &&
                   string.Compare(text, index, value, 0, value.Length, StringComparison.Ordinal) == 0;
        }

        private static Encoding DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new UTF8Encoding(true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode;
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }

            return new UTF8Encoding(false);
        }
    }
}
