namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;

    public static class FPSVGRegionDetector
    {
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

                Vector2 sample = regions[child].OuterLoop[0];
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

                    if (PointInPolygon(sample, regions[parent].OuterLoop))
                    {
                        parents[child] = parent;
                        bestParentArea = parentArea;
                    }
                }
            }

            return parents;
        }
    }
}
