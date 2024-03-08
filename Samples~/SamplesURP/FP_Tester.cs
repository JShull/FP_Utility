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
