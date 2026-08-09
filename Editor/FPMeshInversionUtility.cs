// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Shared editor implementation for creating inside-out mesh copies.
    /// </summary>
    internal static class FPMeshInversionUtility
    {
        public static Mesh CreateInvertedCopy(Mesh source, string outputName = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Mesh invertedMesh = UnityEngine.Object.Instantiate(source);
            invertedMesh.name = string.IsNullOrWhiteSpace(outputName)
                ? $"{source.name}_Inverted"
                : outputName.Trim();
            invertedMesh.hideFlags = HideFlags.None;

            try
            {
                Invert(invertedMesh);
                return invertedMesh;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(invertedMesh);
                throw;
            }
        }

        public static void Invert(Mesh mesh)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            if (!mesh.isReadable)
            {
                throw new InvalidOperationException(
                    $"Mesh '{mesh.name}' is not readable. Enable Read/Write on its model importer before inverting it.");
            }

            List<BlendShapeFrame> blendShapeFrames = CaptureBlendShapeFrames(mesh);
            bool invertedSurface = ReverseSurfaceWinding(mesh);
            if (!invertedSurface)
            {
                throw new InvalidOperationException(
                    $"Mesh '{mesh.name}' does not contain any triangle or quad submeshes to invert.");
            }

            InvertNormals(mesh);
            InvertTangentHandedness(mesh);
            RestoreInvertedBlendShapeNormals(mesh, blendShapeFrames);
            mesh.RecalculateBounds();
        }

        private static bool ReverseSurfaceWinding(Mesh mesh)
        {
            bool invertedSurface = false;

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                MeshTopology topology = mesh.GetTopology(subMesh);
                int faceSize = topology == MeshTopology.Triangles
                    ? 3
                    : topology == MeshTopology.Quads
                        ? 4
                        : 0;
                if (faceSize == 0)
                {
                    continue;
                }

                UnityEngine.Rendering.SubMeshDescriptor descriptor = mesh.GetSubMesh(subMesh);
                int[] indices = mesh.GetIndices(subMesh, false);
                for (int i = 0; i + faceSize - 1 < indices.Length; i += faceSize)
                {
                    int swapIndex = i + faceSize - 1;
                    (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
                }

                mesh.SetIndices(indices, topology, subMesh, false, descriptor.baseVertex);
                invertedSurface = true;
            }

            return invertedSurface;
        }

        private static void InvertNormals(Mesh mesh)
        {
            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length == mesh.vertexCount)
            {
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = -normals[i];
                }

                mesh.normals = normals;
                return;
            }

            mesh.RecalculateNormals();
        }

        private static void InvertTangentHandedness(Mesh mesh)
        {
            Vector4[] tangents = mesh.tangents;
            if (tangents == null || tangents.Length != mesh.vertexCount)
            {
                return;
            }

            for (int i = 0; i < tangents.Length; i++)
            {
                tangents[i].w = -tangents[i].w;
            }

            mesh.tangents = tangents;
        }

        private static List<BlendShapeFrame> CaptureBlendShapeFrames(Mesh mesh)
        {
            var frames = new List<BlendShapeFrame>();
            int vertexCount = mesh.vertexCount;

            for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
            {
                string shapeName = mesh.GetBlendShapeName(shapeIndex);
                int frameCount = mesh.GetBlendShapeFrameCount(shapeIndex);
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    var deltaVertices = new Vector3[vertexCount];
                    var deltaNormals = new Vector3[vertexCount];
                    var deltaTangents = new Vector3[vertexCount];
                    mesh.GetBlendShapeFrameVertices(
                        shapeIndex,
                        frameIndex,
                        deltaVertices,
                        deltaNormals,
                        deltaTangents);

                    frames.Add(new BlendShapeFrame(
                        shapeName,
                        mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex),
                        deltaVertices,
                        deltaNormals,
                        deltaTangents));
                }
            }

            return frames;
        }

        private static void RestoreInvertedBlendShapeNormals(Mesh mesh, List<BlendShapeFrame> frames)
        {
            if (frames.Count == 0)
            {
                return;
            }

            mesh.ClearBlendShapes();
            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                BlendShapeFrame frame = frames[frameIndex];
                for (int vertexIndex = 0; vertexIndex < frame.DeltaNormals.Length; vertexIndex++)
                {
                    frame.DeltaNormals[vertexIndex] = -frame.DeltaNormals[vertexIndex];
                }

                mesh.AddBlendShapeFrame(
                    frame.Name,
                    frame.Weight,
                    frame.DeltaVertices,
                    frame.DeltaNormals,
                    frame.DeltaTangents);
            }
        }

        private sealed class BlendShapeFrame
        {
            public readonly string Name;
            public readonly float Weight;
            public readonly Vector3[] DeltaVertices;
            public readonly Vector3[] DeltaNormals;
            public readonly Vector3[] DeltaTangents;

            public BlendShapeFrame(
                string name,
                float weight,
                Vector3[] deltaVertices,
                Vector3[] deltaNormals,
                Vector3[] deltaTangents)
            {
                Name = name;
                Weight = weight;
                DeltaVertices = deltaVertices;
                DeltaNormals = deltaNormals;
                DeltaTangents = deltaTangents;
            }
        }
    }
}
