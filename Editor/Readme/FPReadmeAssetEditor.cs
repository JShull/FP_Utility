// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(FPReadmeAsset))]
    public class FPReadmeAssetEditor:UnityEditor.Editor
    {
        private const float HeaderIconSize = 72f;
        private const float InlineLinkVerticalOffset = -2f;
        private const float SectionDividerTopSpacing = 12f;
        private const float SectionDividerBottomSpacing = 10f;
        private const int SectionHeadingFontSize = 16;
        private const string MenuTargetPrefix = "menu:";
        private const string UnityMenuTargetPrefix = "unity-menu:";

        private static readonly Regex MarkdownLinkPattern =
            new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

        public override void OnInspectorGUI()
        {
            var readme = (FPReadmeAsset)target;
            var packageReadme = IsPackageAsset(readme);
            var previousGuiEnabled = GUI.enabled;

            GUI.enabled = true;

            try
            {
                DrawHeader(readme);

                EditorGUILayout.Space(12);

                if (!string.IsNullOrWhiteSpace(readme.overview))
                {
                    DrawBodyText(readme.overview);
                }

                if (readme.overviewLinks != null)
                {
                    foreach (var link in readme.overviewLinks)
                    {
                        DrawLink(link);
                    }
                }

                if (!string.IsNullOrWhiteSpace(readme.overview) ||
                    (readme.overviewLinks != null && readme.overviewLinks.Count > 0))
                {
                    EditorGUILayout.Space(12);
                }

                for (var i = 0; i < readme.sections.Count; i++)
                {
                    DrawSection(readme.sections[i], i > 0 && readme.sections[i].showSeparatorBefore);
                }

                EditorGUILayout.Space(16);
            }
            finally
            {
                GUI.enabled = previousGuiEnabled;
            }

            GUI.enabled = true;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Readme Asset",
                    readme,
                    typeof(FPReadmeAsset),
                    false
                );
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(packageReadme))
            {
                if (GUILayout.Button("Edit Readme Data"))
                {
                    Selection.activeObject = readme;
                    EditorGUIUtility.PingObject(readme);
                }
            }

            if (packageReadme)
            {
                EditorGUILayout.HelpBox(
                    "This package Readme is displayed from the installed package, so it is read-only. Embed or copy the package before editing the asset.",
                    MessageType.None
                );
            }

            GUI.enabled = previousGuiEnabled;
        }

        private static void DrawHeader(FPReadmeAsset readme)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (readme.icon != null)
                {
                    GUILayout.Label(
                        readme.icon,
                        GUILayout.Width(HeaderIconSize),
                        GUILayout.Height(HeaderIconSize)
                    );
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(4);

                    var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 20,
                        wordWrap = true
                    };

                    EditorGUILayout.LabelField(readme.title, titleStyle);

                    if (!string.IsNullOrWhiteSpace(readme.subtitle))
                    {
                        var subtitleStyle = new GUIStyle(EditorStyles.label)
                        {
                            fontSize = 12,
                            wordWrap = true
                        };

                        EditorGUILayout.LabelField(readme.subtitle, subtitleStyle);
                    }

                    if (!string.IsNullOrWhiteSpace(readme.version))
                    {
                        EditorGUILayout.LabelField(
                            $"Version {readme.version}",
                            EditorStyles.miniLabel
                        );
                    }
                }
            }
        }

        private static void DrawSection(FPReadmeSection section, bool drawDivider)
        {
            if (section == null)
            {
                return;
            }

            if (drawDivider)
            {
                DrawSectionDivider();
            }
            else
            {
                EditorGUILayout.Space(8);
            }

            if (!string.IsNullOrWhiteSpace(section.heading))
            {
                var headingStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = SectionHeadingFontSize,
                    wordWrap = true
                };

                EditorGUILayout.LabelField(section.heading, headingStyle);
            }

            if (!string.IsNullOrWhiteSpace(section.body))
            {
                DrawBodyText(section.body);
            }

            if (section.links != null)
            {
                foreach (var link in section.links)
                {
                    DrawLink(link);
                }
            }
        }

        private static void DrawSectionDivider()
        {
            EditorGUILayout.Space(SectionDividerTopSpacing);

            Rect rect = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            rect.x += 2f;
            rect.width = Mathf.Max(1f, rect.width - 4f);
            EditorGUI.DrawRect(rect, FP_Utility_Editor.WarningColor);

            EditorGUILayout.Space(SectionDividerBottomSpacing);
        }

        private static void DrawBodyText(string text)
        {
            if (ContainsInlineLinks(text))
            {
                DrawInteractiveBodyText(text);
                return;
            }

            var bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                richText = true
            };

            EditorGUILayout.LabelField(text, bodyStyle);
        }

        private static void DrawLink(FPReadmeLink link)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.label))
            {
                return;
            }

            var linkStyle = CreateLinkStyle(link.url, true);

            var rect = GUILayoutUtility.GetRect(
                new GUIContent(link.label),
                linkStyle
            );

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

            if (GUI.Button(rect, link.label, linkStyle))
            {
                OpenTarget(link.url);
            }
        }

        private static bool ContainsInlineLinks(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && MarkdownLinkPattern.IsMatch(text);
        }

        private static void DrawInteractiveBodyText(string text)
        {
            var bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                richText = true,
                wordWrap = true
            };

            var inlineText = ParseInlineText(text);
            var availableWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 48f);
            var plainContent = new GUIContent(inlineText.Text);
            var renderContent = new GUIContent(inlineText.RenderText);
            var height = bodyStyle.CalcHeight(plainContent, availableWidth);
            var rect = GUILayoutUtility.GetRect(
                plainContent,
                bodyStyle,
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true)
            );

            GUI.Label(rect, renderContent, bodyStyle);

            foreach (var link in inlineText.Links)
            {
                DrawInlineLinkOverlay(rect, plainContent, bodyStyle, link);
            }
        }

        private static InlineText ParseInlineText(string text)
        {
            var normalizedText = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            var links = new List<InlineLink>();
            var displayText = string.Empty;
            var renderText = string.Empty;
            var cursor = 0;

            foreach (Match match in MarkdownLinkPattern.Matches(normalizedText))
            {
                if (match.Index > cursor)
                {
                    var plainText = normalizedText.Substring(cursor, match.Index - cursor);
                    displayText += plainText;
                    renderText += plainText;
                }

                var label = match.Groups[1].Value;
                var startIndex = displayText.Length;
                displayText += label;
                renderText += $"<color=#00000000>{label}</color>";

                links.Add(new InlineLink(startIndex, label.Length, match.Groups[2].Value));

                cursor = match.Index + match.Length;
            }

            if (cursor < normalizedText.Length)
            {
                var plainText = normalizedText.Substring(cursor);
                displayText += plainText;
                renderText += plainText;
            }

            return new InlineText(displayText, renderText, links);
        }

        private static void DrawInlineLinkOverlay(
            Rect textRect,
            GUIContent content,
            GUIStyle bodyStyle,
            InlineLink link)
        {
            var linkRects = GetLinkRects(textRect, content, bodyStyle, link);
            var label = content.text.Substring(link.StartIndex, link.Length);
            var linkStyle = CreateLinkStyle(link.Target, false);

            foreach (var rect in linkRects)
            {
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    OpenTarget(link.Target);
                }
            }

            DrawLinkTextSegments(textRect, content, bodyStyle, linkStyle, link, label);
        }

        private static List<Rect> GetLinkRects(
            Rect textRect,
            GUIContent content,
            GUIStyle bodyStyle,
            InlineLink link)
        {
            var rects = new List<Rect>();
            var lineHeight = Mathf.Ceil(Mathf.Max(EditorGUIUtility.singleLineHeight, bodyStyle.lineHeight));
            Rect? currentRect = null;
            var endIndex = link.StartIndex + link.Length;

            for (var i = link.StartIndex; i < endIndex; i++)
            {
                if (content.text[i] == '\n')
                {
                    FlushRect(rects, ref currentRect);
                    continue;
                }

                var start = bodyStyle.GetCursorPixelPosition(textRect, content, i);
                var end = bodyStyle.GetCursorPixelPosition(textRect, content, i + 1);

                if (end.y > start.y + 0.5f || end.x < start.x)
                {
                    end = new Vector2(textRect.xMax, start.y);
                }

                var charRect = new Rect(
                    start.x,
                    start.y,
                    Mathf.Max(1f, end.x - start.x),
                    lineHeight
                );

                if (currentRect.HasValue && Mathf.Abs(currentRect.Value.y - charRect.y) < 0.5f)
                {
                    var existing = currentRect.Value;
                    existing.xMax = Mathf.Max(existing.xMax, charRect.xMax);
                    currentRect = existing;
                }
                else
                {
                    FlushRect(rects, ref currentRect);
                    currentRect = charRect;
                }
            }

            FlushRect(rects, ref currentRect);
            return rects;
        }

        private static void DrawLinkTextSegments(
            Rect textRect,
            GUIContent content,
            GUIStyle bodyStyle,
            GUIStyle linkStyle,
            InlineLink link,
            string label)
        {
            var linkStart = link.StartIndex;
            var segmentStart = linkStart;
            var currentLineY = bodyStyle.GetCursorPixelPosition(textRect, content, linkStart).y;
            var endIndex = link.StartIndex + link.Length;

            for (var i = link.StartIndex; i <= endIndex; i++)
            {
                var isEnd = i == endIndex;
                var y = isEnd
                    ? currentLineY
                    : bodyStyle.GetCursorPixelPosition(textRect, content, i).y;

                if (!isEnd && Mathf.Abs(y - currentLineY) < 0.5f)
                {
                    continue;
                }

                DrawLinkTextSegment(
                    textRect,
                    content,
                    bodyStyle,
                    linkStyle,
                    segmentStart,
                    i - segmentStart,
                    label,
                    linkStart
                );

                segmentStart = i;
                currentLineY = y;
            }
        }

        private static void DrawLinkTextSegment(
            Rect textRect,
            GUIContent content,
            GUIStyle bodyStyle,
            GUIStyle linkStyle,
            int segmentStart,
            int segmentLength,
            string fullLabel,
            int linkStart)
        {
            if (segmentLength <= 0)
            {
                return;
            }

            var start = bodyStyle.GetCursorPixelPosition(textRect, content, segmentStart);
            var segmentOffset = segmentStart - linkStart;
            var segmentText = fullLabel.Substring(segmentOffset, segmentLength);
            var segmentContent = new GUIContent(segmentText);
            var segmentWidth = linkStyle.CalcSize(segmentContent).x;
            var lineHeight = Mathf.Ceil(Mathf.Max(EditorGUIUtility.singleLineHeight, bodyStyle.lineHeight));
            var rect = new Rect(
                start.x,
                start.y + InlineLinkVerticalOffset,
                segmentWidth,
                lineHeight
            );

            GUI.Label(rect, segmentContent, linkStyle);
        }

        private static void FlushRect(List<Rect> rects, ref Rect? currentRect)
        {
            if (!currentRect.HasValue)
            {
                return;
            }

            rects.Add(currentRect.Value);
            currentRect = null;
        }

        private static void OpenTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            target = target.Trim();

            if (TryGetMenuPath(target, out var menuPath))
            {
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                {
                    Debug.LogWarning($"Readme link could not execute Unity menu item: {menuPath}");
                }

                return;
            }

            Application.OpenURL(target);
        }

        private static bool TryGetMenuPath(string target, out string menuPath)
        {
            if (target.StartsWith(MenuTargetPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                menuPath = target.Substring(MenuTargetPrefix.Length).Trim();
                return !string.IsNullOrWhiteSpace(menuPath);
            }

            if (target.StartsWith(UnityMenuTargetPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                menuPath = target.Substring(UnityMenuTargetPrefix.Length).Trim();
                return !string.IsNullOrWhiteSpace(menuPath);
            }

            if (target.StartsWith("FuzzPhyte/", System.StringComparison.Ordinal))
            {
                menuPath = target;
                return true;
            }

            menuPath = null;
            return false;
        }

        private readonly struct InlineText
        {
            public InlineText(string text, string renderText, List<InlineLink> links)
            {
                Text = text;
                RenderText = renderText;
                Links = links;
            }

            public string Text { get; }
            public string RenderText { get; }
            public List<InlineLink> Links { get; }
        }

        private readonly struct InlineLink
        {
            public InlineLink(int startIndex, int length, string target)
            {
                StartIndex = startIndex;
                Length = length;
                Target = target;
            }

            public int StartIndex { get; }
            public int Length { get; }
            public string Target { get; }
        }

        private static GUIStyle CreateLinkStyle(string target, bool wordWrap)
        {
            var linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                richText = false,
                wordWrap = wordWrap,
                stretchWidth = wordWrap
            };

            if (IsMenuTarget(target))
            {
                SetStyleTextColor(linkStyle, FP_Utility_Editor.WarningColor);
            }

            return linkStyle;
        }

        private static void SetStyleTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static bool IsMenuTarget(string target)
        {
            return TryGetMenuPath(target, out _);
        }

        private static bool IsPackageAsset(Object asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            return assetPath.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
