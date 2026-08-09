// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using NUnit.Framework;
    using UnityEngine;

    public class FPMeshInversionUtilityTests
    {
        [Test]
        public void CreateInvertedCopy_ReversesWindingNormalsAndTangentHandedness()
        {
            Mesh source = CreateTriangleMesh(includeNormals: true);
            source.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };

            Mesh inverted = FPMeshInversionUtility.CreateInvertedCopy(source, "Test_Inverted");

            Assert.That(inverted, Is.Not.SameAs(source));
            Assert.That(inverted.name, Is.EqualTo("Test_Inverted"));
            Assert.That(inverted.GetIndices(0), Is.EqualTo(new[] { 2, 1, 0 }));
            Assert.That(inverted.normals, Is.All.EqualTo(Vector3.back));
            Assert.That(inverted.tangents[0].w, Is.EqualTo(-1f));
            Assert.That(inverted.uv, Is.EqualTo(source.uv));
            Assert.That(source.GetIndices(0), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(source.normals, Is.All.EqualTo(Vector3.forward));

            Object.DestroyImmediate(inverted);
            Object.DestroyImmediate(source);
        }

        [Test]
        public void CreateInvertedCopy_RecalculatesMissingNormalsFromReversedWinding()
        {
            Mesh source = CreateTriangleMesh(includeNormals: false);

            Mesh inverted = FPMeshInversionUtility.CreateInvertedCopy(source);

            Assert.That(inverted.normals, Has.Length.EqualTo(3));
            Assert.That(inverted.normals[0].z, Is.LessThan(-0.99f));

            Object.DestroyImmediate(inverted);
            Object.DestroyImmediate(source);
        }

        [Test]
        public void CreateInvertedCopy_InvertsBlendShapeNormalDeltasOnly()
        {
            Mesh source = CreateTriangleMesh(includeNormals: true);
            var deltaVertices = new[] { Vector3.right, Vector3.zero, Vector3.zero };
            var deltaNormals = new[] { Vector3.up, Vector3.zero, Vector3.zero };
            var deltaTangents = new[] { Vector3.forward, Vector3.zero, Vector3.zero };
            source.AddBlendShapeFrame("Move", 100f, deltaVertices, deltaNormals, deltaTangents);

            Mesh inverted = FPMeshInversionUtility.CreateInvertedCopy(source);
            var actualVertices = new Vector3[3];
            var actualNormals = new Vector3[3];
            var actualTangents = new Vector3[3];
            inverted.GetBlendShapeFrameVertices(0, 0, actualVertices, actualNormals, actualTangents);

            Assert.That(actualVertices[0], Is.EqualTo(Vector3.right));
            Assert.That(actualNormals[0], Is.EqualTo(Vector3.down));
            Assert.That(actualTangents[0], Is.EqualTo(Vector3.forward));

            Object.DestroyImmediate(inverted);
            Object.DestroyImmediate(source);
        }

        private static Mesh CreateTriangleMesh(bool includeNormals)
        {
            var mesh = new Mesh
            {
                name = "TestMesh",
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                },
                triangles = new[] { 0, 1, 2 },
                tangents = new[]
                {
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f)
                }
            };

            if (includeNormals)
            {
                mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
            }

            return mesh;
        }
    }
}
