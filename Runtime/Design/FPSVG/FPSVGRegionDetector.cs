namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;

    public static class FPSVGRegionDetector
    {
        private const float Epsilon = 0.000001f;

        public static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            bool inside = false;
            int previous = polygon.Count - 1;
            for (int current = 0; current < polygon.Count; current++)
            {
                Vector2 a = polygon[current];
                Vector2 b = polygon[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses)
                {
                    float x = ((b.x - a.x) * (point.y - a.y) / (b.y - a.y)) + a.x;
                    if (point.x < x)
                    {
                        inside = !inside;
                    }
                }

                previous = current;
            }

            return inside;
        }

        public static bool PointInPolygonInclusive(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                if (PointOnSegment(point, a, b))
                {
                    return true;
                }
            }

            return PointInPolygon(point, polygon);
        }

        public static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return 0f;
            }

            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                area += (a.x * b.y) - (b.x * a.y);
            }

            return area * 0.5f;
        }

        public static int FindRegionAtPoint(Vector2 point, IReadOnlyList<FPSVGRegion> regions)
        {
            int bestIndex = -1;
            float bestArea = float.MaxValue;
            for (int i = 0; i < regions.Count; i++)
            {
                FPSVGRegion region = regions[i];
                if (!PointInPolygon(point, region.OuterLoop))
                {
                    continue;
                }

                float area = Mathf.Abs(SignedArea(region.OuterLoop));
                if (area < bestArea)
                {
                    bestArea = area;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public static List<FPSVGRegion> BuildSolidRegions(IReadOnlyList<FPSVGRegion> sourceRegions)
        {
            var solids = new List<FPSVGRegion>();
            if (sourceRegions == null || sourceRegions.Count == 0)
            {
                return solids;
            }

            int[] parents = BuildParentMap(sourceRegions);
            for (int i = 0; i < sourceRegions.Count; i++)
            {
                FPSVGRegion source = sourceRegions[i];
                if (!source.Included)
                {
                    continue;
                }

                FPSVGRegion solid = source.CloneWithoutHoles();
                for (int child = 0; child < sourceRegions.Count; child++)
                {
                    if (parents[child] == i && !sourceRegions[child].Included)
                    {
                        solid.Holes.Add(new List<Vector2>(sourceRegions[child].OuterLoop));
                    }
                }

                solids.Add(solid);
            }

            return solids;
        }

        private static int[] BuildParentMap(IReadOnlyList<FPSVGRegion> regions)
        {
            int[] parents = new int[regions.Count];
            for (int i = 0; i < parents.Length; i++)
            {
                parents[i] = -1;
            }

            for (int child = 0; child < regions.Count; child++)
            {
                if (regions[child].OuterLoop == null || regions[child].OuterLoop.Count == 0)
                {
                    continue;
                }

                float childArea = Mathf.Abs(SignedArea(regions[child].OuterLoop));
                float bestParentArea = float.MaxValue;
                for (int parent = 0; parent < regions.Count; parent++)
                {
                    if (parent == child)
                    {
                        continue;
                    }

                    float parentArea = Mathf.Abs(SignedArea(regions[parent].OuterLoop));
                    if (parentArea <= childArea || parentArea >= bestParentArea)
                    {
                        continue;
                    }

                    if (PolygonContainsPolygon(regions[parent].OuterLoop, regions[child].OuterLoop))
                    {
                        parents[child] = parent;
                        bestParentArea = parentArea;
                    }
                }
            }

            return parents;
        }

        private static bool PolygonContainsPolygon(IReadOnlyList<Vector2> parent, IReadOnlyList<Vector2> child)
        {
            if (parent == null || child == null || parent.Count < 3 || child.Count < 3)
            {
                return false;
            }

            for (int i = 0; i < child.Count; i++)
            {
                if (!PointInPolygonInclusive(child[i], parent))
                {
                    return false;
                }
            }

            for (int p = 0; p < parent.Count; p++)
            {
                Vector2 parentA = parent[p];
                Vector2 parentB = parent[(p + 1) % parent.Count];
                for (int c = 0; c < child.Count; c++)
                {
                    Vector2 childA = child[c];
                    Vector2 childB = child[(c + 1) % child.Count];
                    if (SegmentsProperlyIntersect(parentA, parentB, childA, childB))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool PointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            float cross = Cross(a, b, point);
            if (Mathf.Abs(cross) > Epsilon)
            {
                return false;
            }

            float dot = Vector2.Dot(point - a, point - b);
            return dot <= Epsilon;
        }

        private static bool SegmentsProperlyIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (PointOnSegment(a, c, d) || PointOnSegment(b, c, d) ||
                PointOnSegment(c, a, b) || PointOnSegment(d, a, b))
            {
                return false;
            }

            float d1 = Cross(a, b, c);
            float d2 = Cross(a, b, d);
            float d3 = Cross(c, d, a);
            float d4 = Cross(c, d, b);
            return d1 * d2 < -Epsilon && d3 * d4 < -Epsilon;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 ab = b - a;
            Vector2 ac = c - a;
            return (ab.x * ac.y) - (ab.y * ac.x);
        }
    }
}
