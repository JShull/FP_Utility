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
            string regionId = null)
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
            EarClip(polygon, triangulation.Triangles, warnings, regionId);
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

        private static void EarClip(IReadOnlyList<Vector2> polygon, List<int> triangles, List<string> warnings, string regionId)
        {
            var indices = new List<int>(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
            {
                indices.Add(i);
            }

            int guard = polygon.Count * polygon.Count;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int previousIndex = indices[(i - 1 + indices.Count) % indices.Count];
                    int currentIndex = indices[i];
                    int nextIndex = indices[(i + 1) % indices.Count];

                    if (!IsEar(previousIndex, currentIndex, nextIndex, polygon, indices))
                    {
                        continue;
                    }

                    triangles.Add(previousIndex);
                    triangles.Add(currentIndex);
                    triangles.Add(nextIndex);
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

        private static bool IsEar(int previousIndex, int currentIndex, int nextIndex, IReadOnlyList<Vector2> polygon, IReadOnlyList<int> liveIndices)
        {
            Vector2 previous = polygon[previousIndex];
            Vector2 current = polygon[currentIndex];
            Vector2 next = polygon[nextIndex];
            if (SamePoint(previous, current) || SamePoint(current, next) || Cross(previous, current, next) <= Epsilon)
            {
                return false;
            }

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
    }
}
