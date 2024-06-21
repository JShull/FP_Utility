using System.Collections;
using UnityEngine;

namespace FuzzPhyte.Utility.FPSystem
{
    public interface IFPSingleton 
    {
        
        /// <summary>
        /// this should be called via Awake
        /// </summary>
        public void Initialize(bool runAfterLateUpdateLoop, FP_Data data = null);

        public IEnumerator RunAfterLateUpdate();
        public void AfterLateUpdate();
    }
}
