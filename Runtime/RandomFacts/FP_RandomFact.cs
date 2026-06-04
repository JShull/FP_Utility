// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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
