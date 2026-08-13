// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Object = UnityEngine.Object;

    /// <summary>
    /// Runtime-safe settings for exporting a GameObject hierarchy as an OBJ package.
    /// The package is a ZIP containing OBJ, optional MTL, and optional PNG textures.
    /// </summary>
    [Serializable]
    public sealed class FPMeshRuntimeObjExportOptions
    {
        public bool IncludeChildren = true;
        public bool IncludeInactive = true;
        public bool IncludeMeshFilters = true;
        public bool IncludeSkinnedMeshRenderers = true;
        public bool IncludeMeshColliders;
        public bool ExportMaterials = true;
        public bool ExportTextures;
        public bool RootLocalSpace = true;
        public bool FlipNormals;
        public bool MirrorX = true;
        public int MaximumVertexCount;
        public int MaximumTextureSize = 2048;
    }

    /// <summary>
    /// In-memory OBJ package that can be downloaded, shared, saved, or tested by a caller.
    /// </summary>
    public sealed class FPMeshRuntimeObjExportResult
    {
        private readonly IReadOnlyList<string> _warnings;

        internal FPMeshRuntimeObjExportResult(
            string fileName,
            byte[] data,
            int exportedMeshCount,
            int vertexCount,
            IReadOnlyList<string> warnings)
        {
            FileName = fileName;
            Data = data;
            ExportedMeshCount = exportedMeshCount;
            VertexCount = vertexCount;
            _warnings = warnings ?? Array.Empty<string>();
        }

        public string FileName { get; }
        public byte[] Data { get; }
        public string MimeType => "application/zip";
        public int ExportedMeshCount { get; }
        public int VertexCount { get; }
        public IReadOnlyList<string> Warnings => _warnings;
    }

    /// <summary>
    /// Builds an OBJ package entirely in memory so it can run in a player, including WebGL.
    /// It follows the FP Combine Meshes exporter source rules without depending on UnityEditor.
    /// </summary>
    public static class FPMeshRuntimeObjExporter
    {
        private const string DefaultMaterialName = "FP_Default";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>
        /// Builds a ZIP containing the hierarchy's OBJ data and optional material assets.
        /// </summary>
        public static bool TryBuildPackage(
            GameObject root,
            FPMeshRuntimeObjExportOptions options,
            out FPMeshRuntimeObjExportResult result,
            out string message)
        {
            result = null;
            message = string.Empty;
            if (root == null)
            {
                message = "No GameObject was provided for OBJ export.";
                return false;
            }

            options = options ?? new FPMeshRuntimeObjExportOptions();
            var warnings = new List<string>();
            List<RuntimeExportSource> sources = CollectSources(root, options);
            try
            {
                if (sources.Count == 0)
                {
                    message = "No supported mesh sources were found in the selected hierarchy.";
                    return false;
                }

                int readableVertexCount = 0;
                for (int i = 0; i < sources.Count; i++)
                {
                    Mesh mesh = sources[i].Mesh;
                    if (mesh == null || !mesh.isReadable)
                    {
                        string meshName = mesh == null ? sources[i].Name : mesh.name;
                        warnings.Add($"Skipped unreadable mesh '{meshName}'. Enable Read/Write in its import settings.");
                        continue;
                    }

                    readableVertexCount += mesh.vertexCount;
                }

                if (readableVertexCount == 0)
                {
                    message = warnings.Count == 0
                        ? "No readable mesh data was found to export."
                        : warnings[0];
                    return false;
                }

                if (options.MaximumVertexCount > 0 &&
                    readableVertexCount > options.MaximumVertexCount)
                {
                    message =
                        $"OBJ export contains {readableVertexCount:N0} vertices, exceeding the configured limit of {options.MaximumVertexCount:N0}.";
                    return false;
                }

                string packageBaseName = SanitizeFileName(root.name);
                string objFileName = packageBaseName + ".obj";
                string mtlFileName = packageBaseName + ".mtl";
                int initialObjCapacity = (int)Math.Min(
                    4L * 1024L * 1024L,
                    Math.Max(4096L, readableVertexCount * 24L));
                var objBuilder = new StringBuilder(initialObjCapacity);
                var mtlBuilder = new StringBuilder(2048);
                var textureFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                var materialNames = new Dictionary<Material, string>();
                var writtenMaterials = new HashSet<Material>();
                var usedMaterialNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var textureNames = new Dictionary<Texture, string>();
                var usedTextureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                objBuilder.AppendLine("# FuzzPhyte Runtime OBJ Export");
                if (options.ExportMaterials)
                {
                    usedMaterialNames.Add(DefaultMaterialName);
                    objBuilder.Append("mtllib ").AppendLine(mtlFileName);
                }

                int vertexOffset = 1;
                int uvOffset = 1;
                int normalOffset = 1;
                int exportedMeshCount = 0;
                int exportedVertexCount = 0;
                int exportedFaceCount = 0;
                bool defaultMaterialWritten = false;

                for (int i = 0; i < sources.Count; i++)
                {
                    RuntimeExportSource source = sources[i];
                    Mesh mesh = source.Mesh;
                    if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0)
                    {
                        continue;
                    }

                    Vector3[] vertices = mesh.vertices;
                    Vector2[] uv = mesh.uv;
                    Vector3[] normals = mesh.normals;
                    bool hasUv = uv != null && uv.Length == vertices.Length;
                    bool hasNormals = normals != null && normals.Length == vertices.Length;
                    Matrix4x4 normalMatrix = source.Matrix.inverse.transpose;

                    objBuilder.Append("o ").AppendLine(SanitizeObjName(source.Name));
                    for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                    {
                        Vector3 position = source.Matrix.MultiplyPoint3x4(vertices[vertexIndex]);
                        if (options.MirrorX)
                        {
                            position.x = -position.x;
                        }

                        objBuilder.Append("v ")
                            .Append(Float(position.x)).Append(' ')
                            .Append(Float(position.y)).Append(' ')
                            .Append(Float(position.z)).AppendLine();
                    }

                    if (hasUv)
                    {
                        for (int uvIndex = 0; uvIndex < uv.Length; uvIndex++)
                        {
                            objBuilder.Append("vt ")
                                .Append(Float(uv[uvIndex].x)).Append(' ')
                                .Append(Float(uv[uvIndex].y)).AppendLine();
                        }
                    }

                    if (hasNormals)
                    {
                        for (int normalIndex = 0; normalIndex < normals.Length; normalIndex++)
                        {
                            Vector3 normal = normalMatrix.MultiplyVector(normals[normalIndex]).normalized;
                            if (options.MirrorX)
                            {
                                normal.x = -normal.x;
                            }
                            if (options.FlipNormals)
                            {
                                normal = -normal;
                            }

                            objBuilder.Append("vn ")
                                .Append(Float(normal.x)).Append(' ')
                                .Append(Float(normal.y)).Append(' ')
                                .Append(Float(normal.z)).AppendLine();
                        }
                    }

                    int subMeshCount = mesh.subMeshCount;
                    for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                    {
                        MeshTopology topology = mesh.GetTopology(subMesh);
                        if (topology != MeshTopology.Triangles && topology != MeshTopology.Quads)
                        {
                            warnings.Add(
                                $"Skipped unsupported {topology} topology on '{mesh.name}' submesh {subMesh}.");
                            continue;
                        }

                        if (options.ExportMaterials)
                        {
                            Material material = ResolveMaterial(source.Materials, subMesh);
                            string materialName = GetMaterialName(
                                material,
                                materialNames,
                                usedMaterialNames);
                            objBuilder.Append("usemtl ").AppendLine(materialName);
                            if (material == null)
                            {
                                if (!defaultMaterialWritten)
                                {
                                    WriteMaterial(
                                        null,
                                        DefaultMaterialName,
                                        options,
                                        mtlBuilder,
                                        textureFiles,
                                        textureNames,
                                        usedTextureNames,
                                        warnings);
                                    defaultMaterialWritten = true;
                                }
                            }
                            else if (writtenMaterials.Add(material))
                            {
                                WriteMaterial(
                                    material,
                                    materialName,
                                    options,
                                    mtlBuilder,
                                    textureFiles,
                                    textureNames,
                                    usedTextureNames,
                                    warnings);
                            }
                        }

                        int[] indices = mesh.GetIndices(subMesh);
                        int faceSize = topology == MeshTopology.Quads ? 4 : 3;
                        bool reverseWinding = options.FlipNormals ^
                                              options.MirrorX ^
                                              (source.Matrix.determinant < 0f);
                        for (int index = 0; index + faceSize - 1 < indices.Length; index += faceSize)
                        {
                            objBuilder.Append('f');
                            for (int corner = 0; corner < faceSize; corner++)
                            {
                                int sourceCorner = reverseWinding ? faceSize - 1 - corner : corner;
                                int meshVertexIndex = indices[index + sourceCorner];
                                objBuilder.Append(' ').Append(BuildFaceIndex(
                                    meshVertexIndex,
                                    vertexOffset,
                                    uvOffset,
                                    normalOffset,
                                    hasUv,
                                    hasNormals));
                            }
                            objBuilder.AppendLine();
                            exportedFaceCount++;
                        }
                    }

                    vertexOffset += vertices.Length;
                    uvOffset += hasUv ? uv.Length : 0;
                    normalOffset += hasNormals ? normals.Length : 0;
                    exportedVertexCount += vertices.Length;
                    exportedMeshCount++;
                }

                if (exportedMeshCount == 0 || exportedFaceCount == 0)
                {
                    message = exportedMeshCount == 0
                        ? "No readable mesh data was exported."
                        : "No triangle or quad faces were found to export.";
                    return false;
                }

                byte[] packageData = BuildZip(
                    objFileName,
                    objBuilder.ToString(),
                    options.ExportMaterials ? mtlFileName : null,
                    mtlBuilder.ToString(),
                    textureFiles);
                result = new FPMeshRuntimeObjExportResult(
                    packageBaseName + "_OBJ.zip",
                    packageData,
                    exportedMeshCount,
                    exportedVertexCount,
                    warnings.ToArray());
                message = warnings.Count == 0
                    ? $"Built an OBJ package from {exportedMeshCount} mesh source(s)."
                    : $"Built an OBJ package from {exportedMeshCount} mesh source(s) with {warnings.Count} warning(s).";
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                DestroyTemporarySources(sources);
            }
        }

        private static List<RuntimeExportSource> CollectSources(
            GameObject root,
            FPMeshRuntimeObjExportOptions options)
        {
            var result = new List<RuntimeExportSource>();
            Matrix4x4 rootToLocal = options.RootLocalSpace
                ? root.transform.worldToLocalMatrix
                : Matrix4x4.identity;
            var includedComponents = new HashSet<Component>();

            if (options.IncludeMeshFilters)
            {
                MeshFilter[] filters = options.IncludeChildren
                    ? root.GetComponentsInChildren<MeshFilter>(options.IncludeInactive)
                    : root.GetComponents<MeshFilter>();
                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (filter == null || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    result.Add(new RuntimeExportSource(
                        filter.gameObject.name,
                        filter.sharedMesh,
                        rootToLocal * filter.transform.localToWorldMatrix,
                        renderer == null ? null : renderer.sharedMaterials));
                    includedComponents.Add(filter);
                }
            }

            if (options.IncludeSkinnedMeshRenderers)
            {
                SkinnedMeshRenderer[] renderers = options.IncludeChildren
                    ? root.GetComponentsInChildren<SkinnedMeshRenderer>(options.IncludeInactive)
                    : root.GetComponents<SkinnedMeshRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    SkinnedMeshRenderer renderer = renderers[i];
                    if (renderer == null || renderer.sharedMesh == null)
                    {
                        continue;
                    }

                    var bakedMesh = new Mesh { name = renderer.sharedMesh.name + "_Baked" };
                    renderer.BakeMesh(bakedMesh);
                    result.Add(new RuntimeExportSource(
                        renderer.gameObject.name,
                        bakedMesh,
                        rootToLocal * renderer.transform.localToWorldMatrix,
                        renderer.sharedMaterials,
                        true));
                    includedComponents.Add(renderer);
                }
            }

            if (options.IncludeMeshColliders)
            {
                MeshCollider[] colliders = options.IncludeChildren
                    ? root.GetComponentsInChildren<MeshCollider>(options.IncludeInactive)
                    : root.GetComponents<MeshCollider>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    MeshCollider collider = colliders[i];
                    if (collider == null || collider.sharedMesh == null)
                    {
                        continue;
                    }

                    MeshFilter filter = collider.GetComponent<MeshFilter>();
                    SkinnedMeshRenderer renderer = collider.GetComponent<SkinnedMeshRenderer>();
                    if ((filter != null && includedComponents.Contains(filter)) ||
                        (renderer != null && includedComponents.Contains(renderer)))
                    {
                        continue;
                    }

                    MeshRenderer meshRenderer = collider.GetComponent<MeshRenderer>();
                    result.Add(new RuntimeExportSource(
                        collider.gameObject.name + "_Collider",
                        collider.sharedMesh,
                        rootToLocal * collider.transform.localToWorldMatrix,
                        meshRenderer == null ? null : meshRenderer.sharedMaterials));
                }
            }

            return result;
        }

        private static byte[] BuildZip(
            string objFileName,
            string objText,
            string mtlFileName,
            string mtlText,
            IReadOnlyDictionary<string, byte[]> textureFiles)
        {
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    WriteTextEntry(archive, objFileName, objText);
                    if (!string.IsNullOrWhiteSpace(mtlFileName))
                    {
                        WriteTextEntry(archive, mtlFileName, mtlText);
                    }

                    foreach (KeyValuePair<string, byte[]> textureFile in textureFiles)
                    {
                        ZipArchiveEntry entry = archive.CreateEntry(
                            textureFile.Key,
                            System.IO.Compression.CompressionLevel.Optimal);
                        using (Stream entryStream = entry.Open())
                        {
                            entryStream.Write(textureFile.Value, 0, textureFile.Value.Length);
                        }
                    }
                }

                return stream.ToArray();
            }
        }

        private static void WriteTextEntry(ZipArchive archive, string fileName, string text)
        {
            ZipArchiveEntry entry = archive.CreateEntry(
                fileName,
                System.IO.Compression.CompressionLevel.Optimal);
            using (Stream entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream, Utf8WithoutBom))
            {
                writer.Write(text ?? string.Empty);
            }
        }

        private static void WriteMaterial(
            Material material,
            string materialName,
            FPMeshRuntimeObjExportOptions options,
            StringBuilder mtlBuilder,
            IDictionary<string, byte[]> textureFiles,
            IDictionary<Texture, string> textureNames,
            ISet<string> usedTextureNames,
            ICollection<string> warnings)
        {
            Color color = ResolveMaterialColor(material);
            mtlBuilder.Append("newmtl ").AppendLine(materialName)
                .Append("Kd ").Append(Float(color.r)).Append(' ')
                .Append(Float(color.g)).Append(' ')
                .Append(Float(color.b)).AppendLine()
                .Append("d ").AppendLine(Float(color.a));

            if (options.ExportTextures)
            {
                Texture texture = ResolveMainTexture(material);
                if (texture != null)
                {
                    try
                    {
                        if (!textureNames.TryGetValue(texture, out string textureName))
                        {
                            textureName = GetUniqueName(
                                SanitizeFileName(texture.name) + ".png",
                                usedTextureNames);
                            textureFiles[textureName] = EncodeTextureToPng(
                                texture,
                                Mathf.Max(1, options.MaximumTextureSize));
                            textureNames[texture] = textureName;
                        }

                        mtlBuilder.Append("map_Kd ").AppendLine(textureName);
                    }
                    catch (Exception exception)
                    {
                        warnings.Add(
                            $"Could not export texture '{texture.name}': {exception.Message}");
                    }
                }
            }

            mtlBuilder.AppendLine();
        }

        private static byte[] EncodeTextureToPng(Texture source, int maximumSize)
        {
            int sourceWidth = Mathf.Max(1, source.width);
            int sourceHeight = Mathf.Max(1, source.height);
            float scale = Mathf.Min(1f, maximumSize / (float)Mathf.Max(sourceWidth, sourceHeight));
            int width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);
                return readable.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                DestroyObject(readable);
            }
        }

        private static Color ResolveMaterialColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }
            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }
            if (material.HasProperty(ColorId))
            {
                return material.GetColor(ColorId);
            }
            return Color.white;
        }

        private static Texture ResolveMainTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }
            if (material.HasProperty(BaseMapId))
            {
                return material.GetTexture(BaseMapId);
            }
            if (material.HasProperty(MainTextureId))
            {
                return material.GetTexture(MainTextureId);
            }
            return material.mainTexture;
        }

        private static Material ResolveMaterial(Material[] materials, int subMeshIndex)
        {
            if (materials == null || materials.Length == 0)
            {
                return null;
            }
            return materials[Mathf.Clamp(subMeshIndex, 0, materials.Length - 1)];
        }

        private static string GetMaterialName(
            Material material,
            IDictionary<Material, string> materialNames,
            ISet<string> usedNames)
        {
            if (material == null)
            {
                return DefaultMaterialName;
            }
            if (materialNames.TryGetValue(material, out string existingName))
            {
                return existingName;
            }

            string materialName = GetUniqueName(SanitizeObjName(material.name), usedNames);
            materialNames[material] = materialName;
            return materialName;
        }

        private static string GetUniqueName(string requestedName, ISet<string> usedNames)
        {
            string extension = Path.GetExtension(requestedName);
            string stem = string.IsNullOrEmpty(extension)
                ? requestedName
                : Path.GetFileNameWithoutExtension(requestedName);
            string candidate = requestedName;
            int suffix = 1;
            while (!usedNames.Add(candidate))
            {
                candidate = stem + "_" + suffix + extension;
                suffix++;
            }
            return candidate;
        }

        private static string BuildFaceIndex(
            int meshVertexIndex,
            int vertexOffset,
            int uvOffset,
            int normalOffset,
            bool hasUv,
            bool hasNormals)
        {
            int vertexIndex = vertexOffset + meshVertexIndex;
            if (hasUv && hasNormals)
            {
                return $"{vertexIndex}/{uvOffset + meshVertexIndex}/{normalOffset + meshVertexIndex}";
            }
            if (hasUv)
            {
                return $"{vertexIndex}/{uvOffset + meshVertexIndex}";
            }
            if (hasNormals)
            {
                return $"{vertexIndex}//{normalOffset + meshVertexIndex}";
            }
            return vertexIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string SanitizeObjName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Mesh";
            }
            return value.Trim().Replace(' ', '_').Replace('\t', '_').Replace('\r', '_').Replace('\n', '_');
        }

        private static string SanitizeFileName(string value)
        {
            string safeName = SanitizeObjName(value);
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                safeName = safeName.Replace(invalidCharacters[i], '_');
            }
            return string.IsNullOrWhiteSpace(safeName) ? "FP_MeshExport" : safeName;
        }

        private static void DestroyTemporarySources(IReadOnlyList<RuntimeExportSource> sources)
        {
            if (sources == null)
            {
                return;
            }
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].DestroyMeshAfterExport)
                {
                    DestroyObject(sources[i].Mesh);
                }
            }
        }

        private static void DestroyObject(Object value)
        {
            if (value == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Object.Destroy(value);
            }
            else
            {
                Object.DestroyImmediate(value);
            }
        }

        private sealed class RuntimeExportSource
        {
            public RuntimeExportSource(
                string name,
                Mesh mesh,
                Matrix4x4 matrix,
                Material[] materials,
                bool destroyMeshAfterExport = false)
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Mesh" : name;
                Mesh = mesh;
                Matrix = matrix;
                Materials = materials;
                DestroyMeshAfterExport = destroyMeshAfterExport;
            }

            public string Name { get; }
            public Mesh Mesh { get; }
            public Matrix4x4 Matrix { get; }
            public Material[] Materials { get; }
            public bool DestroyMeshAfterExport { get; }
        }
    }
}
