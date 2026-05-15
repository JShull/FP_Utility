namespace FuzzPhyte.Utility.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public static class FPSVGMeshAssetUtility
    {
        public static Mesh SaveMeshAsset(Mesh mesh, string outputFolder, string meshName)
        {
            if (mesh == null)
            {
                Debug.LogError("[FP SVG Extruder] No mesh was generated to save.");
                return null;
            }

            string safeFolder = string.IsNullOrWhiteSpace(outputFolder) ? "Assets/GeneratedMeshes" : outputFolder.Trim();
            string safeName = string.IsNullOrWhiteSpace(meshName) ? "Generated_SVG_Mesh" : meshName.Trim();
            safeFolder = safeFolder.Replace("\\", "/");
            if (!safeFolder.StartsWith("Assets"))
            {
                safeFolder = "Assets/" + safeFolder.TrimStart('/');
            }

            EnsureAssetFolder(safeFolder);

            mesh.name = safeName;
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(safeFolder, safeName + ".asset").Replace("\\", "/"));
            string result = FP_Utility_Editor.CreateAssetAt(mesh, path);
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (savedMesh == null)
            {
                Debug.LogError($"[FP SVG Extruder] Mesh asset could not be saved: {result}");
                return null;
            }

            Debug.Log($"[FP SVG Extruder] Mesh saved to {result}");
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
