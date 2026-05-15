namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;

    public static class FPSVGPolygonCleanup
    {
        public static List<Vector2> CleanLoop(
            IReadOnlyList<Vector2> source,
            float simplifyTolerance,
            float collinearTolerance)
        {
            List<Vector2> loop = RemoveDuplicatePoints(source, Mathf.Max(0.0000001f, simplifyTolerance * 0.1f));
            if (loop.Count < 3)
            {
                return loop;
            }

            if (simplifyTolerance > 0f && loop.Count > 3)
            {
                loop = SimplifyClosedLoop(loop, simplifyTolerance);
            }

            if (collinearTolerance > 0f && loop.Count > 3)
            {
                loop = RemoveCollinearPoints(loop, collinearTolerance);
            }

            return loop;
        }

        private static List<Vector2> RemoveDuplicatePoints(IReadOnlyList<Vector2> source, float distanceTolerance)
        {
            var clean = new List<Vector2>();
            if (source == null)
            {
                return clean;
            }

            float sqrTolerance = distanceTolerance * distanceTolerance;
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 point = source[i];
                if (clean.Count == 0 || (clean[clean.Count - 1] - point).sqrMagnitude > sqrTolerance)
                {
                    clean.Add(point);
                }
            }

            if (clean.Count > 1 && (clean[0] - clean[clean.Count - 1]).sqrMagnitude <= sqrTolerance)
            {
                clean.RemoveAt(clean.Count - 1);
            }

            return clean;
        }

        private static List<Vector2> SimplifyClosedLoop(IReadOnlyList<Vector2> loop, float tolerance)
        {
            if (loop == null || loop.Count <= 3)
            {
                return loop == null ? new List<Vector2>() : new List<Vector2>(loop);
            }

            int anchorIndex = FindAnchorIndex(loop);
            var opened = new List<Vector2>(loop.Count + 1);
            for (int i = 0; i < loop.Count; i++)
            {
                opened.Add(loop[(anchorIndex + i) % loop.Count]);
            }

            opened.Add(opened[0]);

            bool[] keep = new bool[opened.Count];
            keep[0] = true;
            keep[opened.Count - 1] = true;
            SimplifySection(opened, 0, opened.Count - 1, tolerance * tolerance, keep);

            var simplified = new List<Vector2>();
            for (int i = 0; i < opened.Count - 1; i++)
            {
                if (keep[i])
                {
                    simplified.Add(opened[i]);
                }
            }

            return simplified.Count >= 3 ? simplified : new List<Vector2>(loop);
        }

        private static void SimplifySection(
            IReadOnlyList<Vector2> points,
            int start,
            int end,
            float sqrTolerance,
            bool[] keep)
        {
            if (end <= start + 1)
            {
                return;
            }

            float maxDistance = -1f;
            int maxIndex = -1;
            Vector2 a = points[start];
            Vector2 b = points[end];
            for (int i = start + 1; i < end; i++)
            {
                float distance = SqrDistanceToSegment(points[i], a, b);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    maxIndex = i;
                }
            }

            if (maxIndex < 0 || maxDistance <= sqrTolerance)
            {
                return;
            }

            keep[maxIndex] = true;
            SimplifySection(points, start, maxIndex, sqrTolerance, keep);
            SimplifySection(points, maxIndex, end, sqrTolerance, keep);
        }

        private static List<Vector2> RemoveCollinearPoints(IReadOnlyList<Vector2> loop, float tolerance)
        {
            var clean = new List<Vector2>(loop);
            bool removed;
            int guard = clean.Count * 2;
            do
            {
                removed = false;
                for (int i = clean.Count - 1; i >= 0 && clean.Count > 3; i--)
                {
                    Vector2 previous = clean[(i - 1 + clean.Count) % clean.Count];
                    Vector2 current = clean[i];
                    Vector2 next = clean[(i + 1) % clean.Count];
                    float distance = Mathf.Sqrt(SqrDistanceToSegment(current, previous, next));
                    if (distance <= tolerance)
                    {
                        clean.RemoveAt(i);
                        removed = true;
                    }
                }
            }
            while (removed && --guard > 0);

            return clean;
        }

        private static int FindAnchorIndex(IReadOnlyList<Vector2> loop)
        {
            int anchor = 0;
            for (int i = 1; i < loop.Count; i++)
            {
                if (loop[i].x < loop[anchor].x || Mathf.Approximately(loop[i].x, loop[anchor].x) && loop[i].y < loop[anchor].y)
                {
                    anchor = i;
                }
            }

            return anchor;
        }

        private static float SqrDistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0.0000001f)
            {
                return (point - a).sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
            Vector2 projection = a + (ab * t);
            return (point - projection).sqrMagnitude;
        }
    }
}
