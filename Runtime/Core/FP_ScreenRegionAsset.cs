// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

using UnityEngine;

namespace FuzzPhyte.Utility
{
    [CreateAssetMenu(fileName = "FP_ScreenRegionAsset", menuName = "FuzzPhyte/Utility/Design/DrawScreenRegionAsset")]
    public class FP_ScreenRegionAsset : FP_Data
    {
        public FP_ScreenRegion[] Region;
    }
}
