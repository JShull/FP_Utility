namespace FuzzPhyte.Utility.Animation
{
    using UnityEngine;
    using UnityEngine.Playables;
    using UnityEngine.Timeline;
    [TrackBindingType(typeof(FPSplineFollower))]
    [TrackClipType(typeof(FPSplineFollowClip))]
    public class FPSplineFollowTrack : TrackAsset { }
}
