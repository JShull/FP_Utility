namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public enum FPMeshObjMaterialExportMode
    {
        PreserveMaterialsAndTextures,
        GenericWhiteMaterial,
        SingleAlbedoAtlas
    }

    public enum FPMeshObjAtlasUvTransform
    {
        None,
        FlipU,
        FlipV,
        Rotate180
    }

    public sealed class FPMeshExportSource
    {
        public string Name;
        public string GroupName;
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public Material[] Materials;
        public Object SourceObject;
        public bool DestroyMeshAfterExport;

        public FPMeshExportSource(string name, Mesh mesh, Matrix4x4 matrix, Material[] materials = null, Object sourceObject = null, bool destroyMeshAfterExport = false, string groupName = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Mesh" : name;
            GroupName = groupName;
            Mesh = mesh;
            Matrix = matrix;
            Materials = materials;
            SourceObject = sourceObject;
            DestroyMeshAfterExport = destroyMeshAfterExport;
        }
    }

    public sealed class FPMeshExportOptions
    {
        public bool ExportMaterials = true;
        public bool CopyTextures = true;
        public bool IncludeChildren = true;
        public bool IncludeInactive = true;
        public bool IncludeMeshFilters = true;
        public bool IncludeSkinnedMeshRenderers = true;
        public bool IncludeMeshColliders = true;
        public bool RootLocalSpace = true;
        public bool FlipNormals = false;
        public bool MirrorX = false;
        public FPMeshObjMaterialExportMode MaterialExportMode = FPMeshObjMaterialExportMode.PreserveMaterialsAndTextures;
        public int AtlasSize = 4096;
        public int AtlasPadding = 4;
        public string AtlasAlbedoPropertyFallbacks = "overlayTexture_0";
        public FPMeshObjAtlasUvTransform AtlasUvTransform = FPMeshObjAtlasUvTransform.Rotate180;
    }

    internal sealed class FPMeshObjExportSource
    {
        public string Name;
        public string GroupName;
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public Material[] Materials;
        public bool DestroyMeshAfterExport;

        public FPMeshObjExportSource(string name, Mesh mesh, Matrix4x4 matrix, Material[] materials = null, bool destroyMeshAfterExport = false, string groupName = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Mesh" : name;
            GroupName = groupName;
            Mesh = mesh;
            Matrix = matrix;
            Materials = materials;
            DestroyMeshAfterExport = destroyMeshAfterExport;
        }
    }

    internal sealed class FPMeshObjExportOptions
    {
        public bool ExportMaterials = true;
        public bool CopyTextures = true;
        public bool IncludeChildren = true;
        public bool IncludeInactive = true;
        public bool IncludeMeshFilters = true;
        public bool IncludeSkinnedMeshRenderers = true;
        public bool IncludeMeshColliders = true;
        public bool RootLocalSpace = true;
        public bool FlipNormals = false;
        public bool MirrorX = false;
        public FPMeshObjMaterialExportMode MaterialExportMode = FPMeshObjMaterialExportMode.PreserveMaterialsAndTextures;
        public int AtlasSize = 4096;
        public int AtlasPadding = 4;
        public string AtlasAlbedoPropertyFallbacks = "overlayTexture_0";
        public FPMeshObjAtlasUvTransform AtlasUvTransform = FPMeshObjAtlasUvTransform.Rotate180;
    }

    internal static class FPMeshObjExportUtility
    {
        private const string DefaultMaterialName = "FP_Default";
        private const string GenericWhiteMaterialName = "FP_GenericWhite";
        private const string AtlasMaterialName = "FP_AlbedoAtlas";

        [MenuItem("GameObject/FuzzPhyte/Mesh/Export Selection as OBJ", false, 30)]
        private static void ExportSelectedGameObjectMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            ExportGameObjectWithDialog(selected, new FPMeshObjExportOptions());
        }

        [MenuItem("GameObject/FuzzPhyte/Mesh/Export Selection as OBJ", true)]
        private static bool ValidateExportSelectedGameObjectMenu()
        {
            return Selection.activeGameObject != null && HasExportableMeshes(Selection.activeGameObject);
        }

        [MenuItem("Assets/FuzzPhyte/Mesh/Export Selected as OBJ", false, 30)]
        private static void ExportSelectedAssetMenu()
        {
            Object selected = Selection.activeObject;
            if (selected == null)
            {
                return;
            }

            if (selected is GameObject gameObject)
            {
                ExportGameObjectWithDialog(gameObject, new FPMeshObjExportOptions());
                return;
            }

            if (selected is Mesh mesh)
            {
                var sources = new List<FPMeshObjExportSource>
                {
                    new FPMeshObjExportSource(mesh.name, mesh, Matrix4x4.identity)
                };
                ExportSourcesWithDialog(sources, mesh.name, new FPMeshObjExportOptions());
            }
        }

        [MenuItem("Assets/FuzzPhyte/Mesh/Export Selected as OBJ", true)]
        private static bool ValidateExportSelectedAssetMenu()
        {
            Object selected = Selection.activeObject;
            return selected is Mesh || selected is GameObject;
        }

        public static bool ExportGameObjectWithDialog(GameObject root, FPMeshObjExportOptions options)
        {
            if (root == null)
            {
                EditorUtility.DisplayDialog("Export OBJ", "No GameObject was selected.", "OK");
                return false;
            }

            List<FPMeshObjExportSource> sources = CollectGameObjectSources(root, options);
            return ExportSourcesWithDialog(sources, root.name, options);
        }

        public static bool ExportSourcesWithDialog(IList<FPMeshObjExportSource> sources, string defaultName, FPMeshObjExportOptions options)
        {
            if (options == null)
            {
                options = new FPMeshObjExportOptions();
            }

            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(defaultName) ? "FP_MeshExport" : defaultName);
            string path = EditorUtility.SaveFilePanel("Export OBJ", Application.dataPath, safeName, "obj");
            if (string.IsNullOrEmpty(path))
            {
                DestroyTemporarySources(sources);
                return false;
            }

            bool success = ExportSources(sources, path, options, out string message);
            EditorUtility.DisplayDialog(success ? "Export OBJ" : "Export OBJ Failed", message, "OK");
            return success;
        }

        public static bool ExportSources(IList<FPMeshObjExportSource> sources, string objPath, FPMeshObjExportOptions options, out string message)
        {
            if (options == null)
            {
                options = new FPMeshObjExportOptions();
            }

            try
            {
                if (sources == null || sources.Count == 0)
                {
                    message = "No mesh sources were found to export.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(objPath))
                {
                    message = "No OBJ path was provided.";
                    return false;
                }

                string directory = Path.GetDirectoryName(objPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    message = "Could not resolve an export folder.";
                    return false;
                }

                Directory.CreateDirectory(directory);

                string objFileName = Path.GetFileNameWithoutExtension(objPath);
                string mtlFileName = SanitizeFileName(objFileName) + ".mtl";
                string mtlPath = Path.Combine(directory, mtlFileName);
                var mtlBuilder = new StringBuilder(2048);
                var materialNames = new Dictionary<Material, string>();
                var copiedTextures = new Dictionary<Texture, string>();
                FPMeshObjTextureAtlasContext atlasContext = null;
                if (options.ExportMaterials && options.MaterialExportMode == FPMeshObjMaterialExportMode.SingleAlbedoAtlas)
                {
                    atlasContext = BuildAlbedoAtlasContext(sources, directory, SanitizeFileName(objFileName), options);
                }

                int vertexOffset = 1;
                int uvOffset = 1;
                int normalOffset = 1;
                int exportedMeshes = 0;
                string currentGroupName = null;

                using (var objWriter = new StreamWriter(objPath, false, Encoding.UTF8))
                {
                    objWriter.WriteLine("# FuzzPhyte OBJ Export");
                    if (options.ExportMaterials)
                    {
                        objWriter.Write("mtllib ");
                        objWriter.WriteLine(mtlFileName.Replace("\\", "/"));
                        if (options.MaterialExportMode == FPMeshObjMaterialExportMode.GenericWhiteMaterial)
                        {
                            WriteGenericWhiteMaterial(mtlBuilder, GenericWhiteMaterialName);
                        }
                        else if (atlasContext != null)
                        {
                            WriteAtlasMaterial(mtlBuilder, AtlasMaterialName, atlasContext.RelativeTexturePath);
                        }
                    }

                    for (int i = 0; i < sources.Count; i++)
                    {
                        FPMeshObjExportSource source = sources[i];
                        if (source == null || source.Mesh == null)
                        {
                            continue;
                        }

                        Mesh mesh = source.Mesh;
                        if (!mesh.isReadable)
                        {
                            Debug.LogWarning($"[OBJ Export] Skipping unreadable mesh '{mesh.name}'. Enable Read/Write on the import settings to export it.");
                            continue;
                        }

                        Vector3[] vertices = mesh.vertices;
                        if (vertices == null || vertices.Length == 0)
                        {
                            continue;
                        }

                        Vector2[] uv = mesh.uv;
                        bool hasUv = uv != null && uv.Length == vertices.Length;
                        bool writeSourceUv = atlasContext == null &&
                                             hasUv &&
                                             options.MaterialExportMode != FPMeshObjMaterialExportMode.GenericWhiteMaterial;
                        Vector3[] normals = mesh.normals;
                        Matrix4x4 normalMatrix = source.Matrix.inverse.transpose;
                        string objectName = SanitizeObjName(source.Name);
                        string groupName = string.IsNullOrWhiteSpace(source.GroupName)
                            ? null
                            : SanitizeObjName(source.GroupName);

                        if (!string.IsNullOrEmpty(groupName) && groupName != currentGroupName)
                        {
                            objWriter.Write("g ");
                            objWriter.WriteLine(groupName);
                            currentGroupName = groupName;
                        }

                        objWriter.Write("o ");
                        objWriter.WriteLine(objectName);

                        for (int v = 0; v < vertices.Length; v++)
                        {
                            Vector3 position = source.Matrix.MultiplyPoint3x4(vertices[v]);
                            if (options.MirrorX)
                            {
                                position.x = -position.x;
                            }

                            objWriter.Write("v ");
                            objWriter.Write(Float(position.x));
                            objWriter.Write(' ');
                            objWriter.Write(Float(position.y));
                            objWriter.Write(' ');
                            objWriter.Write(Float(position.z));
                            objWriter.WriteLine();
                        }

                        if (writeSourceUv)
                        {
                            for (int u = 0; u < uv.Length; u++)
                            {
                                objWriter.Write("vt ");
                                objWriter.Write(Float(uv[u].x));
                                objWriter.Write(' ');
                                objWriter.Write(Float(uv[u].y));
                                objWriter.WriteLine();
                            }
                        }

                        bool hasNormals = normals != null && normals.Length == vertices.Length;
                        if (hasNormals)
                        {
                            for (int n = 0; n < normals.Length; n++)
                            {
                                Vector3 normal = normalMatrix.MultiplyVector(normals[n]).normalized;
                                if (options.MirrorX)
                                {
                                    normal.x = -normal.x;
                                }

                                if (options.FlipNormals)
                                {
                                    normal = -normal;
                                }

                                objWriter.Write("vn ");
                                objWriter.Write(Float(normal.x));
                                objWriter.Write(' ');
                                objWriter.Write(Float(normal.y));
                                objWriter.Write(' ');
                                objWriter.Write(Float(normal.z));
                                objWriter.WriteLine();
                            }
                        }

                        int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
                        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                        {
                            MeshTopology topology = mesh.GetTopology(subMesh);
                            if (topology != MeshTopology.Triangles && topology != MeshTopology.Quads)
                            {
                                Debug.LogWarning($"[OBJ Export] Skipping unsupported topology '{topology}' on '{mesh.name}' submesh {subMesh}.");
                                continue;
                            }

                            Material material = ResolveMaterial(source.Materials, subMesh);
                            string materialName = GetMaterialNameForMode(material, materialNames, options, atlasContext);
                            if (options.ExportMaterials)
                            {
                                objWriter.Write("usemtl ");
                                objWriter.WriteLine(materialName);
                                if (options.MaterialExportMode == FPMeshObjMaterialExportMode.PreserveMaterialsAndTextures)
                                {
                                    EnsureMaterialWritten(material, materialName, mtlBuilder, directory, options, copiedTextures);
                                }
                            }

                            int[] indices = mesh.GetIndices(subMesh);
                            int step = topology == MeshTopology.Quads ? 4 : 3;
                            for (int index = 0; index + step - 1 < indices.Length; index += step)
                            {
                                int[] atlasUvIndices = null;
                                if (atlasContext != null)
                                {
                                    atlasUvIndices = new int[step];
                                    for (int corner = 0; corner < step; corner++)
                                    {
                                        int vertexIndex = indices[index + corner];
                                        Vector2 sourceUv = hasUv ? uv[vertexIndex] : new Vector2(0.5f, 0.5f);
                                        Vector2 remappedUv = RemapAtlasUv(sourceUv, material, atlasContext);
                                        objWriter.Write("vt ");
                                        objWriter.Write(Float(remappedUv.x));
                                        objWriter.Write(' ');
                                        objWriter.Write(Float(remappedUv.y));
                                        objWriter.WriteLine();
                                        atlasUvIndices[corner] = uvOffset;
                                        uvOffset++;
                                    }
                                }

                                objWriter.Write('f');
                                bool reverseWinding = options.FlipNormals ^ options.MirrorX;
                                for (int corner = 0; corner < step; corner++)
                                {
                                    int sourceCorner = reverseWinding ? step - 1 - corner : corner;
                                    int vertexIndex = indices[index + sourceCorner];
                                    objWriter.Write(' ');
                                    if (atlasContext != null)
                                    {
                                        objWriter.Write(BuildFaceIndexWithUvIndex(vertexIndex, vertexOffset, atlasUvIndices[sourceCorner], normalOffset, hasNormals));
                                    }
                                    else
                                    {
                                        objWriter.Write(BuildFaceIndex(vertexIndex, vertexOffset, uvOffset, normalOffset, writeSourceUv, hasNormals));
                                    }
                                }

                                objWriter.WriteLine();
                            }
                        }

                        vertexOffset += vertices.Length;
                        if (atlasContext == null)
                        {
                            uvOffset += writeSourceUv ? uv.Length : 0;
                        }
                        normalOffset += hasNormals ? normals.Length : 0;
                        exportedMeshes++;
                    }

                    if (exportedMeshes == 0)
                    {
                        message = "No readable mesh data was exported.";
                        return false;
                    }
                }

                if (options.ExportMaterials)
                {
                    if (mtlBuilder.Length == 0)
                    {
                        EnsureMaterialWritten(null, DefaultMaterialName, mtlBuilder, directory, options, copiedTextures);
                    }

                    File.WriteAllText(mtlPath, mtlBuilder.ToString(), Encoding.UTF8);
                }
                AssetDatabase.Refresh();
                message = $"Exported {exportedMeshes} mesh source(s) to:\n{objPath}";
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

        public static List<FPMeshObjExportSource> CollectGameObjectSources(
            GameObject root,
            FPMeshObjExportOptions options,
            Predicate<GameObject> isValidObject = null,
            string groupName = null,
            Matrix4x4? rootToLocalOverride = null)
        {
            var result = new List<FPMeshObjExportSource>();
            if (root == null)
            {
                return result;
            }

            if (options == null)
            {
                options = new FPMeshObjExportOptions();
            }

            Matrix4x4 rootToLocal = rootToLocalOverride ?? (options.RootLocalSpace ? root.transform.worldToLocalMatrix : Matrix4x4.identity);
            var includedComponents = new HashSet<Component>();

            if (options.IncludeMeshFilters)
            {
                MeshFilter[] filters = options.IncludeChildren
                    ? root.GetComponentsInChildren<MeshFilter>(options.IncludeInactive)
                    : root.GetComponents<MeshFilter>();

                for (int i = 0; i < filters.Length; i++)
                {
                    MeshFilter filter = filters[i];
                    if (filter == null || filter.sharedMesh == null || !IsValid(filter.gameObject, isValidObject))
                    {
                        continue;
                    }

                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    result.Add(new FPMeshObjExportSource(
                        filter.gameObject.name,
                        filter.sharedMesh,
                        rootToLocal * filter.transform.localToWorldMatrix,
                        renderer == null ? null : renderer.sharedMaterials,
                        groupName: groupName));
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
                    if (renderer == null || renderer.sharedMesh == null || !IsValid(renderer.gameObject, isValidObject))
                    {
                        continue;
                    }

                    Mesh bakedMesh = new Mesh
                    {
                        name = renderer.sharedMesh.name + "_Baked"
                    };
                    renderer.BakeMesh(bakedMesh);
                    result.Add(new FPMeshObjExportSource(
                        renderer.gameObject.name,
                        bakedMesh,
                        rootToLocal * renderer.transform.localToWorldMatrix,
                        renderer.sharedMaterials,
                        true,
                        groupName));
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
                    if (collider == null || collider.sharedMesh == null || !IsValid(collider.gameObject, isValidObject))
                    {
                        continue;
                    }

                    MeshFilter filter = collider.GetComponent<MeshFilter>();
                    if (filter != null && includedComponents.Contains(filter))
                    {
                        continue;
                    }

                    SkinnedMeshRenderer renderer = collider.GetComponent<SkinnedMeshRenderer>();
                    if (renderer != null && includedComponents.Contains(renderer))
                    {
                        continue;
                    }

                    MeshRenderer meshRenderer = collider.GetComponent<MeshRenderer>();
                    result.Add(new FPMeshObjExportSource(
                        collider.gameObject.name + "_Collider",
                        collider.sharedMesh,
                        rootToLocal * collider.transform.localToWorldMatrix,
                        meshRenderer == null ? null : meshRenderer.sharedMaterials,
                        groupName: groupName));
                    includedComponents.Add(collider);
                }
            }

            return result;
        }

        private static bool HasExportableMeshes(GameObject root)
        {
            if (root == null)
            {
                return false;
            }

            return root.GetComponentInChildren<MeshFilter>(true) != null ||
                   root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                   root.GetComponentInChildren<MeshCollider>(true) != null;
        }

        private static bool IsValid(GameObject gameObject, Predicate<GameObject> isValidObject)
        {
            return gameObject != null && (isValidObject == null || isValidObject(gameObject));
        }

        private static Material ResolveMaterial(Material[] materials, int subMeshIndex)
        {
            if (materials == null || materials.Length == 0)
            {
                return null;
            }

            return materials[Mathf.Clamp(subMeshIndex, 0, materials.Length - 1)];
        }

        private static string GetMaterialName(Material material, Dictionary<Material, string> materialNames)
        {
            if (material == null)
            {
                return DefaultMaterialName;
            }

            if (materialNames.TryGetValue(material, out string existingName))
            {
                return existingName;
            }

            string baseName = SanitizeObjName(material.name);
            string materialName = baseName;
            int suffix = 1;
            while (materialNames.ContainsValue(materialName))
            {
                materialName = baseName + "_" + suffix;
                suffix++;
            }

            materialNames[material] = materialName;
            return materialName;
        }

        private static string GetMaterialNameForMode(Material material, Dictionary<Material, string> materialNames, FPMeshObjExportOptions options, FPMeshObjTextureAtlasContext atlasContext)
        {
            if (!options.ExportMaterials)
            {
                return string.Empty;
            }

            switch (options.MaterialExportMode)
            {
                case FPMeshObjMaterialExportMode.GenericWhiteMaterial:
                    return GenericWhiteMaterialName;
                case FPMeshObjMaterialExportMode.SingleAlbedoAtlas:
                    return atlasContext == null ? GenericWhiteMaterialName : AtlasMaterialName;
                default:
                    return GetMaterialName(material, materialNames);
            }
        }

        private sealed class FPMeshObjTextureAtlasContext
        {
            public readonly Dictionary<int, FPMeshObjTextureAtlasMapping> Mappings = new Dictionary<int, FPMeshObjTextureAtlasMapping>();
            public string RelativeTexturePath;
            public FPMeshObjAtlasUvTransform UvTransform;
        }

        private sealed class FPMeshObjTextureAtlasMapping
        {
            public Rect Rect;
            public Vector2 Scale;
            public Vector2 Offset;

            public FPMeshObjTextureAtlasMapping(Rect rect, Vector2 scale, Vector2 offset)
            {
                Rect = rect;
                Scale = scale;
                Offset = offset;
            }
        }

        private sealed class FPMeshObjTextureAtlasInput
        {
            public int MaterialKey;
            public Texture2D Texture;
            public Vector2 Scale = Vector2.one;
            public Vector2 Offset = Vector2.zero;
        }

        private static FPMeshObjTextureAtlasContext BuildAlbedoAtlasContext(IList<FPMeshObjExportSource> sources, string outputDirectory, string objFileName, FPMeshObjExportOptions options)
        {
            List<Material> materials = CollectAtlasMaterials(sources);
            var inputs = new List<FPMeshObjTextureAtlasInput>(materials.Count);

            for (int i = 0; i < materials.Count; i++)
            {
                inputs.Add(BuildAtlasInput(materials[i], options));
            }

            if (inputs.Count == 0)
            {
                inputs.Add(BuildAtlasInput(null, options));
            }

            Texture2D atlas = null;
            try
            {
                var textures = new Texture2D[inputs.Count];
                for (int i = 0; i < inputs.Count; i++)
                {
                    textures[i] = inputs[i].Texture;
                }

                int atlasSize = Mathf.Clamp(options.AtlasSize, 128, 16384);
                int padding = Mathf.Max(0, options.AtlasPadding);
                atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false)
                {
                    name = objFileName + "_AlbedoAtlas"
                };

                Rect[] rects = atlas.PackTextures(textures, padding, atlasSize);
                atlas.Apply();

                string atlasPath = GetUniqueFilePath(outputDirectory, objFileName + "_AlbedoAtlas", ".png");
                File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());

                var context = new FPMeshObjTextureAtlasContext
                {
                    RelativeTexturePath = Path.GetFileName(atlasPath),
                    UvTransform = options.AtlasUvTransform
                };

                for (int i = 0; i < rects.Length && i < inputs.Count; i++)
                {
                    context.Mappings[inputs[i].MaterialKey] = new FPMeshObjTextureAtlasMapping(rects[i], inputs[i].Scale, inputs[i].Offset);
                }

                return context;
            }
            finally
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    if (inputs[i].Texture != null)
                    {
                        Object.DestroyImmediate(inputs[i].Texture);
                    }
                }

                if (atlas != null)
                {
                    Object.DestroyImmediate(atlas);
                }
            }
        }

        private static List<Material> CollectAtlasMaterials(IList<FPMeshObjExportSource> sources)
        {
            var materials = new List<Material>();
            var materialKeys = new HashSet<int>();

            if (sources == null)
            {
                AddAtlasMaterial(null, materials, materialKeys);
                return materials;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                FPMeshObjExportSource source = sources[i];
                if (source == null || source.Mesh == null)
                {
                    continue;
                }

                int subMeshCount = Mathf.Max(1, source.Mesh.subMeshCount);
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    AddAtlasMaterial(ResolveMaterial(source.Materials, subMesh), materials, materialKeys);
                }
            }

            AddAtlasMaterial(null, materials, materialKeys);

            return materials;
        }

        private static void AddAtlasMaterial(Material material, List<Material> materials, HashSet<int> materialKeys)
        {
            int key = GetMaterialKey(material);
            if (materialKeys.Add(key))
            {
                materials.Add(material);
            }
        }

        private static FPMeshObjTextureAtlasInput BuildAtlasInput(Material material, FPMeshObjExportOptions options)
        {
            Texture texture = ResolveMaterialAlbedoTexture(material, options.AtlasAlbedoPropertyFallbacks, out string textureProperty);
            Vector2 scale = ResolveMaterialTextureScale(material, textureProperty);
            Vector2 offset = ResolveMaterialTextureOffset(material, textureProperty);
            Color tint = ResolveMaterialColor(material);

            Texture2D textureCopy = null;
            if (texture != null)
            {
                textureCopy = CopyTextureToReadable(texture);
            }

            if (textureCopy == null)
            {
                textureCopy = CreateSolidTexture(tint);
            }
            else
            {
                ApplyColorTint(textureCopy, tint);
            }

            return new FPMeshObjTextureAtlasInput
            {
                MaterialKey = GetMaterialKey(material),
                Texture = textureCopy,
                Scale = scale,
                Offset = offset
            };
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D CopyTextureToReadable(Texture texture)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = null;
            try
            {
                renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, renderTexture);
                RenderTexture.active = renderTexture;

                var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply();
                return readable;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OBJ Export] Could not read albedo texture '{texture.name}' for atlas export: {exception.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static void ApplyColorTint(Texture2D texture, Color tint)
        {
            if (texture == null || ApproximatelyColor(tint, Color.white))
            {
                return;
            }

            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= tint;
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }

        private static bool ApproximatelyColor(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) &&
                   Mathf.Approximately(a.a, b.a);
        }

        private static Vector2 RemapAtlasUv(Vector2 sourceUv, Material material, FPMeshObjTextureAtlasContext atlasContext)
        {
            if (atlasContext == null)
            {
                return sourceUv;
            }

            int key = GetMaterialKey(material);
            if (!atlasContext.Mappings.TryGetValue(key, out FPMeshObjTextureAtlasMapping mapping) &&
                !atlasContext.Mappings.TryGetValue(GetMaterialKey(null), out mapping))
            {
                return sourceUv;
            }

            Vector2 transformedUv = new Vector2(
                (sourceUv.x * mapping.Scale.x) + mapping.Offset.x,
                (sourceUv.y * mapping.Scale.y) + mapping.Offset.y);
            transformedUv = TransformAtlasTileUv(transformedUv, atlasContext.UvTransform);

            float atlasU = mapping.Rect.x + (transformedUv.x * mapping.Rect.width);
            float atlasV = mapping.Rect.y + (transformedUv.y * mapping.Rect.height);
            return new Vector2(atlasU, atlasV);
        }

        private static Vector2 TransformAtlasTileUv(Vector2 uv, FPMeshObjAtlasUvTransform transform)
        {
            switch (transform)
            {
                case FPMeshObjAtlasUvTransform.FlipU:
                    return new Vector2(1f - uv.x, uv.y);
                case FPMeshObjAtlasUvTransform.FlipV:
                    return new Vector2(uv.x, 1f - uv.y);
                case FPMeshObjAtlasUvTransform.Rotate180:
                    return new Vector2(1f - uv.x, 1f - uv.y);
                default:
                    return uv;
            }
        }

        private static void WriteGenericWhiteMaterial(StringBuilder builder, string materialName)
        {
            builder.Append("newmtl ").AppendLine(materialName);
            builder.AppendLine("Ka 0.200000 0.200000 0.200000");
            builder.AppendLine("Kd 1 1 1");
            builder.AppendLine("d 1");
            builder.AppendLine("illum 2");
            builder.AppendLine();
        }

        private static void WriteAtlasMaterial(StringBuilder builder, string materialName, string relativeTexturePath)
        {
            builder.Append("newmtl ").AppendLine(materialName);
            builder.AppendLine("Ka 0.200000 0.200000 0.200000");
            builder.AppendLine("Kd 1 1 1");
            builder.AppendLine("d 1");
            builder.AppendLine("illum 2");
            if (!string.IsNullOrWhiteSpace(relativeTexturePath))
            {
                builder.Append("map_Kd ").AppendLine(relativeTexturePath.Replace("\\", "/"));
            }

            builder.AppendLine();
        }

        private static void EnsureMaterialWritten(Material material, string materialName, StringBuilder builder, string outputDirectory, FPMeshObjExportOptions options, Dictionary<Texture, string> copiedTextures)
        {
            string token = "newmtl " + materialName;
            if (builder.ToString().Contains(token))
            {
                return;
            }

            Color color = ResolveMaterialColor(material);
            builder.Append("newmtl ").AppendLine(materialName);
            builder.AppendLine("Ka 0.200000 0.200000 0.200000");
            builder.Append("Kd ")
                .Append(Float(color.r)).Append(' ')
                .Append(Float(color.g)).Append(' ')
                .Append(Float(color.b)).AppendLine();
            builder.Append("d ").AppendLine(Float(color.a));
            builder.AppendLine("illum 2");

            Texture texture = ResolveMaterialTexture(material);
            if (options.CopyTextures && texture != null && TryCopyTexture(texture, outputDirectory, copiedTextures, out string relativeTexturePath))
            {
                builder.Append("map_Kd ").AppendLine(relativeTexturePath.Replace("\\", "/"));
            }

            builder.AppendLine();
        }

        private static Color ResolveMaterialColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        private static Texture ResolveMaterialTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap"))
            {
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null)
                {
                    return texture;
                }
            }

            if (material.HasProperty("_MainTex"))
            {
                Texture texture = material.GetTexture("_MainTex");
                if (texture != null)
                {
                    return texture;
                }
            }

            string[] textureProperties = material.GetTexturePropertyNames();
            for (int i = 0; i < textureProperties.Length; i++)
            {
                Texture texture = material.GetTexture(textureProperties[i]);
                if (texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        private static Texture ResolveMaterialAlbedoTexture(Material material, string fallbackPropertyNames, out string textureProperty)
        {
            textureProperty = null;
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_BaseMap"))
            {
                Texture texture = material.GetTexture("_BaseMap");
                if (texture != null)
                {
                    textureProperty = "_BaseMap";
                    return texture;
                }
            }

            if (material.HasProperty("_MainTex"))
            {
                Texture texture = material.GetTexture("_MainTex");
                if (texture != null)
                {
                    textureProperty = "_MainTex";
                    return texture;
                }
            }

            Texture fallbackTexture = ResolveMaterialTextureFromFallbacks(material, fallbackPropertyNames, out textureProperty);
            if (fallbackTexture != null)
            {
                return fallbackTexture;
            }

            string[] textureProperties = material.GetTexturePropertyNames();
            for (int i = 0; i < textureProperties.Length; i++)
            {
                string property = textureProperties[i];
                if (!IsLikelyAlbedoTextureProperty(property))
                {
                    continue;
                }

                Texture texture = material.GetTexture(property);
                if (texture != null)
                {
                    textureProperty = property;
                    return texture;
                }
            }

            return null;
        }

        private static Texture ResolveMaterialTextureFromFallbacks(Material material, string fallbackPropertyNames, out string textureProperty)
        {
            textureProperty = null;
            if (material == null || string.IsNullOrWhiteSpace(fallbackPropertyNames))
            {
                return null;
            }

            string[] names = fallbackPropertyNames.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                string property = names[i].Trim();
                if (string.IsNullOrEmpty(property) || !material.HasProperty(property))
                {
                    continue;
                }

                Texture texture = material.GetTexture(property);
                if (texture != null)
                {
                    textureProperty = property;
                    return texture;
                }
            }

            return null;
        }

        private static bool IsLikelyAlbedoTextureProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            string lowerName = propertyName.ToLowerInvariant();
            return lowerName.Contains("albedo") ||
                   lowerName.Contains("basemap") ||
                   lowerName.Contains("base_map") ||
                   lowerName.Contains("basecolor") ||
                   lowerName.Contains("base_color") ||
                   lowerName.Contains("diffuse") ||
                   lowerName.Contains("colormap") ||
                   lowerName.Contains("color_map") ||
                   lowerName.Contains("overlaytexture") ||
                   lowerName.Contains("overlay_texture") ||
                   lowerName.Contains("maintex") ||
                   lowerName.Contains("main_tex");
        }

        private static Vector2 ResolveMaterialTextureScale(Material material, string textureProperty)
        {
            if (material == null || string.IsNullOrEmpty(textureProperty) || !material.HasProperty(textureProperty))
            {
                return Vector2.one;
            }

            return material.GetTextureScale(textureProperty);
        }

        private static Vector2 ResolveMaterialTextureOffset(Material material, string textureProperty)
        {
            if (material == null || string.IsNullOrEmpty(textureProperty) || !material.HasProperty(textureProperty))
            {
                return Vector2.zero;
            }

            return material.GetTextureOffset(textureProperty);
        }

        private static int GetMaterialKey(Material material)
        {
            return material == null ? 0 : material.GetInstanceID();
        }

        private static bool TryCopyTexture(Texture texture, string outputDirectory, Dictionary<Texture, string> copiedTextures, out string relativePath)
        {
            relativePath = null;
            if (texture == null)
            {
                return false;
            }

            if (copiedTextures.TryGetValue(texture, out relativePath))
            {
                return true;
            }

            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return TrySnapshotRuntimeTexture(texture, outputDirectory, copiedTextures, out relativePath);
            }

            string sourcePath = Path.GetFullPath(assetPath);
            if (!File.Exists(sourcePath))
            {
                return TrySnapshotRuntimeTexture(texture, outputDirectory, copiedTextures, out relativePath);
            }

            string extension = Path.GetExtension(sourcePath);
            string fileName = SanitizeFileName(texture.name) + extension;
            string destinationPath = Path.Combine(outputDirectory, fileName);
            int suffix = 1;
            while (File.Exists(destinationPath) && !PathsEqual(destinationPath, sourcePath))
            {
                fileName = SanitizeFileName(texture.name) + "_" + suffix + extension;
                destinationPath = Path.Combine(outputDirectory, fileName);
                suffix++;
            }

            if (!PathsEqual(destinationPath, sourcePath))
            {
                File.Copy(sourcePath, destinationPath, true);
            }

            relativePath = Path.GetFileName(destinationPath);
            copiedTextures[texture] = relativePath;
            return true;
        }

        private static bool TrySnapshotRuntimeTexture(Texture texture, string outputDirectory, Dictionary<Texture, string> copiedTextures, out string relativePath)
        {
            relativePath = null;
            if (texture == null)
            {
                return false;
            }

            if (texture.width <= 0 || texture.height <= 0)
            {
                return false;
            }

            string fileName = SanitizeFileName(texture.name);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "FP_RuntimeTexture";
            }

            string destinationPath = Path.Combine(outputDirectory, fileName + ".png");
            int suffix = 1;
            while (File.Exists(destinationPath))
            {
                destinationPath = Path.Combine(outputDirectory, fileName + "_" + suffix + ".png");
                suffix++;
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = null;
            Texture2D readable = null;

            try
            {
                renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, renderTexture);
                RenderTexture.active = renderTexture;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply();
                File.WriteAllBytes(destinationPath, readable.EncodeToPNG());
                relativePath = Path.GetFileName(destinationPath);
                copiedTextures[texture] = relativePath;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OBJ Export] Could not snapshot runtime texture '{texture.name}': {exception.Message}");
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }

                if (readable != null)
                {
                    Object.DestroyImmediate(readable);
                }
            }
        }

        private static string BuildFaceIndex(int vertexIndex, int vertexOffset, int uvOffset, int normalOffset, bool hasUv, bool hasNormals)
        {
            int v = vertexOffset + vertexIndex;
            if (hasUv && hasNormals)
            {
                return $"{v}/{uvOffset + vertexIndex}/{normalOffset + vertexIndex}";
            }

            if (hasUv)
            {
                return $"{v}/{uvOffset + vertexIndex}";
            }

            if (hasNormals)
            {
                return $"{v}//{normalOffset + vertexIndex}";
            }

            return v.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildFaceIndexWithUvIndex(int vertexIndex, int vertexOffset, int uvIndex, int normalOffset, bool hasNormals)
        {
            int v = vertexOffset + vertexIndex;
            if (hasNormals)
            {
                return $"{v}/{uvIndex}/{normalOffset + vertexIndex}";
            }

            return $"{v}/{uvIndex}";
        }

        private static void DestroyTemporarySources(IList<FPMeshObjExportSource> sources)
        {
            if (sources == null)
            {
                return;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                FPMeshObjExportSource source = sources[i];
                if (source != null && source.DestroyMeshAfterExport && source.Mesh != null)
                {
                    Object.DestroyImmediate(source.Mesh);
                    source.Mesh = null;
                }
            }
        }

        private static bool PathsEqual(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetUniqueFilePath(string directory, string baseName, string extension)
        {
            string safeBaseName = SanitizeFileName(baseName);
            string path = Path.Combine(directory, safeBaseName + extension);
            int suffix = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(directory, safeBaseName + "_" + suffix + extension);
                suffix++;
            }

            return path;
        }

        private static string Float(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string SanitizeObjName(string value)
        {
            string safe = SanitizeFileName(value);
            return string.IsNullOrWhiteSpace(safe) ? "Mesh" : safe.Replace(' ', '_');
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "FP_MeshExport";
            }

            string safe = value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                safe = safe.Replace(invalid[i], '_');
            }

            return safe;
        }
    }

    public static class FPMeshExportFacade
    {
        public static bool ExportObj(
            IReadOnlyList<FPMeshExportSource> sources,
            string path,
            FPMeshExportOptions options,
            out string message)
        {
            List<FPMeshObjExportSource> objSources = ConvertSources(sources);
            return FPMeshObjExportUtility.ExportSources(objSources, path, ConvertOptions(options), out message);
        }

        public static bool ExportObjWithDialog(
            IReadOnlyList<FPMeshExportSource> sources,
            string defaultName,
            FPMeshExportOptions options)
        {
            List<FPMeshObjExportSource> objSources = ConvertSources(sources);
            return FPMeshObjExportUtility.ExportSourcesWithDialog(objSources, defaultName, ConvertOptions(options));
        }

        public static List<FPMeshExportSource> CollectGameObjectSources(GameObject root, FPMeshExportOptions options, Predicate<GameObject> isValidObject = null)
        {
            List<FPMeshObjExportSource> objSources = FPMeshObjExportUtility.CollectGameObjectSources(root, ConvertOptions(options), isValidObject);
            var publicSources = new List<FPMeshExportSource>(objSources.Count);
            for (int i = 0; i < objSources.Count; i++)
            {
                FPMeshObjExportSource source = objSources[i];
                publicSources.Add(new FPMeshExportSource(source.Name, source.Mesh, source.Matrix, source.Materials, null, source.DestroyMeshAfterExport, source.GroupName));
            }

            return publicSources;
        }

        private static List<FPMeshObjExportSource> ConvertSources(IReadOnlyList<FPMeshExportSource> sources)
        {
            var objSources = new List<FPMeshObjExportSource>();
            if (sources == null)
            {
                return objSources;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                FPMeshExportSource source = sources[i];
                if (source == null)
                {
                    continue;
                }

                objSources.Add(new FPMeshObjExportSource(source.Name, source.Mesh, source.Matrix, source.Materials, source.DestroyMeshAfterExport, source.GroupName));
            }

            return objSources;
        }

        private static FPMeshObjExportOptions ConvertOptions(FPMeshExportOptions options)
        {
            if (options == null)
            {
                return new FPMeshObjExportOptions();
            }

            return new FPMeshObjExportOptions
            {
                ExportMaterials = options.ExportMaterials,
                CopyTextures = options.CopyTextures,
                IncludeChildren = options.IncludeChildren,
                IncludeInactive = options.IncludeInactive,
                IncludeMeshFilters = options.IncludeMeshFilters,
                IncludeSkinnedMeshRenderers = options.IncludeSkinnedMeshRenderers,
                IncludeMeshColliders = options.IncludeMeshColliders,
                RootLocalSpace = options.RootLocalSpace,
                FlipNormals = options.FlipNormals,
                MirrorX = options.MirrorX,
                MaterialExportMode = options.MaterialExportMode,
                AtlasSize = options.AtlasSize,
                AtlasPadding = options.AtlasPadding,
                AtlasAlbedoPropertyFallbacks = options.AtlasAlbedoPropertyFallbacks,
                AtlasUvTransform = options.AtlasUvTransform
            };
        }
    }

    public static class FPMeshObjExport
    {
        public static bool ExportMeshWithDialog(Mesh mesh, string defaultName, Material material = null, bool destroyMeshAfterExport = false)
        {
            if (mesh == null)
            {
                EditorUtility.DisplayDialog("Export OBJ", "No mesh was available to export.", "OK");
                return false;
            }

            Material[] materials = material == null ? null : new[] { material };
            var sources = new List<FPMeshObjExportSource>
            {
                new FPMeshObjExportSource(mesh.name, mesh, Matrix4x4.identity, materials, destroyMeshAfterExport)
            };

            return FPMeshObjExportUtility.ExportSourcesWithDialog(
                sources,
                string.IsNullOrWhiteSpace(defaultName) ? mesh.name : defaultName,
                new FPMeshObjExportOptions
                {
                    ExportMaterials = true,
                    CopyTextures = true
                });
        }
    }
}
