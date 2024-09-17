namespace FuzzPhyte.Utility.FPSystem
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class FPBootStrapper<TData> : MonoBehaviour where TData:FP_Data
    {
        public static FPBootStrapper<TData> Instance { get; private set; }
        [TextArea(3, 4)]
        public string Instructions = $"An editor script - FPExecutionOrder.cs - will set the execution order of this script to -50. This script will run all FPSystems in the scene. If you want to run a system after the late update loop, set the bool to true in the inspector. If you want to run all systems in the scene, set the bool to true in the inspector. If you want to run a specific list of systems, add them to the list in the inspector.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeAfterAwake()
        {
            Debug.LogWarning($"Running bootStrapper! {Time.time} and {Time.frameCount}");
            var MajorFPSystems = Object.FindObjectsOfType<FPSystemBase<TData>>().ToList();
            Debug.LogWarning($"Major Systems Found: {MajorFPSystems.Count}");
            foreach (var initializer in MajorFPSystems)
            {
                initializer.Initialize(true);
            }
        }
        protected virtual void Awake()
        {
            // Ensure only one instance exists
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
                Debug.LogWarning($"Destroying duplicate FPBootStrapper instance on {this.gameObject.name}.");
                return;
            }
        }
    }
}
