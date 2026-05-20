namespace FuzzPhyte.Utility.Editor.DebugTools
{
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public static class FPAssetReferenceTracer
    {
        [MenuItem("FuzzPhyte/Utility/Diagnostics/Trace Selected Asset References", priority = FP_UtilityData.MENU_UTILITY_DIAGNOSTICS)]
        public static void TraceSelectedAssetReferences()
        {
            Object selected = Selection.activeObject;

            if (selected == null)
            {
                Debug.LogWarning("[FP_TRACE] No asset selected.");
                return;
            }

            string targetPath = AssetDatabase.GetAssetPath(selected);

            if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogWarning($"[FP_TRACE] Selected object has no asset path: {selected.name}");
                return;
            }

            Debug.Log($"[FP_TRACE] Target: {selected.name}");
            Debug.Log($"[FP_TRACE] Path: {targetPath}");

            TraceBuildScenes(targetPath);
            TraceAssets(targetPath);
        }

        private static void TraceBuildScenes(string targetPath)
        {
            Debug.Log("[FP_TRACE] Checking enabled build scenes...");

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                string[] dependencies = AssetDatabase.GetDependencies(scene.path, true);

                if (dependencies.Contains(targetPath))
                {
                    Debug.Log($"[FP_TRACE][SCENE] Referenced by enabled scene: {scene.path}");
                }
            }
        }

        private static void TraceAssets(string targetPath)
        {
            Debug.Log("[FP_TRACE] Checking Assets and Packages...");

            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path =>
                    path.StartsWith("Assets/") ||
                    path.StartsWith("Packages/"))
                .ToArray();

            int hitCount = 0;

            foreach (string path in allAssetPaths)
            {
                if (path == targetPath)
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(path, true);

                if (dependencies.Contains(targetPath))
                {
                    hitCount++;
                    Debug.Log($"[FP_TRACE][ASSET] Referenced by: {path}");
                }
            }

            Debug.Log($"[FP_TRACE] Finished. Found {hitCount} referencing assets.");
        }
    }
}
