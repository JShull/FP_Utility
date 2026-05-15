namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    public static class FPSVGExtrudedMeshBuilder
    {
        public static Mesh Build(
            IReadOnlyList<FPSVGRegion> regions,
            FPSVGExtruderSettings settings,
            out FPSVGMeshBuildReport report)
        {
            report = new FPSVGMeshBuildReport();
            FPSVGExtruderSettings safeSettings = (settings ?? FPSVGExtruderSettings.Default).Sanitized();

            if (regions == null || regions.Count == 0)
            {
                report.Errors.Add("No included SVG regions were provided for mesh generation.");
                return null;
            }

            Bounds2D bounds = CalculateMeshBounds(regions, safeSettings.Scale);
            Vector2 pivotOffset = safeSettings.CenterPivot ? bounds.Center : Vector2.zero;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < regions.Count; i++)
            {
                FPSVGRegion source = regions[i];
                List<Vector2> outer = ToMeshLoop(source.OuterLoop, safeSettings.Scale, pivotOffset, safeSettings);
                List<List<Vector2>> holes = new List<List<Vector2>>();
                for (int h = 0; h < source.Holes.Count; h++)
                {
                    holes.Add(ToMeshLoop(source.Holes[h], safeSettings.Scale, pivotOffset, safeSettings));
                }

                List<int> bridgedHoleIndices = AddRegionSurfaces(source.Id, outer, holes, safeSettings, bounds, pivotOffset, vertices, normals, uvs, triangles, report);
                if (bridgedHoleIndices == null)
                {
                    continue;
                }

                AddSideWalls(EnsureWinding(outer, true), safeSettings.ExtrusionDepth, bounds, pivotOffset, vertices, normals, uvs, triangles);
                for (int h = 0; h < bridgedHoleIndices.Count; h++)
                {
                    int holeIndex = bridgedHoleIndices[h];
                    if (holeIndex >= 0 && holeIndex < holes.Count)
                    {
                        AddSideWalls(EnsureWinding(holes[holeIndex], false), safeSettings.ExtrusionDepth, bounds, pivotOffset, vertices, normals, uvs, triangles);
                    }
                }
            }

            if (vertices.Count == 0 || triangles.Count == 0)
            {
                report.Errors.Add("Mesh generation produced no triangles. Check that the selected SVG regions are closed and non-self-intersecting.");
                return null;
            }

            if (safeSettings.GenerateDoubleSided)
            {
                int originalTriangleCount = triangles.Count;
                for (int i = 0; i < originalTriangleCount; i += 3)
                {
                    triangles.Add(triangles[i + 2]);
                    triangles.Add(triangles[i + 1]);
                    triangles.Add(triangles[i]);
                }
            }

            Mesh mesh = new Mesh
            {
                name = safeSettings.OutputMeshName
            };

            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            if (safeSettings.RecalculateNormals)
            {
                mesh.RecalculateNormals();
            }

            return mesh;
        }

        private static List<int> AddRegionSurfaces(
            string regionId,
            List<Vector2> outer,
            List<List<Vector2>> holes,
            FPSVGExtruderSettings settings,
            Bounds2D sourceBounds,
            Vector2 pivotOffset,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            FPSVGMeshBuildReport report)
        {
            FPSVGTriangulation triangulation = TriangulateRegion(regionId, outer, holes, settings, report);
            if (triangulation.Triangles.Count == 0)
            {
                report.Warnings.Add($"Region '{regionId}' did not generate top or bottom surface triangles.");
                return null;
            }

            float halfDepth = settings.ExtrusionDepth * 0.5f;
            int topStart = vertices.Count;
            for (int i = 0; i < triangulation.Vertices.Count; i++)
            {
                Vector2 point = triangulation.Vertices[i];
                vertices.Add(new Vector3(point.x, halfDepth, point.y));
                normals.Add(Vector3.up);
                uvs.Add(ToUV(point, sourceBounds, pivotOffset));
            }

            int bottomStart = vertices.Count;
            for (int i = 0; i < triangulation.Vertices.Count; i++)
            {
                Vector2 point = triangulation.Vertices[i];
                vertices.Add(new Vector3(point.x, -halfDepth, point.y));
                normals.Add(Vector3.down);
                uvs.Add(ToUV(point, sourceBounds, pivotOffset));
            }

            for (int i = 0; i < triangulation.Triangles.Count; i += 3)
            {
                int a = triangulation.Triangles[i];
                int b = triangulation.Triangles[i + 1];
                int c = triangulation.Triangles[i + 2];

                triangles.Add(topStart + c);
                triangles.Add(topStart + b);
                triangles.Add(topStart + a);

                triangles.Add(bottomStart + a);
                triangles.Add(bottomStart + b);
                triangles.Add(bottomStart + c);
            }

            return triangulation.BridgedHoleIndices;
        }

        private static FPSVGTriangulation TriangulateRegion(
            string regionId,
            List<Vector2> outer,
            List<List<Vector2>> holes,
            FPSVGExtruderSettings settings,
            FPSVGMeshBuildReport report)
        {
            if (settings.TriangulationBackend == FPSVGTriangulationBackend.UnityVectorGraphics)
            {
                if (FPSVGUnityVectorGraphicsTriangulator.TryTriangulate(outer, holes, settings, out FPSVGTriangulation unityTriangulation, out string message))
                {
                    return unityTriangulation;
                }

                report.Warnings.Add($"Region '{regionId}' could not use Unity Vector Graphics triangulation ({message}). Falling back to custom ear clipping.");
            }

            return FPSVGPolygonTriangulator.Triangulate(
                outer,
                holes,
                report.Warnings,
                regionId,
                settings.OptimizeSurfaceTriangulation,
                settings.SurfaceOptimizationPasses,
                settings.UseZOrderEarSearch);
        }

        private static void AddSideWalls(
            List<Vector2> loop,
            float depth,
            Bounds2D sourceBounds,
            Vector2 pivotOffset,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles)
        {
            if (loop == null || loop.Count < 2)
            {
                return;
            }

            float halfDepth = depth * 0.5f;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 a = loop[i];
                Vector2 b = loop[(i + 1) % loop.Count];
                if ((a - b).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                int start = vertices.Count;
                Vector3 topA = new Vector3(a.x, halfDepth, a.y);
                Vector3 topB = new Vector3(b.x, halfDepth, b.y);
                Vector3 bottomA = new Vector3(a.x, -halfDepth, a.y);
                Vector3 bottomB = new Vector3(b.x, -halfDepth, b.y);
                Vector3 sideNormal = Vector3.Cross(topB - topA, Vector3.down).normalized;

                vertices.Add(topA);
                vertices.Add(topB);
                vertices.Add(bottomB);
                vertices.Add(bottomA);

                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);

                Vector2 uvA = ToUV(a, sourceBounds, pivotOffset);
                Vector2 uvB = ToUV(b, sourceBounds, pivotOffset);
                uvs.Add(uvA);
                uvs.Add(uvB);
                uvs.Add(uvB);
                uvs.Add(uvA);

                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }

        private static List<Vector2> ToMeshLoop(
            IReadOnlyList<Vector2> source,
            float scale,
            Vector2 pivotOffset,
            FPSVGExtruderSettings settings)
        {
            var loop = new List<Vector2>();
            if (source == null)
            {
                return loop;
            }

            for (int i = 0; i < source.Count; i++)
            {
                Vector2 point = new Vector2(source[i].x * scale, -source[i].y * scale) - pivotOffset;
                if (loop.Count == 0 || (loop[loop.Count - 1] - point).sqrMagnitude > 0.0000001f)
                {
                    loop.Add(point);
                }
            }

            if (loop.Count > 1 && (loop[0] - loop[loop.Count - 1]).sqrMagnitude <= 0.0000001f)
            {
                loop.RemoveAt(loop.Count - 1);
            }

            return FPSVGPolygonCleanup.CleanLoop(loop, settings.BoundarySimplifyTolerance, settings.CollinearTolerance);
        }

        private static List<Vector2> EnsureWinding(List<Vector2> loop, bool counterClockwise)
        {
            var copy = new List<Vector2>(loop);
            bool isCounterClockwise = FPSVGRegionDetector.SignedArea(copy) > 0f;
            if (isCounterClockwise != counterClockwise)
            {
                copy.Reverse();
            }

            return copy;
        }

        private static Vector2 ToUV(Vector2 meshPoint, Bounds2D sourceBounds, Vector2 pivotOffset)
        {
            Vector2 uncentered = meshPoint + pivotOffset;
            float u = sourceBounds.Width <= 0f ? 0f : Mathf.InverseLerp(sourceBounds.Min.x, sourceBounds.Max.x, uncentered.x);
            float v = sourceBounds.Height <= 0f ? 0f : Mathf.InverseLerp(sourceBounds.Min.y, sourceBounds.Max.y, uncentered.y);
            return new Vector2(u, v);
        }

        private static Bounds2D CalculateMeshBounds(IReadOnlyList<FPSVGRegion> regions, float scale)
        {
            bool initialized = false;
            Vector2 min = Vector2.zero;
            Vector2 max = Vector2.zero;
            for (int r = 0; r < regions.Count; r++)
            {
                IncludeLoop(regions[r].OuterLoop, scale, ref initialized, ref min, ref max);
                for (int h = 0; h < regions[r].Holes.Count; h++)
                {
                    IncludeLoop(regions[r].Holes[h], scale, ref initialized, ref min, ref max);
                }
            }

            if (!initialized)
            {
                min = Vector2.zero;
                max = Vector2.one;
            }

            return new Bounds2D(min, max);
        }

        private static void IncludeLoop(IReadOnlyList<Vector2> loop, float scale, ref bool initialized, ref Vector2 min, ref Vector2 max)
        {
            if (loop == null)
            {
                return;
            }

            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 point = new Vector2(loop[i].x * scale, -loop[i].y * scale);
                if (!initialized)
                {
                    initialized = true;
                    min = max = point;
                }
                else
                {
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }
        }

        private readonly struct Bounds2D
        {
            public readonly Vector2 Min;
            public readonly Vector2 Max;

            public Bounds2D(Vector2 min, Vector2 max)
            {
                Min = min;
                Max = max;
            }

            public Vector2 Center => (Min + Max) * 0.5f;
            public float Width => Max.x - Min.x;
            public float Height => Max.y - Min.y;
        }
    }
}
