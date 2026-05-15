namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;

    public class FPSVGTriangulation
    {
        public readonly List<Vector2> Vertices = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<int> BridgedHoleIndices = new List<int>();
    }

    public static class FPSVGPolygonTriangulator
    {
        private const float Epsilon = 0.000001f;

        public static FPSVGTriangulation Triangulate(
            IReadOnlyList<Vector2> outerLoop,
            IReadOnlyList<List<Vector2>> holes,
            List<string> warnings = null,
            string regionId = null,
            bool optimizeTriangles = false,
            int optimizationPasses = 8,
            bool useZOrderEarSearch = false)
        {
            var triangulation = new FPSVGTriangulation();
            if (outerLoop == null || outerLoop.Count < 3)
            {
                warnings?.Add($"Region '{regionId}' has no valid outer loop.");
                return triangulation;
            }

            List<Vector2> polygon = CopyWithWinding(outerLoop, true);
            if (holes != null)
            {
                for (int i = 0; i < holes.Count; i++)
                {
                    List<Vector2> hole = CopyWithWinding(holes[i], false);
                    if (hole.Count < 3)
                    {
                        continue;
                    }

                    if (TryBridgeHole(ref polygon, hole))
                    {
                        triangulation.BridgedHoleIndices.Add(i);
                    }
                    else
                    {
                        warnings?.Add($"Region '{regionId}' hole {i} could not be bridged for triangulation and was skipped.");
                    }
                }
            }

            triangulation.Vertices.AddRange(polygon);
            EarClip(polygon, triangulation.Triangles, warnings, regionId, useZOrderEarSearch);
            if (optimizeTriangles && triangulation.Triangles.Count >= 6)
            {
                OptimizeInternalEdges(polygon, triangulation.Triangles, Mathf.Max(0, optimizationPasses));
            }

            return triangulation;
        }

        private static List<Vector2> CopyWithWinding(IReadOnlyList<Vector2> loop, bool counterClockwise)
        {
            var copy = new List<Vector2>(loop);
            bool isCounterClockwise = FPSVGRegionDetector.SignedArea(copy) > 0f;
            if (isCounterClockwise != counterClockwise)
            {
                copy.Reverse();
            }

            return copy;
        }

        private static bool TryBridgeHole(ref List<Vector2> outer, List<Vector2> hole)
        {
            int holeIndex = FindRightmostIndex(hole);
            Vector2 holePoint = hole[holeIndex];
            int outerIndex = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < outer.Count; i++)
            {
                Vector2 outerPoint = outer[i];
                if (!IsBridgeVisible(holePoint, outerPoint, outer, hole))
                {
                    continue;
                }

                float distance = (outerPoint - holePoint).sqrMagnitude;
                if (outerPoint.x < holePoint.x)
                {
                    distance *= 4f;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    outerIndex = i;
                }
            }

            if (outerIndex < 0)
            {
                return false;
            }

            var merged = new List<Vector2>(outer.Count + hole.Count + 2);
            for (int i = 0; i <= outerIndex; i++)
            {
                merged.Add(outer[i]);
            }

            for (int i = 0; i <= hole.Count; i++)
            {
                merged.Add(hole[(holeIndex + i) % hole.Count]);
            }

            merged.Add(outer[outerIndex]);
            for (int i = outerIndex + 1; i < outer.Count; i++)
            {
                merged.Add(outer[i]);
            }

            outer = merged;
            return true;
        }

        private static int FindRightmostIndex(IReadOnlyList<Vector2> loop)
        {
            int best = 0;
            for (int i = 1; i < loop.Count; i++)
            {
                if (loop[i].x > loop[best].x || Mathf.Approximately(loop[i].x, loop[best].x) && loop[i].y < loop[best].y)
                {
                    best = i;
                }
            }

            return best;
        }

        private static bool IsBridgeVisible(Vector2 a, Vector2 b, IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> hole)
        {
            Vector2 middle = (a + b) * 0.5f;
            if (!FPSVGRegionDetector.PointInPolygon(middle, outer))
            {
                return false;
            }

            if (FPSVGRegionDetector.PointInPolygon(middle, hole))
            {
                return false;
            }

            if (IntersectsAnyEdge(a, b, outer) || IntersectsAnyEdge(a, b, hole))
            {
                return false;
            }

            return true;
        }

        private static bool IntersectsAnyEdge(Vector2 a, Vector2 b, IReadOnlyList<Vector2> loop)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 c = loop[i];
                Vector2 d = loop[(i + 1) % loop.Count];
                if (SamePoint(a, c) || SamePoint(a, d) || SamePoint(b, c) || SamePoint(b, d))
                {
                    continue;
                }

                if (SegmentsIntersect(a, b, c, d))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EarClip(
            IReadOnlyList<Vector2> polygon,
            List<int> triangles,
            List<string> warnings,
            string regionId,
            bool useZOrderEarSearch)
        {
            var indices = new List<int>(polygon.Count);
            bool[] active = new bool[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
            {
                indices.Add(i);
                active[i] = true;
            }

            ZOrderIndex zOrderIndex = useZOrderEarSearch ? new ZOrderIndex(polygon) : null;
            int guard = polygon.Count * polygon.Count;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int previousIndex = indices[(i - 1 + indices.Count) % indices.Count];
                    int currentIndex = indices[i];
                    int nextIndex = indices[(i + 1) % indices.Count];

                    if (!IsEar(previousIndex, currentIndex, nextIndex, polygon, indices, active, zOrderIndex))
                    {
                        continue;
                    }

                    triangles.Add(previousIndex);
                    triangles.Add(currentIndex);
                    triangles.Add(nextIndex);
                    active[currentIndex] = false;
                    indices.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    warnings?.Add($"Region '{regionId}' triangulation stopped early. The polygon may self-intersect or contain an unsupported hole configuration.");
                    return;
                }
            }

            if (indices.Count == 3)
            {
                Vector2 a = polygon[indices[0]];
                Vector2 b = polygon[indices[1]];
                Vector2 c = polygon[indices[2]];
                if (Cross(a, b, c) > Epsilon)
                {
                    triangles.Add(indices[0]);
                    triangles.Add(indices[1]);
                    triangles.Add(indices[2]);
                }
            }
        }

        private static bool IsEar(
            int previousIndex,
            int currentIndex,
            int nextIndex,
            IReadOnlyList<Vector2> polygon,
            IReadOnlyList<int> liveIndices,
            bool[] active,
            ZOrderIndex zOrderIndex)
        {
            Vector2 previous = polygon[previousIndex];
            Vector2 current = polygon[currentIndex];
            Vector2 next = polygon[nextIndex];
            if (SamePoint(previous, current) || SamePoint(current, next) || Cross(previous, current, next) <= Epsilon)
            {
                return false;
            }

            if (zOrderIndex != null)
            {
                if (ContainsAnyPointInTriangle(previous, current, next, polygon, active, previousIndex, currentIndex, nextIndex, zOrderIndex))
                {
                    return false;
                }
            }
            else
            {
                for (int i = 0; i < liveIndices.Count; i++)
                {
                    int testIndex = liveIndices[i];
                    if (testIndex == previousIndex || testIndex == currentIndex || testIndex == nextIndex)
                    {
                        continue;
                    }

                    Vector2 point = polygon[testIndex];
                    if (SamePoint(point, previous) || SamePoint(point, current) || SamePoint(point, next))
                    {
                        continue;
                    }

                    if (PointInTriangle(point, previous, current, next))
                    {
                        return false;
                    }
                }
            }

            for (int i = 0; i < liveIndices.Count; i++)
            {
                int edgeAIndex = liveIndices[i];
                int edgeBIndex = liveIndices[(i + 1) % liveIndices.Count];
                if (edgeAIndex == previousIndex || edgeAIndex == currentIndex || edgeAIndex == nextIndex ||
                    edgeBIndex == previousIndex || edgeBIndex == currentIndex || edgeBIndex == nextIndex)
                {
                    continue;
                }

                if (SegmentsIntersect(previous, next, polygon[edgeAIndex], polygon[edgeBIndex]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsAnyPointInTriangle(
            Vector2 a,
            Vector2 b,
            Vector2 c,
            IReadOnlyList<Vector2> polygon,
            bool[] active,
            int aIndex,
            int bIndex,
            int cIndex,
            ZOrderIndex zOrderIndex)
        {
            Rect bounds = Rect.MinMaxRect(
                Mathf.Min(a.x, Mathf.Min(b.x, c.x)),
                Mathf.Min(a.y, Mathf.Min(b.y, c.y)),
                Mathf.Max(a.x, Mathf.Max(b.x, c.x)),
                Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            List<int> candidates = zOrderIndex.Query(bounds);
            for (int i = 0; i < candidates.Count; i++)
            {
                int testIndex = candidates[i];
                if (!active[testIndex] || testIndex == aIndex || testIndex == bIndex || testIndex == cIndex)
                {
                    continue;
                }

                Vector2 point = polygon[testIndex];
                if (SamePoint(point, a) || SamePoint(point, b) || SamePoint(point, c))
                {
                    continue;
                }

                if (PointInTriangle(point, a, b, c))
                {
                    return true;
                }
            }

            return false;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 ab = b - a;
            Vector2 ac = c - a;
            return (ab.x * ac.y) - (ab.y * ac.x);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float c1 = Cross(a, b, p);
            float c2 = Cross(b, c, p);
            float c3 = Cross(c, a, p);
            return c1 >= -Epsilon && c2 >= -Epsilon && c3 >= -Epsilon;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(a, b, c);
            float d2 = Cross(a, b, d);
            float d3 = Cross(c, d, a);
            float d4 = Cross(c, d, b);
            return d1 * d2 < -Epsilon && d3 * d4 < -Epsilon;
        }

        private static bool SamePoint(Vector2 a, Vector2 b)
        {
            return (a - b).sqrMagnitude <= Epsilon;
        }

        private static void OptimizeInternalEdges(IReadOnlyList<Vector2> polygon, List<int> triangles, int passes)
        {
            if (passes <= 0)
            {
                return;
            }

            for (int pass = 0; pass < passes; pass++)
            {
                bool changed = false;
                Dictionary<EdgeKey, List<TriangleEdge>> edgeMap = BuildEdgeMap(triangles);
                foreach (KeyValuePair<EdgeKey, List<TriangleEdge>> pair in edgeMap)
                {
                    List<TriangleEdge> shared = pair.Value;
                    if (shared.Count != 2)
                    {
                        continue;
                    }

                    if (TryFlipSharedEdge(polygon, triangles, shared[0], shared[1]))
                    {
                        changed = true;
                        break;
                    }
                }

                if (!changed)
                {
                    return;
                }
            }
        }

        private static Dictionary<EdgeKey, List<TriangleEdge>> BuildEdgeMap(IReadOnlyList<int> triangles)
        {
            var edgeMap = new Dictionary<EdgeKey, List<TriangleEdge>>();
            for (int t = 0; t < triangles.Count; t += 3)
            {
                AddEdge(edgeMap, triangles[t], triangles[t + 1], triangles[t + 2], t);
                AddEdge(edgeMap, triangles[t + 1], triangles[t + 2], triangles[t], t);
                AddEdge(edgeMap, triangles[t + 2], triangles[t], triangles[t + 1], t);
            }

            return edgeMap;
        }

        private static void AddEdge(Dictionary<EdgeKey, List<TriangleEdge>> edgeMap, int a, int b, int opposite, int triangleStart)
        {
            var key = new EdgeKey(a, b);
            if (!edgeMap.TryGetValue(key, out List<TriangleEdge> edges))
            {
                edges = new List<TriangleEdge>(2);
                edgeMap[key] = edges;
            }

            edges.Add(new TriangleEdge(a, b, opposite, triangleStart));
        }

        private static bool TryFlipSharedEdge(
            IReadOnlyList<Vector2> polygon,
            List<int> triangles,
            TriangleEdge first,
            TriangleEdge second)
        {
            int a = first.A;
            int b = first.B;
            int c = first.Opposite;
            int d = second.Opposite;
            if (c == d || a == b || SamePoint(polygon[c], polygon[d]))
            {
                return false;
            }

            Vector2 pa = polygon[a];
            Vector2 pb = polygon[b];
            Vector2 pc = polygon[c];
            Vector2 pd = polygon[d];
            if (!IsConvexQuad(pc, pa, pd, pb))
            {
                return false;
            }

            float currentQuality = Mathf.Min(TriangleQuality(pa, pc, pb), TriangleQuality(pa, pb, pd));
            float flippedQuality = Mathf.Min(TriangleQuality(pc, pd, pa), TriangleQuality(pd, pc, pb));
            if (flippedQuality <= currentQuality + Epsilon)
            {
                return false;
            }

            WriteTriangle(triangles, first.TriangleStart, c, d, a, polygon);
            WriteTriangle(triangles, second.TriangleStart, d, c, b, polygon);
            return true;
        }

        private static bool IsConvexQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float c1 = Cross(a, b, c);
            float c2 = Cross(b, c, d);
            float c3 = Cross(c, d, a);
            float c4 = Cross(d, a, b);
            bool allPositive = c1 > Epsilon && c2 > Epsilon && c3 > Epsilon && c4 > Epsilon;
            bool allNegative = c1 < -Epsilon && c2 < -Epsilon && c3 < -Epsilon && c4 < -Epsilon;
            return allPositive || allNegative;
        }

        private static float TriangleQuality(Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = (a - b).sqrMagnitude;
            float bc = (b - c).sqrMagnitude;
            float ca = (c - a).sqrMagnitude;
            float longest = Mathf.Max(ab, Mathf.Max(bc, ca));
            if (longest <= Epsilon)
            {
                return 0f;
            }

            return Mathf.Abs(Cross(a, b, c)) / longest;
        }

        private static void WriteTriangle(List<int> triangles, int start, int a, int b, int c, IReadOnlyList<Vector2> points)
        {
            if (Cross(points[a], points[b], points[c]) < 0f)
            {
                triangles[start] = a;
                triangles[start + 1] = c;
                triangles[start + 2] = b;
                return;
            }

            triangles[start] = a;
            triangles[start + 1] = b;
            triangles[start + 2] = c;
        }

        private readonly struct EdgeKey
        {
            private readonly int min;
            private readonly int max;

            public EdgeKey(int a, int b)
            {
                min = Mathf.Min(a, b);
                max = Mathf.Max(a, b);
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey other && min == other.min && max == other.max;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (min * 397) ^ max;
                }
            }
        }

        private readonly struct TriangleEdge
        {
            public readonly int A;
            public readonly int B;
            public readonly int Opposite;
            public readonly int TriangleStart;

            public TriangleEdge(int a, int b, int opposite, int triangleStart)
            {
                A = a;
                B = b;
                Opposite = opposite;
                TriangleStart = triangleStart;
            }
        }

        private sealed class ZOrderIndex
        {
            private const int QuantizationMax = 65535;
            private readonly List<ZOrderEntry> entries = new List<ZOrderEntry>();
            private readonly IReadOnlyList<Vector2> points;
            private readonly Rect bounds;

            public ZOrderIndex(IReadOnlyList<Vector2> points)
            {
                this.points = points;
                bounds = CalculateBounds(points);
                for (int i = 0; i < points.Count; i++)
                {
                    entries.Add(new ZOrderEntry(i, MortonCode(points[i])));
                }

                entries.Sort((a, b) => a.Code.CompareTo(b.Code));
            }

            public List<int> Query(Rect queryBounds)
            {
                uint minCode = MortonCode(new Vector2(queryBounds.xMin, queryBounds.yMin));
                uint maxCode = MortonCode(new Vector2(queryBounds.xMax, queryBounds.yMax));
                if (minCode > maxCode)
                {
                    uint swap = minCode;
                    minCode = maxCode;
                    maxCode = swap;
                }

                int start = LowerBound(minCode);
                var candidates = new List<int>();
                for (int i = start; i < entries.Count && entries[i].Code <= maxCode; i++)
                {
                    Vector2 point = points[entries[i].Index];
                    if (point.x >= queryBounds.xMin - Epsilon &&
                        point.x <= queryBounds.xMax + Epsilon &&
                        point.y >= queryBounds.yMin - Epsilon &&
                        point.y <= queryBounds.yMax + Epsilon)
                    {
                        candidates.Add(entries[i].Index);
                    }
                }

                return candidates;
            }

            private int LowerBound(uint code)
            {
                int low = 0;
                int high = entries.Count;
                while (low < high)
                {
                    int mid = (low + high) >> 1;
                    if (entries[mid].Code < code)
                    {
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                return low;
            }

            private uint MortonCode(Vector2 point)
            {
                ushort x = Quantize(point.x, bounds.xMin, bounds.xMax);
                ushort y = Quantize(point.y, bounds.yMin, bounds.yMax);
                return InterleaveBits(x, y);
            }

            private static ushort Quantize(float value, float min, float max)
            {
                if (Mathf.Abs(max - min) <= Epsilon)
                {
                    return 0;
                }

                return (ushort)Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(min, max, value) * QuantizationMax), 0, QuantizationMax);
            }

            private static uint InterleaveBits(ushort x, ushort y)
            {
                uint xx = Part1By1(x);
                uint yy = Part1By1(y);
                return xx | (yy << 1);
            }

            private static uint Part1By1(uint value)
            {
                value &= 0x0000ffff;
                value = (value | (value << 8)) & 0x00FF00FF;
                value = (value | (value << 4)) & 0x0F0F0F0F;
                value = (value | (value << 2)) & 0x33333333;
                value = (value | (value << 1)) & 0x55555555;
                return value;
            }

            private static Rect CalculateBounds(IReadOnlyList<Vector2> points)
            {
                if (points == null || points.Count == 0)
                {
                    return new Rect(0f, 0f, 1f, 1f);
                }

                float minX = points[0].x;
                float minY = points[0].y;
                float maxX = points[0].x;
                float maxY = points[0].y;
                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 point = points[i];
                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxX = Mathf.Max(maxX, point.x);
                    maxY = Mathf.Max(maxY, point.y);
                }

                return Rect.MinMaxRect(minX, minY, maxX, maxY);
            }
        }

        private readonly struct ZOrderEntry
        {
            public readonly int Index;
            public readonly uint Code;

            public ZOrderEntry(int index, uint code)
            {
                Index = index;
                Code = code;
            }
        }
    }
}
