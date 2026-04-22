namespace FuzzPhyte.Utility.Video
{
    using UnityEngine;

    /// <summary>
    /// Builds a UV sphere suitable for equirectangular 360 image or video playback.
    /// </summary>
    public static class FPVideoSphereBuilder
    {
        public static Mesh Build(FPVideoSphereBuildSettings settings)
        {
            FPVideoSphereBuildSettings sanitized = settings.Sanitized();

            int longitudeSegments = sanitized.LongitudeSegments;
            int latitudeSegments = sanitized.LatitudeSegments;
            int vertexCount = (longitudeSegments + 1) * (latitudeSegments + 1);
            int triangleCount = longitudeSegments * Mathf.Max(1, (latitudeSegments - 1)) * 2;
            int triangleIndexCount = triangleCount * 3;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            int[] triangles = new int[triangleIndexCount];

            float radius = sanitized.Radius;
            int vertexIndex = 0;

            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float v = (float)lat / latitudeSegments;
                float theta = v * Mathf.PI;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float u = (float)lon / longitudeSegments;
                    float phi = u * Mathf.PI * 2f;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    Vector3 normal = new Vector3(sinTheta * cosPhi, cosTheta, sinTheta * sinPhi);
                    vertices[vertexIndex] = normal * radius;
                    normals[vertexIndex] = sanitized.GenerateInsideOut ? -normal : normal;
                    uvs[vertexIndex] = new Vector2(1f - u, v);

                    Vector3 tangentDirection = new Vector3(-sinPhi, 0f, cosPhi);
                    if (tangentDirection.sqrMagnitude < 0.0001f)
                    {
                        tangentDirection = Vector3.right;
                    }

                    tangentDirection.Normalize();
                    tangents[vertexIndex] = new Vector4(
                        tangentDirection.x,
                        tangentDirection.y,
                        tangentDirection.z,
                        sanitized.GenerateInsideOut ? -1f : 1f);

                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            int stride = longitudeSegments + 1;

            for (int lat = 0; lat < latitudeSegments; lat++)
            {
                int rowStart = lat * stride;
                int nextRowStart = (lat + 1) * stride;

                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    int current = rowStart + lon;
                    int next = current + 1;
                    int below = nextRowStart + lon;
                    int belowNext = below + 1;

                    if (sanitized.GenerateInsideOut)
                    {
                        if (lat == 0)
                        {
                            triangles[triangleIndex++] = current;
                            triangles[triangleIndex++] = below;
                            triangles[triangleIndex++] = belowNext;
                        }
                        else if (lat == latitudeSegments - 1)
                        {
                            triangles[triangleIndex++] = current;
                            triangles[triangleIndex++] = belowNext;
                            triangles[triangleIndex++] = next;
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
                    else
                    {
                        if (lat == 0)
                        {
                            triangles[triangleIndex++] = current;
                            triangles[triangleIndex++] = belowNext;
                            triangles[triangleIndex++] = below;
                        }
                        else if (lat == latitudeSegments - 1)
                        {
                            triangles[triangleIndex++] = current;
                            triangles[triangleIndex++] = next;
                            triangles[triangleIndex++] = belowNext;
                        }
                        else
                        {
                            triangles[triangleIndex++] = current;
                            triangles[triangleIndex++] = belowNext;
                            triangles[triangleIndex++] = below;

                            triangles[triangleIndex++] = current;
                            triangles[triangleIndex++] = next;
                            triangles[triangleIndex++] = belowNext;
                        }
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
