using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FuzzPhyte.Utility.FPSystem
{
    /// <summary>
    /// This works well with the FPBootStrapper.cs script in your scene
    /// </summary>
    //public class FPManager<TData> : IFPSingleton<TData> where TData : FP_Data
    public abstract class FPSystemBase<TData> : MonoBehaviour, IFPSingleton<TData> where TData:FP_Data
    {
        /// <summary>
        /// so we don't have to keep declaring a return new call on this
        /// </summary>
        public WaitForEndOfFrame EndOfFrame;
        protected bool AfterLateUpdateActive=false;
        public static FPSystemBase<TData> Instance { get; protected set; }
        [Tooltip("Maybe some starter data for this system")]
        [SerializeField]
        protected TData systemData;
        /// <summary>
        /// we might have some data we want to pass in on and it's probably based on our lowest base data class
        /// but it's not required
        /// </summary>
        /// <param name="data"></param>
        public virtual void Initialize(bool runAfterLateUpdateLoop,TData data = null)
        {
            AfterLateUpdateActive = runAfterLateUpdateLoop;
            if(data!=null)
            {
                systemData = data;
            }
        }
        
        public virtual void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
            else
            {
                Destroy(this.gameObject);
                Debug.LogWarning($"Destroying {this.gameObject.name} as there is already an instance of {this.GetType().Name} in the scene.");
            }
        }

        public virtual void Start()
        {
            if(AfterLateUpdateActive)
            {
                StartCoroutine(RunAfterLateUpdate());
            }
            
        }
        public virtual void OnDestroy()
        {
            //clean up our instance and singleton
            
        }
        /// <summary>
        /// Loop code to run after the EndOfFrame delay
        /// </summary>
        /// <returns></returns>
        public IEnumerator RunAfterLateUpdate()
        {
            while(AfterLateUpdateActive)
            {
                yield return EndOfFrame;
                AfterLateUpdate();
            }
            
        }
        /// <summary>
        /// override this function to run some code after the late update loop
        /// </summary>
        public virtual void AfterLateUpdate()
        {
            
        }
    }
}
