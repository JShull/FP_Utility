// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

using System;
using UnityEngine;

namespace FuzzPhyte.Utility.Meta
{
    [Serializable]
    [CreateAssetMenu(fileName = "Tag", menuName = "FuzzPhyte/Utility/Meta/Tag", order = 0)]
    public class FP_Tag : FP_Data
    {
        public string TagName;
        [TextArea(2,4)]
        public string Description;
    }
}
