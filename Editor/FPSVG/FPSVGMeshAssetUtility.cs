// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public static class FPSVGMeshAssetUtility
    {
        public static Mesh SaveMeshAsset(Mesh mesh, string outputFolder, string meshName, out string message)
        {
            message = string.Empty;
            if (mesh == null)
            {
                message = "No mesh was generated to save.";
                return null;
            }

            string safeFolder = string.IsNullOrWhiteSpace(outputFolder) ? "Assets/_FPUtility" : outputFolder.Trim();
            string safeName = string.IsNullOrWhiteSpace(meshName) ? "GeneratedSVGMesh" : meshName.Trim();
            safeFolder = safeFolder.Replace("\\", "/");
            if (!safeFolder.StartsWith("Assets"))
            {
                safeFolder = "Assets/" + safeFolder.TrimStart('/');
            }

            EnsureAssetFolder(safeFolder);

            mesh.name = safeName;
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(safeFolder, safeName + ".asset").Replace("\\", "/"));
            try
            {
                AssetDatabase.CreateAsset(mesh, path);
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                message = $"Mesh asset could not be saved: {ex.Message}";
                return null;
            }

            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (savedMesh == null)
            {
                message = $"Mesh asset could not be loaded after save: {path}";
                return null;
            }

            message = $"Mesh saved to {path}";
            return savedMesh;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
