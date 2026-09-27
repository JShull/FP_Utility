// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor
{
    // Unity's inline Material Enum drawer has only seven name/value pairs.
    // A named enum keeps all eight choices available without shader keywords.
    public enum FPDitherPattern
    {
        Bayer = 0,
        Squares = 1,
        Dots = 2,
        Lines = 3,
        Plus = 4,
        Diamond = 5,
        Star = 6,
        CustomStamp = 7
    }
}
