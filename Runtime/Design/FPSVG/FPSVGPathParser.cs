// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using UnityEngine;

    public static class FPSVGPathParser
    {
        private static readonly Regex PathTokenRegex = new Regex(
            @"[AaCcHhLlMmQqSsTtVvZz]|[-+]?(?:(?:\d*\.\d+)|(?:\d+\.?))(?:[eE][-+]?\d+)?",
            RegexOptions.Compiled);

        private static readonly Regex NumberRegex = new Regex(
            @"[-+]?(?:(?:\d*\.\d+)|(?:\d+\.?))(?:[eE][-+]?\d+)?",
            RegexOptions.Compiled);

        public static FPSVGParseResult Parse(string svgText, float pathSampleDistance)
        {
            var result = new FPSVGParseResult();
            if (string.IsNullOrWhiteSpace(svgText))
            {
                result.Errors.Add("SVG text is empty.");
                return result;
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(svgText);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"SVG XML could not be parsed: {ex.Message}");
                return result;
            }

            Dictionary<string, Dictionary<string, string>> styleClasses = ReadStyleClasses(document);
            int generatedId = 0;
            foreach (XElement element in document.Descendants())
            {
                string localName = element.Name.LocalName;
                string id = GetAttribute(element, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = $"{localName}_{generatedId++}";
                }

                SVGPaintInfo paintInfo = ReadPaintInfo(element, styleClasses);
                if (!string.IsNullOrWhiteSpace(GetAttribute(element, "transform")))
                {
                    result.Warnings.Add($"Element '{id}' has a transform. This first SVG extruder pass reads raw geometry and does not apply SVG transforms.");
                }

                try
                {
                    switch (localName)
                    {
                        case "path":
                            ParsePathElement(element, id, pathSampleDistance, paintInfo, result);
                            break;
                        case "polygon":
                            AddPointListRegion(element, id, true, paintInfo, result);
                            break;
                        case "polyline":
                            AddPointListRegion(element, id, false, paintInfo, result);
                            break;
                        case "rect":
                            AddRectRegion(element, id, paintInfo, result);
                            break;
                        case "circle":
                        case "ellipse":
                            AddEllipseRegion(element, id, pathSampleDistance, paintInfo, result);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Element '{id}' could not be parsed: {ex.Message}");
                }
            }

            CleanupRegions(result);
            result.Bounds = CalculateBounds(result.Regions);
            if (result.Regions.Count == 0 && result.Errors.Count == 0)
            {
                result.Errors.Add("No closed SVG path, polygon, rect, circle, or ellipse regions were found.");
            }

            return result;
        }

        private static void ParsePathElement(XElement element, string id, float sampleDistance, SVGPaintInfo paintInfo, FPSVGParseResult result)
        {
            string data = GetAttribute(element, "d");
            if (string.IsNullOrWhiteSpace(data))
            {
                result.Warnings.Add($"Path '{id}' has no path data.");
                return;
            }

            var parser = new PathDataParser(data, sampleDistance, result.Warnings, id);
            List<List<Vector2>> contours = parser.Parse();
            for (int i = 0; i < contours.Count; i++)
            {
                List<Vector2> loop = CleanLoop(contours[i]);
                if (loop.Count < 3)
                {
                    result.Warnings.Add($"Path '{id}' contour {i} was ignored because it has fewer than three unique points.");
                    continue;
                }

                result.Regions.Add(CreateRegion($"{id}_{i}", loop, paintInfo));
            }
        }

        private static void AddPointListRegion(XElement element, string id, bool requireClosed, SVGPaintInfo paintInfo, FPSVGParseResult result)
        {
            string points = GetAttribute(element, "points");
            if (string.IsNullOrWhiteSpace(points))
            {
                result.Warnings.Add($"Element '{id}' has no points.");
                return;
            }

            List<float> numbers = ReadNumbers(points);
            if (numbers.Count < 6)
            {
                result.Warnings.Add($"Element '{id}' does not contain enough point coordinates.");
                return;
            }

            var loop = new List<Vector2>();
            for (int i = 0; i + 1 < numbers.Count; i += 2)
            {
                loop.Add(new Vector2(numbers[i], numbers[i + 1]));
            }

            loop = CleanLoop(loop);
            if (loop.Count >= 3 && requireClosed)
            {
                result.Regions.Add(CreateRegion(id, loop, paintInfo));
            }
            else if (!requireClosed)
            {
                result.Warnings.Add($"Polyline '{id}' is not closed and was not converted into a region.");
            }
        }

        private static void AddRectRegion(XElement element, string id, SVGPaintInfo paintInfo, FPSVGParseResult result)
        {
            float x = ReadFloat(GetAttribute(element, "x"), 0f);
            float y = ReadFloat(GetAttribute(element, "y"), 0f);
            float width = ReadFloat(GetAttribute(element, "width"), 0f);
            float height = ReadFloat(GetAttribute(element, "height"), 0f);
            if (width <= 0f || height <= 0f)
            {
                result.Warnings.Add($"Rect '{id}' was ignored because its width or height is zero.");
                return;
            }

            result.Regions.Add(CreateRegion(id, new List<Vector2>
            {
                new Vector2(x, y),
                new Vector2(x + width, y),
                new Vector2(x + width, y + height),
                new Vector2(x, y + height)
            }, paintInfo));
        }

        private static void AddEllipseRegion(XElement element, string id, float sampleDistance, SVGPaintInfo paintInfo, FPSVGParseResult result)
        {
            bool circle = element.Name.LocalName == "circle";
            float cx = ReadFloat(GetAttribute(element, "cx"), 0f);
            float cy = ReadFloat(GetAttribute(element, "cy"), 0f);
            float rx = circle ? ReadFloat(GetAttribute(element, "r"), 0f) : ReadFloat(GetAttribute(element, "rx"), 0f);
            float ry = circle ? rx : ReadFloat(GetAttribute(element, "ry"), 0f);
            if (rx <= 0f || ry <= 0f)
            {
                result.Warnings.Add($"Ellipse '{id}' was ignored because its radius is zero.");
                return;
            }

            int segments = Mathf.Clamp(Mathf.CeilToInt((Mathf.PI * 2f * Mathf.Max(rx, ry)) / Mathf.Max(0.01f, sampleDistance)), 12, 256);
            var loop = new List<Vector2>(segments);
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                loop.Add(new Vector2(cx + Mathf.Cos(angle) * rx, cy + Mathf.Sin(angle) * ry));
            }

            result.Regions.Add(CreateRegion(id, loop, paintInfo));
        }

        private static FPSVGRegion CreateRegion(string id, List<Vector2> loop, SVGPaintInfo paintInfo)
        {
            return new FPSVGRegion(id, loop)
            {
                Included = paintInfo.HasFillColor,
                HasFillColor = paintInfo.HasFillColor,
                FillColor = paintInfo.FillColor,
                HasStrokeColor = paintInfo.HasStrokeColor,
                StrokeColor = paintInfo.StrokeColor
            };
        }

        private static void CleanupRegions(FPSVGParseResult result)
        {
            for (int i = result.Regions.Count - 1; i >= 0; i--)
            {
                FPSVGRegion region = result.Regions[i];
                region.OuterLoop = CleanLoop(region.OuterLoop);
                if (region.OuterLoop.Count < 3 || Mathf.Abs(FPSVGRegionDetector.SignedArea(region.OuterLoop)) < 0.0001f)
                {
                    result.Warnings.Add($"Region '{region.Id}' was ignored because it has no usable enclosed area.");
                    result.Regions.RemoveAt(i);
                }
            }
        }

        private static Rect CalculateBounds(IReadOnlyList<FPSVGRegion> regions)
        {
            if (regions == null || regions.Count == 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            bool initialized = false;
            float minX = 0f;
            float minY = 0f;
            float maxX = 0f;
            float maxY = 0f;
            for (int i = 0; i < regions.Count; i++)
            {
                List<Vector2> loop = regions[i].OuterLoop;
                for (int p = 0; p < loop.Count; p++)
                {
                    Vector2 point = loop[p];
                    if (!initialized)
                    {
                        initialized = true;
                        minX = maxX = point.x;
                        minY = maxY = point.y;
                    }
                    else
                    {
                        minX = Mathf.Min(minX, point.x);
                        minY = Mathf.Min(minY, point.y);
                        maxX = Mathf.Max(maxX, point.x);
                        maxY = Mathf.Max(maxY, point.y);
                    }
                }
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static List<Vector2> CleanLoop(IReadOnlyList<Vector2> points)
        {
            var clean = new List<Vector2>();
            if (points == null)
            {
                return clean;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                if (clean.Count == 0 || (clean[clean.Count - 1] - point).sqrMagnitude > 0.000001f)
                {
                    clean.Add(point);
                }
            }

            if (clean.Count > 1 && (clean[0] - clean[clean.Count - 1]).sqrMagnitude <= 0.000001f)
            {
                clean.RemoveAt(clean.Count - 1);
            }

            return clean;
        }

        private static List<float> ReadNumbers(string value)
        {
            return NumberRegex.Matches(value)
                .Cast<Match>()
                .Select(match => ReadFloat(match.Value, 0f))
                .ToList();
        }

        private static string GetAttribute(XElement element, string name)
        {
            XAttribute attribute = element.Attribute(name);
            return attribute == null ? string.Empty : attribute.Value;
        }

        private static SVGPaintInfo ReadPaintInfo(XElement element, IReadOnlyDictionary<string, Dictionary<string, string>> styleClasses)
        {
            var paintInfo = new SVGPaintInfo();
            string fill = GetInheritedPresentationValue(element, "fill", styleClasses);
            string stroke = GetInheritedPresentationValue(element, "stroke", styleClasses);
            float opacity = ReadOpacity(GetInheritedPresentationValue(element, "opacity", styleClasses), 1f);
            float fillOpacity = ReadOpacity(GetInheritedPresentationValue(element, "fill-opacity", styleClasses), 1f);
            float strokeOpacity = ReadOpacity(GetInheritedPresentationValue(element, "stroke-opacity", styleClasses), 1f);

            if (TryReadColor(fill, opacity * fillOpacity, out Color fillColor))
            {
                paintInfo.HasFillColor = true;
                paintInfo.FillColor = fillColor;
            }

            if (TryReadColor(stroke, opacity * strokeOpacity, out Color strokeColor))
            {
                paintInfo.HasStrokeColor = true;
                paintInfo.StrokeColor = strokeColor;
            }

            return paintInfo;
        }

        private static string GetInheritedPresentationValue(
            XElement element,
            string property,
            IReadOnlyDictionary<string, Dictionary<string, string>> styleClasses)
        {
            for (XElement current = element; current != null; current = current.Parent)
            {
                string styleValue = GetStyleValue(GetAttribute(current, "style"), property);
                if (!string.IsNullOrWhiteSpace(styleValue))
                {
                    return styleValue.Trim();
                }

                string directValue = GetAttribute(current, property);
                if (!string.IsNullOrWhiteSpace(directValue))
                {
                    return directValue.Trim();
                }

                string classValue = GetClassStyleValue(current, property, styleClasses);
                if (!string.IsNullOrWhiteSpace(classValue))
                {
                    return classValue.Trim();
                }
            }

            return string.Empty;
        }

        private static Dictionary<string, Dictionary<string, string>> ReadStyleClasses(XDocument document)
        {
            var styleClasses = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (document == null)
            {
                return styleClasses;
            }

            foreach (XElement styleElement in document.Descendants().Where(element => element.Name.LocalName == "style"))
            {
                string css = styleElement.Value;
                foreach (Match match in Regex.Matches(css, @"\.([A-Za-z_][\w-]*)\s*\{([^}]*)\}", RegexOptions.Singleline))
                {
                    string className = match.Groups[1].Value.Trim();
                    Dictionary<string, string> declarations = ParseStyleDeclarations(match.Groups[2].Value);
                    if (string.IsNullOrWhiteSpace(className) || declarations.Count == 0)
                    {
                        continue;
                    }

                    if (!styleClasses.TryGetValue(className, out Dictionary<string, string> existing))
                    {
                        styleClasses[className] = declarations;
                        continue;
                    }

                    foreach (KeyValuePair<string, string> pair in declarations)
                    {
                        existing[pair.Key] = pair.Value;
                    }
                }
            }

            return styleClasses;
        }

        private static Dictionary<string, string> ParseStyleDeclarations(string style)
        {
            var declarationsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(style))
            {
                return declarationsByName;
            }

            string[] declarations = style.Split(';');
            for (int i = 0; i < declarations.Length; i++)
            {
                string declaration = declarations[i];
                int colonIndex = declaration.IndexOf(':');
                if (colonIndex <= 0)
                {
                    continue;
                }

                string key = declaration.Substring(0, colonIndex).Trim();
                string value = declaration.Substring(colonIndex + 1).Trim();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                declarationsByName[key] = value;
            }

            return declarationsByName;
        }

        private static string GetClassStyleValue(
            XElement element,
            string property,
            IReadOnlyDictionary<string, Dictionary<string, string>> styleClasses)
        {
            if (styleClasses == null || styleClasses.Count == 0)
            {
                return string.Empty;
            }

            string classAttribute = GetAttribute(element, "class");
            if (string.IsNullOrWhiteSpace(classAttribute))
            {
                return string.Empty;
            }

            string[] classNames = classAttribute.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = classNames.Length - 1; i >= 0; i--)
            {
                string className = classNames[i].TrimStart('.');
                if (!styleClasses.TryGetValue(className, out Dictionary<string, string> declarations))
                {
                    continue;
                }

                if (declarations.TryGetValue(property, out string value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string GetStyleValue(string style, string property)
        {
            Dictionary<string, string> declarations = ParseStyleDeclarations(style);
            return declarations.TryGetValue(property, out string value) ? value : string.Empty;
        }

        private static float ReadOpacity(string value, float fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            value = value.Trim();
            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                float percent = ReadFloat(value.Substring(0, value.Length - 1), fallback * 100f);
                return Mathf.Clamp01(percent / 100f);
            }

            return Mathf.Clamp01(ReadFloat(value, fallback));
        }

        private static bool TryReadColor(string value, float alphaMultiplier, out Color color)
        {
            color = Color.clear;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            if (value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("transparent", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("url(", StringComparison.OrdinalIgnoreCase) ||
                alphaMultiplier <= 0f)
            {
                return false;
            }

            if (TryReadHexColor(value, alphaMultiplier, out color) ||
                TryReadRgbColor(value, alphaMultiplier, out color) ||
                TryReadNamedColor(value, alphaMultiplier, out color))
            {
                return color.a > 0f;
            }

            return false;
        }

        private static bool TryReadHexColor(string value, float alphaMultiplier, out Color color)
        {
            color = Color.clear;
            if (!value.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            string hex = value.Substring(1);
            if (hex.Length == 3 || hex.Length == 4)
            {
                string expanded = string.Empty;
                for (int i = 0; i < hex.Length; i++)
                {
                    expanded += hex[i].ToString() + hex[i];
                }

                hex = expanded;
            }

            if (hex.Length != 6 && hex.Length != 8)
            {
                return false;
            }

            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed))
            {
                return false;
            }

            float r;
            float g;
            float b;
            float a = 1f;
            if (hex.Length == 8)
            {
                r = ((packed >> 24) & 0xFF) / 255f;
                g = ((packed >> 16) & 0xFF) / 255f;
                b = ((packed >> 8) & 0xFF) / 255f;
                a = (packed & 0xFF) / 255f;
            }
            else
            {
                r = ((packed >> 16) & 0xFF) / 255f;
                g = ((packed >> 8) & 0xFF) / 255f;
                b = (packed & 0xFF) / 255f;
            }

            color = new Color(r, g, b, a * alphaMultiplier);
            return true;
        }

        private static bool TryReadRgbColor(string value, float alphaMultiplier, out Color color)
        {
            color = Color.clear;
            Match match = Regex.Match(value, @"rgba?\(([^)]*)\)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            string[] parts = match.Groups[1].Value.Split(',');
            if (parts.Length < 3)
            {
                return false;
            }

            float r = ReadColorChannel(parts[0]);
            float g = ReadColorChannel(parts[1]);
            float b = ReadColorChannel(parts[2]);
            float a = parts.Length >= 4 ? ReadOpacity(parts[3], 1f) : 1f;
            color = new Color(r, g, b, a * alphaMultiplier);
            return true;
        }

        private static float ReadColorChannel(string value)
        {
            value = value.Trim();
            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                return Mathf.Clamp01(ReadFloat(value.Substring(0, value.Length - 1), 0f) / 100f);
            }

            return Mathf.Clamp01(ReadFloat(value, 0f) / 255f);
        }

        private static bool TryReadNamedColor(string value, float alphaMultiplier, out Color color)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "white":
                    color = Color.white;
                    break;
                case "black":
                    color = Color.black;
                    break;
                case "red":
                    color = Color.red;
                    break;
                case "green":
                    color = Color.green;
                    break;
                case "blue":
                    color = Color.blue;
                    break;
                case "yellow":
                    color = Color.yellow;
                    break;
                case "cyan":
                    color = Color.cyan;
                    break;
                case "magenta":
                    color = Color.magenta;
                    break;
                case "orange":
                    color = new Color(1f, 0.5f, 0f, 1f);
                    break;
                case "gray":
                case "grey":
                    color = Color.gray;
                    break;
                default:
                    color = Color.clear;
                    return false;
            }

            color.a *= alphaMultiplier;
            return true;
        }

        private static float ReadFloat(string value, float fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            value = value.Trim();
            if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 2);
            }

            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallback;
        }

        private struct SVGPaintInfo
        {
            public bool HasFillColor;
            public Color FillColor;
            public bool HasStrokeColor;
            public Color StrokeColor;
        }

        private sealed class PathDataParser
        {
            private readonly List<string> tokens;
            private readonly float sampleDistance;
            private readonly List<string> warnings;
            private readonly string elementId;
            private readonly List<List<Vector2>> contours = new List<List<Vector2>>();
            private List<Vector2> currentContour;
            private int index;
            private Vector2 current;
            private Vector2 contourStart;
            private Vector2 lastCubicControl;
            private Vector2 lastQuadraticControl;
            private char previousCommand;

            public PathDataParser(string data, float sampleDistance, List<string> warnings, string elementId)
            {
                this.tokens = PathTokenRegex.Matches(data).Cast<Match>().Select(match => match.Value).ToList();
                this.sampleDistance = Mathf.Max(0.01f, sampleDistance);
                this.warnings = warnings;
                this.elementId = elementId;
            }

            public List<List<Vector2>> Parse()
            {
                char command = '\0';
                while (index < tokens.Count)
                {
                    if (IsCommand(tokens[index]))
                    {
                        command = tokens[index][0];
                        index++;
                    }
                    else if (command == '\0')
                    {
                        throw new FormatException("Path data starts with coordinate data before a command.");
                    }

                    ExecuteCommand(command);
                }

                CommitOpenContour(false);
                return contours;
            }

            private void ExecuteCommand(char command)
            {
                bool relative = char.IsLower(command);
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        MoveTo(relative);
                        break;
                    case 'L':
                        LineTo(relative);
                        break;
                    case 'H':
                        HorizontalTo(relative);
                        break;
                    case 'V':
                        VerticalTo(relative);
                        break;
                    case 'C':
                        CubicTo(relative);
                        break;
                    case 'S':
                        SmoothCubicTo(relative);
                        break;
                    case 'Q':
                        QuadraticTo(relative);
                        break;
                    case 'T':
                        SmoothQuadraticTo(relative);
                        break;
                    case 'A':
                        ArcTo(relative);
                        break;
                    case 'Z':
                        CloseContour();
                        break;
                    default:
                        throw new FormatException($"Unsupported SVG path command '{command}'.");
                }

                previousCommand = command;
            }

            private void MoveTo(bool relative)
            {
                Vector2 point = ReadPoint(relative);
                CommitOpenContour(false);
                currentContour = new List<Vector2>();
                current = point;
                contourStart = point;
                currentContour.Add(point);

                while (HasNumber())
                {
                    point = ReadPoint(relative);
                    AddPoint(point);
                }
            }

            private void LineTo(bool relative)
            {
                while (HasNumber())
                {
                    AddPoint(ReadPoint(relative));
                }
            }

            private void HorizontalTo(bool relative)
            {
                while (HasNumber())
                {
                    float x = ReadNumber();
                    AddPoint(relative ? new Vector2(current.x + x, current.y) : new Vector2(x, current.y));
                }
            }

            private void VerticalTo(bool relative)
            {
                while (HasNumber())
                {
                    float y = ReadNumber();
                    AddPoint(relative ? new Vector2(current.x, current.y + y) : new Vector2(current.x, y));
                }
            }

            private void CubicTo(bool relative)
            {
                while (HasNumber())
                {
                    Vector2 c1 = ReadPoint(relative);
                    Vector2 c2 = ReadPoint(relative);
                    Vector2 end = ReadPoint(relative);
                    AddCubic(current, c1, c2, end);
                    lastCubicControl = c2;
                }
            }

            private void SmoothCubicTo(bool relative)
            {
                while (HasNumber())
                {
                    Vector2 c1 = char.ToUpperInvariant(previousCommand) == 'C' || char.ToUpperInvariant(previousCommand) == 'S'
                        ? current + (current - lastCubicControl)
                        : current;
                    Vector2 c2 = ReadPoint(relative);
                    Vector2 end = ReadPoint(relative);
                    AddCubic(current, c1, c2, end);
                    lastCubicControl = c2;
                }
            }

            private void QuadraticTo(bool relative)
            {
                while (HasNumber())
                {
                    Vector2 c = ReadPoint(relative);
                    Vector2 end = ReadPoint(relative);
                    AddQuadratic(current, c, end);
                    lastQuadraticControl = c;
                }
            }

            private void SmoothQuadraticTo(bool relative)
            {
                while (HasNumber())
                {
                    Vector2 c = char.ToUpperInvariant(previousCommand) == 'Q' || char.ToUpperInvariant(previousCommand) == 'T'
                        ? current + (current - lastQuadraticControl)
                        : current;
                    Vector2 end = ReadPoint(relative);
                    AddQuadratic(current, c, end);
                    lastQuadraticControl = c;
                }
            }

            private void ArcTo(bool relative)
            {
                while (HasNumber())
                {
                    ReadNumber();
                    ReadNumber();
                    ReadNumber();
                    ReadNumber();
                    ReadNumber();
                    Vector2 end = ReadPoint(relative);
                    warnings.Add($"Path '{elementId}' contains an arc command. Arc segments are approximated as straight lines in this first pass.");
                    AddPoint(end);
                }
            }

            private void CloseContour()
            {
                if (currentContour == null || currentContour.Count == 0)
                {
                    return;
                }

                if ((current - contourStart).sqrMagnitude > 0.000001f)
                {
                    AddPoint(contourStart);
                }

                CommitOpenContour(true);
                current = contourStart;
            }

            private void AddCubic(Vector2 start, Vector2 c1, Vector2 c2, Vector2 end)
            {
                int steps = CurveSteps(start, c1, c2, end);
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float inv = 1f - t;
                    Vector2 point =
                        (inv * inv * inv * start) +
                        (3f * inv * inv * t * c1) +
                        (3f * inv * t * t * c2) +
                        (t * t * t * end);
                    AddPoint(point);
                }
            }

            private void AddQuadratic(Vector2 start, Vector2 c, Vector2 end)
            {
                int steps = CurveSteps(start, c, end);
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float inv = 1f - t;
                    Vector2 point = (inv * inv * start) + (2f * inv * t * c) + (t * t * end);
                    AddPoint(point);
                }
            }

            private int CurveSteps(params Vector2[] points)
            {
                float length = 0f;
                for (int i = 1; i < points.Length; i++)
                {
                    length += Vector2.Distance(points[i - 1], points[i]);
                }

                return Mathf.Clamp(Mathf.CeilToInt(length / sampleDistance), 4, 128);
            }

            private Vector2 ReadPoint(bool relative)
            {
                float x = ReadNumber();
                float y = ReadNumber();
                Vector2 point = new Vector2(x, y);
                return relative ? current + point : point;
            }

            private float ReadNumber()
            {
                if (!HasNumber())
                {
                    throw new FormatException("Expected a numeric path value.");
                }

                string token = tokens[index++];
                if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    throw new FormatException($"Invalid numeric path value '{token}'.");
                }

                return value;
            }

            private bool HasNumber()
            {
                return index < tokens.Count && !IsCommand(tokens[index]);
            }

            private static bool IsCommand(string token)
            {
                return token.Length == 1 && char.IsLetter(token[0]);
            }

            private void AddPoint(Vector2 point)
            {
                if (currentContour == null)
                {
                    currentContour = new List<Vector2>();
                    contourStart = point;
                }

                currentContour.Add(point);
                current = point;
            }

            private void CommitOpenContour(bool closed)
            {
                if (currentContour == null || currentContour.Count == 0)
                {
                    currentContour = null;
                    return;
                }

                if (closed || currentContour.Count >= 3 && (currentContour[0] - currentContour[currentContour.Count - 1]).sqrMagnitude <= 0.000001f)
                {
                    contours.Add(currentContour);
                }
                else
                {
                    warnings.Add($"Path '{elementId}' has an open contour that was ignored.");
                }

                currentContour = null;
            }
        }
    }
}
