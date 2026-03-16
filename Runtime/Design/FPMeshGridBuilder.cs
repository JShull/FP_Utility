namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Settings used to build a flat rectangular grid mesh on the XZ plane.
    /// </summary>
    [Serializable]
    public struct FPMeshGridBuildSettings
    {
        public string MeshName;
        public float Width;
        public float Length;
        public int XSegments;
        public int YSegments;
        public bool CenterPivot;

        public static FPMeshGridBuildSettings Default => new FPMeshGridBuildSettings
        {
            MeshName = "FP_GridSurface",
            Width = 1f,
            Length = 1f,
            XSegments = 1,
            YSegments = 1,
            CenterPivot = true
        };

        public FPMeshGridBuildSettings Sanitized()
        {
            return new FPMeshGridBuildSettings
            {
                MeshName = string.IsNullOrWhiteSpace(MeshName) ? "FP_GridSurface" : MeshName.Trim(),
                Width = Mathf.Max(0.01f, Width),
                Length = Mathf.Max(0.01f, Length),
                XSegments = Mathf.Max(1, XSegments),
                YSegments = Mathf.Max(1, YSegments),
                CenterPivot = CenterPivot
            };
        }
    }

    /// <summary>
    /// Builds a rectangular grid mesh that can later be deformed by heightmap data.
    /// </summary>
    public static class FPMeshGridBuilder
    {
        public static Mesh Build(FPMeshGridBuildSettings settings)
        {
            var safeSettings = settings.Sanitized();

            int columns = safeSettings.XSegments + 1;
            int rows = safeSettings.YSegments + 1;
            int vertexCount = columns * rows;
            int quadCount = safeSettings.XSegments * safeSettings.YSegments;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[quadCount * 6];

            float xStep = safeSettings.Width / safeSettings.XSegments;
            float zStep = safeSettings.Length / safeSettings.YSegments;
            float xStart = safeSettings.CenterPivot ? -safeSettings.Width * 0.5f : 0f;
            float zStart = safeSettings.CenterPivot ? -safeSettings.Length * 0.5f : 0f;

            int vertexIndex = 0;
            for (int y = 0; y < rows; y++)
            {
                float zPos = zStart + (y * zStep);
                float v = safeSettings.YSegments == 0 ? 0f : y / (float)safeSettings.YSegments;

                for (int x = 0; x < columns; x++)
                {
                    float xPos = xStart + (x * xStep);
                    float u = safeSettings.XSegments == 0 ? 0f : x / (float)safeSettings.XSegments;

                    vertices[vertexIndex] = new Vector3(xPos, 0f, zPos);
                    normals[vertexIndex] = Vector3.up;
                    tangents[vertexIndex] = new Vector4(1f, 0f, 0f, 1f);
                    uv[vertexIndex] = new Vector2(u, v);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            for (int y = 0; y < safeSettings.YSegments; y++)
            {
                for (int x = 0; x < safeSettings.XSegments; x++)
                {
                    int bottomLeft = x + (y * columns);
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + columns;
                    int topRight = topLeft + 1;

                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;

                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                }
            }

            Mesh mesh = new Mesh
            {
                name = safeSettings.MeshName
            };

            if (vertexCount > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
