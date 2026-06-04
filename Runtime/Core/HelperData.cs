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
    using System.Collections.Generic;
    public class HelperData : IComparable<HelperData>
    {
        public List<HelperCategory> Category { get; set; }
        public List<HelperAction> HelperAction { get; set; }
        public float ActivationTime { get; set; }  // Time when the helper should activate
        public List<Action> onActivate { get; set; }      // Action to execute when activated
        public int CompareTo(HelperData other)
        {
            return ActivationTime.CompareTo(other.ActivationTime);
        }
    }
}
