namespace FuzzPhyte.Utility.Animation.Editor
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Playables;
    using System.Linq;

    [InitializeOnLoad]
    public static class FPTimelineAutoRefresh
    {
        static FPTimelineAutoRefresh()
        {
            // After scripts reload or at editor idle, rebuild graphs
            EditorApplication.delayCall += RefreshOpenSceneTimelines;
            AssemblyReloadEvents.afterAssemblyReload += RefreshOpenSceneTimelines;

            // When returning to Edit Mode, rebuild
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    RefreshOpenSceneTimelines();
                }
            };
        }
        static void RefreshOpenSceneTimelines()
        {
            // Find all directors in loaded scenes (incl. disabled)
            //var directors = Object.FindObjectsOfType<PlayableDirector>(true);
            var directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
            foreach (var dir in directors)
            {
                if (!dir || dir.playableAsset == null) continue;

                // Re-import the asset to ensure types are loaded (safe & cheap)
                var path = AssetDatabase.GetAssetPath(dir.playableAsset);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                dir.RebuildGraph();  // rebuild playable graph
                dir.Evaluate();      // display current frame in the editor
            }
        }
    }
#endif
}


