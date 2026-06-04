// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Core class for all things tied to Data and Scriptable Objects
    /// </summary>
    [Serializable]
    public abstract class FP_Data : ScriptableObject
    {
        [Tooltip("Must be UNIQUE")]
        public string UniqueID;

        public string Initialize(string projectName, string itemName, Color color)
        {
            if (string.IsNullOrEmpty(UniqueID))
            {
                UniqueID = System.Guid.NewGuid().ToString();
            }
            UniqueID = FP_UniqueGenerator.Encode(projectName, itemName, color);
            return UniqueID;
        }
    }
}
