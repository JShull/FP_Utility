namespace FuzzPhyte.Utility.Animation
{
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;

    [System.Serializable]
    public enum FPSplineCommand
    {
        Pause,
        Resume,
        Stop,
        Unstop,
        SetSpeedMultiplier,
        WarpToT,        // immediate jump (teleport)
        SetT            // sets current t respecting pause/stop gates
    }
    /// <summary>
    /// Setting up the marker for our spline follower timeline portion
    /// </summary>
    [System.Serializable]
    public class FPSplineCommandMarker : Marker,INotification
    {
        public FPSplineCommand command = FPSplineCommand.Pause;
        [Tooltip("Optional float payload (e.g., speed multiplier or t).")]
        public float value = 1f;

        // Required by INotification; can be any object identity
        public PropertyName id => new PropertyName(nameof(FPSplineCommandMarker));
    }
}
