using System.Collections;
using UnityEngine;

namespace FuzzPhyte.Utility.FPSystem
{
    public interface IFPSingleton<TData> where TData : FP_Data 
    {
        
        /// <summary>
        /// this should be called via Awake
        /// </summary>
        public void Initialize(bool runAfterLateUpdateLoop, TData data = null);

        public IEnumerator RunAfterLateUpdate();
        public void AfterLateUpdate();
    }
}
