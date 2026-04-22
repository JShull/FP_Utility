namespace FuzzPhyte.Utility.Video
{
    using UnityEngine;

    public static class FPVideoQuadBuilder
    {
        public static Mesh Build(FPVideoQuadBuildSettings settings)
        {
            FPVideoQuadBuildSettings sanitized = settings.Sanitized();
            int widthSegments = sanitized.WidthSegments;
            int heightSegments = sanitized.HeightSegments;
            int vertexCount = (widthSegments + 1) * (heightSegments + 1);
            int triangleIndexCount = widthSegments * heightSegments * 6;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            int[] triangles = new int[triangleIndexCount];

            float halfWidth = sanitized.Width * 0.5f;
            float halfHeight = sanitized.Height * 0.5f;
            int vertexIndex = 0;

            for (int y = 0; y <= heightSegments; y++)
            {
                float v = (float)y / heightSegments;
                float posY = Mathf.Lerp(-halfHeight, halfHeight, v);

                for (int x = 0; x <= widthSegments; x++)
                {
                    float u = (float)x / widthSegments;
                    float posX = Mathf.Lerp(-halfWidth, halfWidth, u);

                    vertices[vertexIndex] = new Vector3(posX, posY, 0f);
                    normals[vertexIndex] = sanitized.FlipFacing ? Vector3.forward : Vector3.back;
                    uvs[vertexIndex] = new Vector2(u, v);
                    tangents[vertexIndex] = sanitized.FlipFacing
                        ? new Vector4(-1f, 0f, 0f, -1f)
                        : new Vector4(1f, 0f, 0f, -1f);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            int stride = widthSegments + 1;

            for (int y = 0; y < heightSegments; y++)
            {
                int rowStart = y * stride;
                int nextRowStart = (y + 1) * stride;

                for (int x = 0; x < widthSegments; x++)
                {
                    int current = rowStart + x;
                    int next = current + 1;
                    int below = nextRowStart + x;
                    int belowNext = below + 1;

                    if (sanitized.FlipFacing)
                    {
                        triangles[triangleIndex++] = current;
                        triangles[triangleIndex++] = belowNext;
                        triangles[triangleIndex++] = below;
                        triangles[triangleIndex++] = current;
                        triangles[triangleIndex++] = next;
                        triangles[triangleIndex++] = belowNext;
                    }
                    else
                    {
                        triangles[triangleIndex++] = current;
                        triangles[triangleIndex++] = below;
                        triangles[triangleIndex++] = belowNext;
                        triangles[triangleIndex++] = current;
                        triangles[triangleIndex++] = belowNext;
                        triangles[triangleIndex++] = next;
                    }
                }
            }

            Mesh mesh = new Mesh
            {
                name = sanitized.MeshName
            };

            if (vertexCount > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
