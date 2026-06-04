// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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
