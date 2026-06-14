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

    internal sealed class FPMeshObjExportSource
    {
        public string Name;
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public Material[] Materials;
        public bool DestroyMeshAfterExport;

        public FPMeshObjExportSource(string name, Mesh mesh, Matrix4x4 matrix, Material[] materials = null, bool destroyMeshAfterExport = false)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Mesh" : name;
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
    }

    internal static class FPMeshObjExportUtility
    {
        private const string DefaultMaterialName = "FP_Default";

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
                var objBuilder = new StringBuilder(8192);
                var mtlBuilder = new StringBuilder(2048);
                var materialNames = new Dictionary<Material, string>();
                var copiedTextures = new Dictionary<Texture, string>();

                objBuilder.AppendLine("# FuzzPhyte OBJ Export");
                if (options.ExportMaterials)
                {
                    objBuilder.Append("mtllib ").AppendLine(mtlFileName.Replace("\\", "/"));
                }

                int vertexOffset = 1;
                int uvOffset = 1;
                int normalOffset = 1;
                int exportedMeshes = 0;

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
                    Vector3[] normals = mesh.normals;
                    Matrix4x4 normalMatrix = source.Matrix.inverse.transpose;
                    string objectName = SanitizeObjName(source.Name);

                    objBuilder.Append("o ").AppendLine(objectName);

                    for (int v = 0; v < vertices.Length; v++)
                    {
                        Vector3 position = source.Matrix.MultiplyPoint3x4(vertices[v]);
                        objBuilder.Append("v ")
                            .Append(Float(position.x)).Append(' ')
                            .Append(Float(position.y)).Append(' ')
                            .Append(Float(position.z)).AppendLine();
                    }

                    if (uv != null && uv.Length == vertices.Length)
                    {
                        for (int u = 0; u < uv.Length; u++)
                        {
                            objBuilder.Append("vt ")
                                .Append(Float(uv[u].x)).Append(' ')
                                .Append(Float(uv[u].y)).AppendLine();
                        }
                    }

                    bool hasNormals = normals != null && normals.Length == vertices.Length;
                    if (hasNormals)
                    {
                        for (int n = 0; n < normals.Length; n++)
                        {
                            Vector3 normal = normalMatrix.MultiplyVector(normals[n]).normalized;
                            objBuilder.Append("vn ")
                                .Append(Float(normal.x)).Append(' ')
                                .Append(Float(normal.y)).Append(' ')
                                .Append(Float(normal.z)).AppendLine();
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
                        string materialName = GetMaterialName(material, materialNames);
                        if (options.ExportMaterials)
                        {
                            objBuilder.Append("usemtl ").AppendLine(materialName);
                            EnsureMaterialWritten(material, materialName, mtlBuilder, directory, options, copiedTextures);
                        }

                        int[] indices = mesh.GetIndices(subMesh);
                        int step = topology == MeshTopology.Quads ? 4 : 3;
                        for (int index = 0; index + step - 1 < indices.Length; index += step)
                        {
                            objBuilder.Append('f');
                            for (int corner = 0; corner < step; corner++)
                            {
                                int vertexIndex = indices[index + corner];
                                objBuilder.Append(' ').Append(BuildFaceIndex(vertexIndex, vertexOffset, uvOffset, normalOffset, uv != null && uv.Length == vertices.Length, hasNormals));
                            }

                            objBuilder.AppendLine();
                        }
                    }

                    vertexOffset += vertices.Length;
                    uvOffset += uv != null && uv.Length == vertices.Length ? uv.Length : 0;
                    normalOffset += hasNormals ? normals.Length : 0;
                    exportedMeshes++;
                }

                if (exportedMeshes == 0)
                {
                    message = "No readable mesh data was exported.";
                    return false;
                }

                File.WriteAllText(objPath, objBuilder.ToString(), Encoding.UTF8);
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

        public static List<FPMeshObjExportSource> CollectGameObjectSources(GameObject root, FPMeshObjExportOptions options, Predicate<GameObject> isValidObject = null)
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

            Matrix4x4 rootToLocal = options.RootLocalSpace ? root.transform.worldToLocalMatrix : Matrix4x4.identity;
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
                    result.Add(new FPMeshObjExportSource(filter.gameObject.name, filter.sharedMesh, rootToLocal * filter.transform.localToWorldMatrix, renderer == null ? null : renderer.sharedMaterials));
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
                    result.Add(new FPMeshObjExportSource(renderer.gameObject.name, bakedMesh, rootToLocal * renderer.transform.localToWorldMatrix, renderer.sharedMaterials, true));
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

                    result.Add(new FPMeshObjExportSource(collider.gameObject.name + "_Collider", collider.sharedMesh, rootToLocal * collider.transform.localToWorldMatrix));
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

            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
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
                return false;
            }

            string sourcePath = Path.GetFullPath(assetPath);
            if (!File.Exists(sourcePath))
            {
                return false;
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
