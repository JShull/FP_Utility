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
