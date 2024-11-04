#if UNITY_EDITOR
namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public class FPBuildProcessor
    {
        static FPBuildProcessor()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildHandler);
        }
        private static void BuildHandler(BuildPlayerOptions options)
        {
            // Find all GameObjects with the EditorOnlyComponent in the scene
            FP_EditorOnly[] editorOnlyComponents = Object.FindObjectsByType<FP_EditorOnly>(FindObjectsSortMode.InstanceID);
            foreach (FP_EditorOnly component in editorOnlyComponents)
            {
                // Remove the GameObject from the scene
                Object.DestroyImmediate(component.gameObject);
            }
            // Proceed with the build
            BuildPipeline.BuildPlayer(options);
        }
    }
}
#endif