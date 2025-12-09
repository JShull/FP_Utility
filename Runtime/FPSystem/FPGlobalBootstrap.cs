namespace FuzzPhyte.Utility.FPSystem
{
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using System.Collections.Generic;
    /// <summary>
    /// Makes sure to do all of the bootstrapping for FuzzPhyte IFPAfterSceneLoadBootstrap.
    /// </summary>
    public static class FPGlobalBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            // This runs AFTER Awake/OnEnable, BEFORE Start on the first scene.

            // Grab all behaviours in the loaded scene(s)
            var monos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID);

            var list = new List<IFPAfterSceneLoadBootstrap>();

            foreach (var mb in monos)
            {
                if (mb is IFPAfterSceneLoadBootstrap bootstrap)
                {
                    list.Add(bootstrap);
                }
            }

            Debug.Log($"FPGlobalBootstrap: Found {list.Count} IFPAfterSceneLoadBootstrap implementors.");

            foreach (var bootstrap in list)
            {
                try
                {
                    bootstrap.InitializeAfterSceneLoad();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error in OnAfterSceneLoadBootstrap on {bootstrap}: {e}");
                }
            }
        }
    }
}
