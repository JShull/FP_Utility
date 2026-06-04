// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FuzzPhyte.Utility.Samples
{
    /// <summary>
    /// class used for misc. testing items to demonstrate things
    /// generally used with the FP_Timer configuration
    /// </summary>
    public class FP_Tester : MonoBehaviour
    {
        public UnityEvent OnStartDelay;
        public float Delay = 1f;

        public void Start()
        {
            AudioActivation();
        }


        public void AudioActivation()
        {
            FP_Timer.CCTimer.StartTimer(Delay, () => { OnStartDelay.Invoke(); });
        }
    }
}
