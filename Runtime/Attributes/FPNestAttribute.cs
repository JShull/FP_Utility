// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Attributes
{
    using System;
    [AttributeUsage(AttributeTargets.Field,AllowMultiple=false,Inherited=true)]
    public class FPNestAttribute:FPAttribute
    {
    }
}
