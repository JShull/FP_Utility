// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// Controls how an outline target contributes pixels to the outline mask.
    /// </summary>
    public enum FPOutlineAlphaMode
    {
        MeshSilhouette = 0,
        MainTextureAlpha = 1,
        CustomMaskTexture = 2
    }
}
