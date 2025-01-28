namespace FuzzPhyte.Utility
{
    using System;

    public class HelperData : IComparable<HelperData>
    {
        public HelperCategory Category { get; set; }
        public float ActivationTime { get; set; }  // Time when the helper should activate
        public Action onActivate { get; set; }      // Action to execute when activated
        public int CompareTo(HelperData other)
        {
            return ActivationTime.CompareTo(other.ActivationTime);
        }
    }
}
