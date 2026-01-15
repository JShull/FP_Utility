namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;
    using UnityEngine.Rendering;
    public sealed class FPMeshViewCache : IDisposable
    {
        private ComputeBuffer _vertexBuffer;
        private ComputeBuffer _normalLineBuffer;
        private Mesh _wireframeMesh;
        private int _vertexCount;
        private int _normalLineVertexCount;
        #region Fallback Method for Metal

        private static ComputeBuffer s_FallbackVertices;
        private static readonly int FPVerticesID = Shader.PropertyToID("_FPVertices");

        private static void EnsureFallback()
        {
            if (s_FallbackVertices != null) return;
            s_FallbackVertices = new ComputeBuffer(1, sizeof(float) * 3);
            s_FallbackVertices.SetData(new Vector3[] { Vector3.zero });
        }
        #endregion
        public static FPMeshViewCache Build(Mesh mesh)
        {
            var cache = new FPMeshViewCache();
            cache.BuildVertexBuffer(mesh);
            cache.BuildNormalLines(mesh);
            cache.BuildWireframeMeshWithBarycentric(mesh);
            return cache;
        }
        public void DrawVertices(RasterCommandBuffer cmd, Matrix4x4 localToWorld, Material mat)
        {
            if (_vertexBuffer == null || mat == null) return;
            EnsureFallback();
            //
            // Always bind a buffer so Metal is satisfied
            var bufferToBind = _vertexBuffer != null ? _vertexBuffer : s_FallbackVertices;
            mat.SetMatrix("_LocalToWorld", localToWorld);
            mat.SetBuffer(FPVerticesID, bufferToBind);

    // If no real data, do not draw
    if ( _vertexCount <= 0) return;

    int quadVertexCount = _vertexCount * 6;
    cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, quadVertexCount, 1);
            
            //
            //mat.SetMatrix("_LocalToWorld", localToWorld);
            //mat.SetBuffer("_FPVertices", _vertexBuffer);

            //int quadVertexCount = _vertexCount * 6;
            //cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, quadVertexCount, 1);

            //
            //cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Points, _vertexCount, 1);
        }
        public void DrawNormals(RasterCommandBuffer cmd, Matrix4x4 localToWorld, Material mat)
        {
            if (_normalLineBuffer == null || mat == null) return;
            mat.SetMatrix("_LocalToWorld", localToWorld);
            mat.SetBuffer("_Lines", _normalLineBuffer);
            cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Lines, _normalLineVertexCount, 1);
        }
        public void DrawWireframe(RasterCommandBuffer cmd, Matrix4x4 localToWorld, Material mat)
        {
            if (_wireframeMesh == null || mat == null) return;
            cmd.DrawMesh(_wireframeMesh, localToWorld, mat, 0, 0);
        }
        private void BuildVertexBuffer(Mesh mesh)
        {
            var verts = mesh.vertices;
            _vertexCount = verts.Length;

            // Packed as float3
            _vertexBuffer = new ComputeBuffer(_vertexCount, sizeof(float) * 3);
            _vertexBuffer.SetData(verts);
        }
        private void BuildNormalLines(Mesh mesh)
        {
            var verts = mesh.vertices;
            var norms = mesh.normals;
            if (norms == null || norms.Length != verts.Length) return;

            // Each normal line is 2 vertices => pos, pos+normal*scale
            var scale = 0.05f;
            var lineVerts = new Vector3[verts.Length * 2];
            for (int i = 0; i < verts.Length; i++)
            {
                lineVerts[i * 2 + 0] = verts[i];
                lineVerts[i * 2 + 1] = verts[i] + norms[i] * scale;
            }

            _normalLineVertexCount = lineVerts.Length;
            _normalLineBuffer = new ComputeBuffer(_normalLineVertexCount, sizeof(float) * 3);
            _normalLineBuffer.SetData(lineVerts);
        }

        private void BuildWireframeMeshWithBarycentric(Mesh src, int maxTriangleCap = 200_000)
        {
            if (src == null) return;

            int[] srcTris = src.triangles;
            int triCount = srcTris.Length / 3;

            // Performance guard: you can tune this cap for mobile/Web/VR.
            if (triCount > maxTriangleCap)
            {
                _wireframeMesh = null;
                return;
            }

            Vector3[] srcVerts = src.vertices;
            Vector3[] srcNormals = src.normals;
            Vector4[] srcTangents = src.tangents;
            Vector2[] srcUv0 = src.uv;

            bool hasNormals = srcNormals != null && srcNormals.Length == srcVerts.Length;
            bool hasTangents = srcTangents != null && srcTangents.Length == srcVerts.Length;
            bool hasUv0 = srcUv0 != null && srcUv0.Length == srcVerts.Length;

            int newVertCount = triCount * 3;

            var newVerts = new Vector3[newVertCount];
            var newNormals = hasNormals ? new Vector3[newVertCount] : null;
            var newTangents = hasTangents ? new Vector4[newVertCount] : null;
            var newUv0 = hasUv0 ? new Vector2[newVertCount] : null;

            // Store barycentric in vertex colors:
            var newColors = new Color[newVertCount];

            // Indices become sequential (0..N-1)
            var newIndices = new int[newVertCount];

            for (int t = 0; t < triCount; t++)
            {
                int i0 = srcTris[t * 3 + 0];
                int i1 = srcTris[t * 3 + 1];
                int i2 = srcTris[t * 3 + 2];

                int o = t * 3;

                newVerts[o + 0] = srcVerts[i0];
                newVerts[o + 1] = srcVerts[i1];
                newVerts[o + 2] = srcVerts[i2];

                if (hasNormals)
                {
                    newNormals[o + 0] = srcNormals[i0];
                    newNormals[o + 1] = srcNormals[i1];
                    newNormals[o + 2] = srcNormals[i2];
                }

                if (hasTangents)
                {
                    newTangents[o + 0] = srcTangents[i0];
                    newTangents[o + 1] = srcTangents[i1];
                    newTangents[o + 2] = srcTangents[i2];
                }

                if (hasUv0)
                {
                    newUv0[o + 0] = srcUv0[i0];
                    newUv0[o + 1] = srcUv0[i1];
                    newUv0[o + 2] = srcUv0[i2];
                }

                // Barycentric: (1,0,0), (0,1,0), (0,0,1)
                newColors[o + 0] = new Color(1, 0, 0, 1);
                newColors[o + 1] = new Color(0, 1, 0, 1);
                newColors[o + 2] = new Color(0, 0, 1, 1);

                newIndices[o + 0] = o + 0;
                newIndices[o + 1] = o + 1;
                newIndices[o + 2] = o + 2;
            }

            _wireframeMesh = new Mesh
            {
                name = $"{src.name}_FPWireBary"
            };

            // Use 32-bit indices if needed
            if (newVertCount > 65535)
                _wireframeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            _wireframeMesh.vertices = newVerts;
            if (hasNormals) _wireframeMesh.normals = newNormals;
            if (hasTangents) _wireframeMesh.tangents = newTangents;
            if (hasUv0) _wireframeMesh.uv = newUv0;

            _wireframeMesh.colors = newColors;
            _wireframeMesh.SetIndices(newIndices, MeshTopology.Triangles, 0, true);

            _wireframeMesh.RecalculateBounds();
        }

        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _normalLineBuffer?.Dispose();
            if (_wireframeMesh != null)
            {
                GameObject.Destroy(_wireframeMesh);  
            }
        }
    }
}
