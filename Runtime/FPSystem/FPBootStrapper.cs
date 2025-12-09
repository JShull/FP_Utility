namespace FuzzPhyte.Utility.FPSystem
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class FPBootStrapper<TData> : MonoBehaviour, IFPAfterSceneLoadBootstrap, IFPDontDestroy where TData:FP_Data
    {
        public static FPBootStrapper<TData> Instance { get; private set; }
        //bool IFPDontDestroy.DontDestroy { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public bool DontDestroy { get => dontDestroy; set=>dontDestroy = value; }
        [SerializeField]protected bool dontDestroy;
        [TextArea(3, 4)]
        public string Instructions = $"An editor script - FPExecutionOrder.cs - will set the execution order of this script to -50. This script will run all FPSystems in the scene. If you want to run a system after the late update loop, set the bool to true in the inspector. If you want to run all systems in the scene, set the bool to true in the inspector. If you want to run a specific list of systems, add them to the list in the inspector.";
        public bool RunAfterLateUpdateLoop;
        [Header("Bootstrapper Data Processing Settings")]
        [Tooltip("If you want to process some sort of derived FP_Data")]
        public bool ProcessSystemDataOnInit;
        [Tooltip("If you want the bootstrapper to initialize the system data, set this to true and set the InitSystemData to the data you want to initialize.")]
        public TData InitSystemData;
        /*
        #if !UNITY_WEBGL
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        
        public static void InitializeAfterAwake()
        {
            Debug.LogWarning($"Running bootStrapper! {Time.time} and {Time.frameCount}");
            
            //var MajorFPSystems = Object.FindObjectsOfType<FPSystemBase<TData>>().ToList();
            var MajorFPSystems=Object.FindObjectsByType<FPSystemBase<TData>>(FindObjectsSortMode.InstanceID).ToList();
            Debug.LogWarning($"Major Systems Found: {MajorFPSystems.Count}");
            foreach (var initializer in MajorFPSystems)
            {
                if (Instance.ProcessSystemDataOnInit)
                {
                    initializer.Initialize(initializer.AfterLateUpdateActive,Instance.InitSystemData);
                }
                else
                {
                    initializer.Initialize(initializer.AfterLateUpdateActive);
                }
            }
        }
        #endif
        */
        public virtual void Awake()
        {
            // Ensure only one instance exists
            if (Instance == null)
            {
                Instance = this;
                Debug.LogWarning($"FPBootStrapper instance created on {this.gameObject.name}.");
                if (dontDestroy)
                {
                    DontDestroyOnLoad(this.gameObject);
                }
            }
            else
            {
                Destroy(this.gameObject);
                Debug.LogWarning($"Destroying duplicate FPBootStrapper instance on {this.gameObject.name}.");
                return;
            }
        }
        public virtual void InitializeAfterSceneLoad()
        {
            Debug.LogWarning($"Running FPBootStrapper<{typeof(TData).Name}>! {Time.time} and {Time.frameCount}");
            // you should consider adding the attribute above this function -->  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            // If you care about WEBGL, keep the old #if – purely optional here.
#if !UNITY_WEBGL
            var majorFPSystems =
                Object.FindObjectsByType<FPSystemBase<TData>>(FindObjectsSortMode.InstanceID).ToList();

            Debug.LogWarning($"Major Systems Found: {majorFPSystems.Count}");

            foreach (var initializer in majorFPSystems)
            {
                if (ProcessSystemDataOnInit)
                {
                    initializer.Initialize(initializer.AfterLateUpdateActive, InitSystemData);
                }
                else
                {
                    initializer.Initialize(initializer.AfterLateUpdateActive);
                }
            }
#endif
        }
    }
}
