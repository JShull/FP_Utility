// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using FuzzPhyte.Utility.Meta;
    using UnityEngine;

    [SelectionBase]
    public class FP_SelectionBase : MonoBehaviour
    {
        public FP_Tag MainFPTag;
        public FP_Tag HelperTag;
    }
}
