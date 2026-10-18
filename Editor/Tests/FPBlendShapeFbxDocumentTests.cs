// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
// Public license: GNU GPLv3-or-later. See LICENSE.md.
// Commercial/proprietary use requires a separate license from John B. Shull.

namespace FuzzPhyte.Utility.Editor.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    public sealed class FPBlendShapeFbxDocumentTests
    {
        private const string OldName = "blendShape1.jawFowad";
        private const string NewName = "blendShape1.jawForward";

        [TestCase(7400)]
        [TestCase(7700)]
        public void Binary_RenameAndReverseRestoresEverySourceByte(int version)
        {
            byte[] source = BinaryFixture(version);
            var document = new FPBlendShapeFbxDocument(source);
            Assert.That(document.Channels.Count, Is.EqualTo(2));
            byte[] renamed = document.Rename(new Dictionary<long, string> { [400] = NewName + "_長い名前" });
            var result = new FPBlendShapeFbxDocument(renamed);
            Assert.That(result.Channels.Single(c => c.Id == 400).Name, Is.EqualTo(NewName + "_長い名前"));
            Assert.That(result.Channels.Single(c => c.Id == 401).Name, Is.EqualTo("blink"));
            byte[] restored = result.Rename(new Dictionary<long, string> { [400] = OldName });
            Assert.That(restored, Is.EqualTo(source), "Opaque arrays, footer, IDs and unrelated strings must survive exactly.");
            Assert.That(source, Is.EqualTo(BinaryFixture(version)), "The source buffer must not be mutated.");
        }

        [TestCase(7400)]
        [TestCase(7700)]
        public void Binary_NoChangesIsByteIdentical(int version)
        {
            byte[] source = BinaryFixture(version);
            Assert.That(new FPBlendShapeFbxDocument(source).Rename(new Dictionary<long, string>()), Is.EqualTo(source));
        }

        [Test]
        public void Ascii_OnlyChannelNameChangesIncludingUtf8Offsets()
        {
            string text = AsciiFixture.Replace("; FBX 7.4.0", "; FBX 7.4.0\n; 日本語 " + OldName);
            byte[] source = Encoding.UTF8.GetBytes(text);
            var document = new FPBlendShapeFbxDocument(source);
            byte[] result = document.Rename(new Dictionary<long, string> { [400] = NewName });
            string expected = text.Replace("SubDeformer::" + OldName, "SubDeformer::" + NewName);
            Assert.That(Encoding.UTF8.GetString(result), Is.EqualTo(expected));
            Assert.That(Encoding.UTF8.GetString(source), Is.EqualTo(text));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" name")]
        [TestCase("name\n")]
        [TestCase("bad\0name")]
        [TestCase("bad\"name")]
        public void InvalidNamesAreRejected(string name)
        {
            var document = new FPBlendShapeFbxDocument(BinaryFixture(7700));
            Assert.Throws<ArgumentException>(() => document.Rename(new Dictionary<long, string> { [400] = name }));
        }

        [Test]
        public void DuplicateNamesRejectedButSimultaneousSwapsAllowed()
        {
            var document = new FPBlendShapeFbxDocument(BinaryFixture(7700));
            Assert.Throws<ArgumentException>(() => document.Rename(new Dictionary<long, string> { [400] = "blink" }));
            byte[] bytes = document.Rename(new Dictionary<long, string> { [400] = "blink", [401] = OldName });
            var renamed = new FPBlendShapeFbxDocument(bytes);
            Assert.That(renamed.Channels.Single(c => c.Id == 400).Name, Is.EqualTo("blink"));
            Assert.That(renamed.Channels.Single(c => c.Id == 401).Name, Is.EqualTo(OldName));
        }

        [Test]
        public void UnknownChannelAndTruncatedBinaryAreRejected()
        {
            byte[] source = BinaryFixture(7700);
            Assert.Throws<ArgumentException>(() => new FPBlendShapeFbxDocument(source).Rename(new Dictionary<long, string> { [999] = NewName }));
            Assert.Throws<InvalidDataException>(() => new FPBlendShapeFbxDocument(source.Take(60).ToArray()));
            Assert.Throws<InvalidDataException>(() => new FPBlendShapeFbxDocument(Encoding.UTF8.GetBytes("not an FBX")));
        }

        [Test]
        public void SaveCopy_ReimportsWholeModelWithNewNameAndKeepsSourceAndImporterSettings()
        {
            string folder = CreateFolder();
            FPBlendShapeRenameWindow window = null;
            try
            {
                string sourcePath = folder + "/Source.fbx";
                File.WriteAllText(sourcePath, AsciiFixture, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(sourcePath);
                importer.isReadable = true;
                importer.globalScale = 1.75f;
                importer.SaveAndReimport();
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                Assert.That(model, Is.Not.Null);
                Mesh original = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Mesh>().Single();
                Assert.That(original.GetBlendShapeName(0), Is.EqualTo(OldName));
                byte[] originalBytes = File.ReadAllBytes(sourcePath);
                string originalGuid = AssetDatabase.AssetPathToGUID(sourcePath);
                window = ScriptableObject.CreateInstance<FPBlendShapeRenameWindow>();
                window.LoadSource(model);
                var serialized = new SerializedObject(window);
                SerializedProperty row = serialized.FindProperty("rows").GetArrayElementAtIndex(0);
                row.FindPropertyRelative("selected").boolValue = true;
                row.FindPropertyRelative("replacement").stringValue = NewName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                string outputPath = folder + "/Renamed.fbx";
                window.SaveCopy(outputPath);
                Mesh copy = AssetDatabase.LoadAllAssetsAtPath(outputPath).OfType<Mesh>().Single();
                Assert.That(copy.GetBlendShapeName(0), Is.EqualTo(NewName));
                Assert.That(copy.vertices, Is.EqualTo(original.vertices));
                Assert.That(copy.triangles, Is.EqualTo(original.triangles));
                Assert.That(copy.GetBlendShapeFrameCount(0), Is.EqualTo(original.GetBlendShapeFrameCount(0)));
                var sourceDeltas = new Vector3[original.vertexCount];
                var copyDeltas = new Vector3[copy.vertexCount];
                original.GetBlendShapeFrameVertices(0, 0, sourceDeltas, null, null);
                copy.GetBlendShapeFrameVertices(0, 0, copyDeltas, null, null);
                Assert.That(copyDeltas, Is.EqualTo(sourceDeltas));
                Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(originalBytes));
                Assert.That(AssetDatabase.AssetPathToGUID(outputPath), Is.Not.EqualTo(originalGuid));
                Assert.That(((ModelImporter)AssetImporter.GetAtPath(outputPath)).globalScale, Is.EqualTo(1.75f));
                Assert.Throws<IOException>(() => window.SaveCopy(sourcePath));
                Assert.Throws<IOException>(() => window.SaveCopy(outputPath));
                File.AppendAllText(sourcePath, "\n; changed source");
                Assert.Throws<InvalidOperationException>(() => window.SaveCopy(folder + "/Stale.fbx"));
                Assert.That(File.Exists(folder + "/Stale.fbx"), Is.False);
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static string CreateFolder()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/__FPBlendShapeRenameTests");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(path));
            return path;
        }

        private static byte[] BinaryFixture(int version)
        {
            bool wide = version >= 7500;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("Kaydara FBX Binary  \0\x1a\0"));
                writer.Write(version);
                Node(writer, wide, "Objects", null, () =>
                {
                    Node(writer, wide, "Deformer", new[] { Long(400), Text(OldName + "\0\x01SubDeformer"), Text("BlendShapeChannel") });
                    Node(writer, wide, "Deformer", new[] { Long(401), Text("blink\0\x01SubDeformer"), Text("BlendShapeChannel") });
                    Node(writer, wide, "Opaque", new[] { Text(OldName), new byte[] { (byte)'R', 5, 0, 0, 0, 0, 255, 9, 13, 10 } });
                });
                Node(writer, wide, "Connections", null, () =>
                {
                    Node(writer, wide, "C", new[] { Text("OO"), Long(400), Long(300) });
                    Node(writer, wide, "C", new[] { Text("OO"), Long(401), Long(300) });
                });
                writer.Write(new byte[wide ? 25 : 13]);
                writer.Write(Encoding.ASCII.GetBytes("OPAQUE_FOOTER_" + OldName));
                return stream.ToArray();
            }
        }

        private static void Node(BinaryWriter writer, bool wide, string name, byte[][] properties, Action children = null)
        {
            long start = writer.BaseStream.Position;
            writer.Write(new byte[wide ? 24 : 12]);
            writer.Write((byte)name.Length);
            writer.Write(Encoding.ASCII.GetBytes(name));
            int size = 0;
            if (properties != null) foreach (byte[] property in properties) { writer.Write(property); size += property.Length; }
            if (children != null) { children(); writer.Write(new byte[wide ? 25 : 13]); }
            long end = writer.BaseStream.Position;
            writer.BaseStream.Position = start;
            if (wide) { writer.Write(end); writer.Write((long)(properties?.Length ?? 0)); writer.Write((long)size); }
            else { writer.Write((uint)end); writer.Write((uint)(properties?.Length ?? 0)); writer.Write((uint)size); }
            writer.BaseStream.Position = end;
        }

        private static byte[] Long(long value) => new[] { (byte)'L' }.Concat(BitConverter.GetBytes(value)).ToArray();
        private static byte[] Text(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            return new[] { (byte)'S' }.Concat(BitConverter.GetBytes(bytes.Length)).Concat(bytes).ToArray();
        }

        private const string AsciiFixture = @"; FBX 7.4.0 project file
FBXHeaderExtension: {
 FBXHeaderVersion: 1003
 FBXVersion: 7400
}
GlobalSettings: {
 Version: 1000
 Properties70: {
  P: ""UpAxis"", ""int"", ""Integer"", """",1
  P: ""UpAxisSign"", ""int"", ""Integer"", """",1
  P: ""FrontAxis"", ""int"", ""Integer"", """",2
  P: ""FrontAxisSign"", ""int"", ""Integer"", """",-1
  P: ""CoordAxis"", ""int"", ""Integer"", """",0
  P: ""CoordAxisSign"", ""int"", ""Integer"", """",1
  P: ""UnitScaleFactor"", ""double"", ""Number"", """",100
 }
}
Definitions: {
 Version: 100
 Count: 5
 ObjectType: ""Geometry"" {
  Count: 2
 }
 ObjectType: ""Deformer"" {
  Count: 2
 }
 ObjectType: ""Model"" {
  Count: 1
 }
}
Objects: {
 Geometry: 100, ""Geometry::Triangle"", ""Mesh"" {
  Vertices: *9 {
   a: 0,0,0,1,0,0,0,1,0
  }
  PolygonVertexIndex: *3 {
   a: 0,1,-3
  }
  GeometryVersion: 124
 }
 Geometry: 200, ""Geometry::jawFowad"", ""Shape"" {
  Version: 100
  Indexes: *3 {
   a: 0,1,2
  }
  Vertices: *9 {
   a: 0,0.1,0,0,0.2,0,0,0.3,0
  }
  Normals: *9 {
   a: 0,0,0,0,0,0,0,0,0
  }
 }
 Deformer: 300, ""Deformer::blendShape1"", ""BlendShape"" {
  Version: 100
 }
 Deformer: 400, ""SubDeformer::blendShape1.jawFowad"", ""BlendShapeChannel"" {
  Version: 100
  DeformPercent: 0
  FullWeights: *1 {
   a: 100
  }
 }
 Model: 500, ""Model::Triangle"", ""Mesh"" {
  Version: 232
  Properties70: {
   P: ""Lcl Translation"", ""Lcl Translation"", """", ""A"",0,0,0
   P: ""Lcl Rotation"", ""Lcl Rotation"", """", ""A"",0,0,0
   P: ""Lcl Scaling"", ""Lcl Scaling"", """", ""A"",1,1,1
  }
  Shading: T
  Culling: ""CullingOff""
 }
}
Connections: {
 C: ""OO"",100,500
 C: ""OO"",500,0
 C: ""OO"",300,100
 C: ""OO"",400,300
 C: ""OO"",200,400
}
";
    }
}
