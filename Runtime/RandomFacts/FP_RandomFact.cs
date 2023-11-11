using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FuzzPhyte.Utility.Notification
{
    [Serializable]
    [CreateAssetMenu(fileName = "Random Fact", menuName = "FuzzPhyte/Utility/Notification/RandomFact", order = 5)]
    public class FP_RandomFact : FP_Notification
    {
        [TextArea(3, 8)]
        public string RandomFactDescription;
        [Tooltip("If we want this to be part of the top left running list of tasks")]
        public OverlayType OverlayTaskType;
    }
    
}
