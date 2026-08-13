namespace FuzzPhyte.Utility.Editor.Tests
{
    using System.IO;
    using System.IO.Compression;
    using NUnit.Framework;
    using UnityEngine;

    public sealed class FPMeshRuntimeObjExporterTests
    {
        [Test]
        public void TryBuildPackage_MultiSubmeshHierarchy_ContainsObjAndMtl()
        {
            GameObject root = new GameObject("Runtime Export Root");
            GameObject child = new GameObject("Mesh Child");
            child.transform.SetParent(root.transform, false);
            Mesh mesh = CreateTwoSubmeshQuad();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>();

            try
            {
                var options = new FPMeshRuntimeObjExportOptions
                {
                    ExportMaterials = true,
                    ExportTextures = false,
                    MirrorX = false
                };

                bool success = FPMeshRuntimeObjExporter.TryBuildPackage(
                    root,
                    options,
                    out FPMeshRuntimeObjExportResult result,
                    out string message);

                Assert.That(success, Is.True, message);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.ExportedMeshCount, Is.EqualTo(1));
                Assert.That(result.VertexCount, Is.EqualTo(4));
                Assert.That(result.FileName, Is.EqualTo("Runtime_Export_Root_OBJ.zip"));

                using (var stream = new MemoryStream(result.Data))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry objEntry = archive.GetEntry("Runtime_Export_Root.obj");
                    ZipArchiveEntry mtlEntry = archive.GetEntry("Runtime_Export_Root.mtl");
                    Assert.That(objEntry, Is.Not.Null);
                    Assert.That(mtlEntry, Is.Not.Null);

                    string objText = ReadEntry(objEntry);
                    string mtlText = ReadEntry(mtlEntry);
                    Assert.That(objText, Does.Contain("o Mesh_Child"));
                    Assert.That(objText, Does.Contain("mtllib Runtime_Export_Root.mtl"));
                    Assert.That(objText, Does.Contain("usemtl FP_Default"));
                    Assert.That(CountLinesBeginningWith(objText, "f "), Is.EqualTo(2));
                    Assert.That(mtlText, Does.Contain("newmtl FP_Default"));
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryBuildPackage_OverVertexLimit_ReturnsClearFailure()
        {
            GameObject root = new GameObject("Vertex Limit Root");
            Mesh mesh = CreateTwoSubmeshQuad();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;

            try
            {
                var options = new FPMeshRuntimeObjExportOptions
                {
                    MaximumVertexCount = 3
                };

                bool success = FPMeshRuntimeObjExporter.TryBuildPackage(
                    root,
                    options,
                    out FPMeshRuntimeObjExportResult result,
                    out string message);

                Assert.That(success, Is.False);
                Assert.That(result, Is.Null);
                Assert.That(message, Does.Contain("exceeding the configured limit"));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TrySaveOrDownload_RegisteredHandler_ControlsDeliveryLocation()
        {
            FPFileExportHandler previousHandler =
                FPFileExportUtility.PlatformSaveHandler;
            bool handlerInvoked = false;
            try
            {
                FPFileExportUtility.SetPlatformSaveHandler(
                    delegate(
                        byte[] data,
                        string fileName,
                        string mimeType,
                        out string deliveredLocation,
                        out string message)
                    {
                        handlerInvoked = true;
                        deliveredLocation = "C:/Exports/Test_OBJ.zip";
                        message = "Saved with a prompt.";
                        return data.Length == 3 &&
                               fileName == "Test_OBJ.zip" &&
                               mimeType == "application/zip";
                    });

                bool success = FPFileExportUtility.TrySaveOrDownload(
                    new byte[] { 1, 2, 3 },
                    "Test_OBJ.zip",
                    "application/zip",
                    out string deliveredLocation,
                    out string message);

                Assert.That(success, Is.True, message);
                Assert.That(handlerInvoked, Is.True);
                Assert.That(deliveredLocation, Is.EqualTo("C:/Exports/Test_OBJ.zip"));
            }
            finally
            {
                FPFileExportUtility.SetPlatformSaveHandler(previousHandler);
            }
        }

        private static Mesh CreateTwoSubmeshQuad()
        {
            var mesh = new Mesh { name = "Two Submesh Quad" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.normals = new[]
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 0, 2, 3 }, 1);
            return mesh;
        }

        private static string ReadEntry(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static int CountLinesBeginningWith(string text, string prefix)
        {
            int count = 0;
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith(prefix))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
