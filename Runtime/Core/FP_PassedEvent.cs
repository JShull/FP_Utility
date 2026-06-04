// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

using UnityEngine;
using UnityEngine.Events;

namespace FuzzPhyte.Utility
{
    public class FP_PassedEvent : MonoBehaviour
    {
        public UnityEvent ThePassedEvent;

        public void PassMyEvent()
        {
            ThePassedEvent.Invoke();
        }
    }
}
