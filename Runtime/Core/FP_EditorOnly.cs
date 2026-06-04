// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using UnityEngine;
    /// <summary>
    /// This is a tag, our BuildProcessor will remove this GameObject
    /// </summary>
    public class FP_EditorOnly : MonoBehaviour
    {
        [TextArea(3, 10)]
        public string Notes;
    }
}
