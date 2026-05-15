namespace FuzzPhyte.Utility.Editor
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;

    public static class FPSVGRegionPreview
    {
        private const float Padding = 12f;
        private static readonly List<PreviewTriangle> CachedTriangles = new List<PreviewTriangle>();
        private static int cachedSignature;
        private static GUIStyle cachedLabelStyle;

        public static bool Draw(
            Rect rect,
            IReadOnlyList<FPSVGRegion> regions,
            Rect svgBounds,
            Color labelColor,
            Color selectionColor,
            out int clickedRegionIndex)
        {
            clickedRegionIndex = -1;
            EditorGUI.DrawRect(rect, FP_Utility_Editor.UnityEditorDarkGrey);

            if (regions == null || regions.Count == 0)
            {
                GUI.Label(rect, "No closed SVG regions parsed.", EditorStyles.centeredGreyMiniLabel);
                return false;
            }

            PreviewTransform transform = PreviewTransform.Create(rect, svgBounds);
            Handles.BeginGUI();
            DrawSelectedFills(regions, transform, BuildSignature(regions, selectionColor), selectionColor);
            DrawOutlines(regions, transform, labelColor);
            Handles.EndGUI();

            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                Vector2 svgPoint = transform.GuiToSvg(current.mousePosition);
                clickedRegionIndex = FPSVGRegionDetector.FindRegionAtPoint(svgPoint, regions);
                if (clickedRegionIndex >= 0)
                {
                    current.Use();
                    return true;
                }
            }

            return false;
        }

        private static void DrawSelectedFills(IReadOnlyList<FPSVGRegion> regions, PreviewTransform transform, int signature, Color selectionColor)
        {
            if (signature != cachedSignature)
            {
                RebuildFillCache(regions, signature, selectionColor);
            }

            for (int i = 0; i < CachedTriangles.Count; i++)
            {
                PreviewTriangle triangle = CachedTriangles[i];
                Handles.color = triangle.FillColor;
                Handles.DrawAAConvexPolygon(
                    transform.SvgToGui(triangle.A),
                    transform.SvgToGui(triangle.B),
                    transform.SvgToGui(triangle.C));
            }
        }

        private static void RebuildFillCache(IReadOnlyList<FPSVGRegion> regions, int signature, Color selectionColor)
        {
            CachedTriangles.Clear();
            cachedSignature = signature;

            List<FPSVGRegion> solids = FPSVGRegionDetector.BuildSolidRegions(regions);
            for (int i = 0; i < solids.Count; i++)
            {
                FPSVGRegion solid = solids[i];
                Color previewFill = solid.HasFillColor
                    ? new Color(solid.FillColor.r, solid.FillColor.g, solid.FillColor.b, Mathf.Clamp(solid.FillColor.a, 0.18f, 0.38f))
                    : new Color(selectionColor.r, selectionColor.g, selectionColor.b, Mathf.Clamp(selectionColor.a, 0.18f, 0.38f));
                List<Vector2> outer = FlipSvgY(solid.OuterLoop);
                var holes = new List<List<Vector2>>();
                for (int h = 0; h < solid.Holes.Count; h++)
                {
                    holes.Add(FlipSvgY(solid.Holes[h]));
                }

                FPSVGTriangulation triangulation = FPSVGPolygonTriangulator.Triangulate(outer, holes);
                for (int t = 0; t < triangulation.Triangles.Count; t += 3)
                {
                    Vector2 a = UnflipSvgY(triangulation.Vertices[triangulation.Triangles[t]]);
                    Vector2 b = UnflipSvgY(triangulation.Vertices[triangulation.Triangles[t + 1]]);
                    Vector2 c = UnflipSvgY(triangulation.Vertices[triangulation.Triangles[t + 2]]);
                    CachedTriangles.Add(new PreviewTriangle(a, b, c, previewFill));
                }
            }
        }

        private static void DrawOutlines(IReadOnlyList<FPSVGRegion> regions, PreviewTransform transform, Color labelColor)
        {
            GUIStyle labelStyle = GetLabelStyle(labelColor);
            for (int i = 0; i < regions.Count; i++)
            {
                FPSVGRegion region = regions[i];
                Color color = region.Included ? FP_Utility_Editor.OkayColor : new Color(0.85f, 0.85f, 0.85f, 0.92f);
                Handles.color = color;
                DrawLoop(region.OuterLoop, transform, region.Included ? 3f : 1.5f);

                if (region.OuterLoop.Count > 0)
                {
                    Vector2 labelPoint = transform.SvgToGui(region.OuterLoop[0]);
                    GUI.Label(new Rect(labelPoint.x + 3f, labelPoint.y + 3f, 180f, 18f), region.Id, labelStyle);
                }
            }
        }

        private static void DrawLoop(IReadOnlyList<Vector2> loop, PreviewTransform transform, float width)
        {
            if (loop == null || loop.Count < 2)
            {
                return;
            }

            var points = new Vector3[loop.Count + 1];
            for (int i = 0; i < loop.Count; i++)
            {
                points[i] = transform.SvgToGui(loop[i]);
            }

            points[points.Length - 1] = points[0];
            Handles.DrawAAPolyLine(width, points);
        }

        private static List<Vector2> FlipSvgY(IReadOnlyList<Vector2> loop)
        {
            var flipped = new List<Vector2>();
            if (loop == null)
            {
                return flipped;
            }

            for (int i = 0; i < loop.Count; i++)
            {
                flipped.Add(new Vector2(loop[i].x, -loop[i].y));
            }

            return flipped;
        }

        private static Vector2 UnflipSvgY(Vector2 point)
        {
            return new Vector2(point.x, -point.y);
        }

        private static int BuildSignature(IReadOnlyList<FPSVGRegion> regions, Color selectionColor)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Mathf.RoundToInt(selectionColor.r * 255f);
                hash = (hash * 31) + Mathf.RoundToInt(selectionColor.g * 255f);
                hash = (hash * 31) + Mathf.RoundToInt(selectionColor.b * 255f);
                hash = (hash * 31) + Mathf.RoundToInt(selectionColor.a * 255f);
                if (regions == null)
                {
                    return hash;
                }

                hash = (hash * 31) + regions.Count;
                for (int r = 0; r < regions.Count; r++)
                {
                    FPSVGRegion region = regions[r];
                    hash = (hash * 31) + (region.Included ? 1 : 0);
                    hash = (hash * 31) + (region.HasFillColor ? 1 : 0);
                    hash = (hash * 31) + Mathf.RoundToInt(region.FillColor.r * 255f);
                    hash = (hash * 31) + Mathf.RoundToInt(region.FillColor.g * 255f);
                    hash = (hash * 31) + Mathf.RoundToInt(region.FillColor.b * 255f);
                    hash = (hash * 31) + Mathf.RoundToInt(region.FillColor.a * 255f);
                    hash = AddLoopToHash(hash, region.OuterLoop);
                }

                return hash;
            }
        }

        private static int AddLoopToHash(int hash, IReadOnlyList<Vector2> loop)
        {
            unchecked
            {
                if (loop == null)
                {
                    return hash * 31;
                }

                hash = (hash * 31) + loop.Count;
                for (int i = 0; i < loop.Count; i++)
                {
                    hash = (hash * 31) + Mathf.RoundToInt(loop[i].x * 1000f);
                    hash = (hash * 31) + Mathf.RoundToInt(loop[i].y * 1000f);
                }

                return hash;
            }
        }

        private static GUIStyle GetLabelStyle(Color labelColor)
        {
            cachedLabelStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft
            };

            cachedLabelStyle.normal.textColor = labelColor;
            return cachedLabelStyle;
        }

        private readonly struct PreviewTriangle
        {
            public readonly Vector2 A;
            public readonly Vector2 B;
            public readonly Vector2 C;
            public readonly Color FillColor;

            public PreviewTriangle(Vector2 a, Vector2 b, Vector2 c, Color fillColor)
            {
                A = a;
                B = b;
                C = c;
                FillColor = fillColor;
            }
        }

        private readonly struct PreviewTransform
        {
            private readonly Rect rect;
            private readonly Rect bounds;
            private readonly float scale;
            private readonly Vector2 offset;

            private PreviewTransform(Rect rect, Rect bounds, float scale, Vector2 offset)
            {
                this.rect = rect;
                this.bounds = bounds;
                this.scale = scale;
                this.offset = offset;
            }

            public static PreviewTransform Create(Rect rect, Rect bounds)
            {
                float width = Mathf.Max(0.0001f, bounds.width);
                float height = Mathf.Max(0.0001f, bounds.height);
                float scale = Mathf.Min((rect.width - Padding * 2f) / width, (rect.height - Padding * 2f) / height);
                scale = Mathf.Max(0.0001f, scale);
                Vector2 drawnSize = new Vector2(width * scale, height * scale);
                Vector2 offset = new Vector2((rect.width - drawnSize.x) * 0.5f, (rect.height - drawnSize.y) * 0.5f);
                return new PreviewTransform(rect, bounds, scale, offset);
            }

            public Vector2 SvgToGui(Vector2 point)
            {
                return new Vector2(
                    rect.x + offset.x + ((point.x - bounds.xMin) * scale),
                    rect.y + offset.y + ((point.y - bounds.yMin) * scale));
            }

            public Vector2 GuiToSvg(Vector2 point)
            {
                return new Vector2(
                    ((point.x - rect.x - offset.x) / scale) + bounds.xMin,
                    ((point.y - rect.y - offset.y) / scale) + bounds.yMin);
            }
        }
    }
}
