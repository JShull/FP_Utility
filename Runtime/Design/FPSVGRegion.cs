namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public class FPSVGRegion
    {
        public string Id;
        public List<Vector2> OuterLoop = new List<Vector2>();
        public List<List<Vector2>> Holes = new List<List<Vector2>>();
        public bool Included;

        public FPSVGRegion()
        {
        }

        public FPSVGRegion(string id, List<Vector2> outerLoop)
        {
            Id = id;
            OuterLoop = outerLoop ?? new List<Vector2>();
        }

        public float AbsArea => Mathf.Abs(FPSVGRegionDetector.SignedArea(OuterLoop));

        public FPSVGRegion CloneWithoutHoles()
        {
            return new FPSVGRegion
            {
                Id = Id,
                OuterLoop = new List<Vector2>(OuterLoop),
                Holes = new List<List<Vector2>>(),
                Included = Included
            };
        }
    }

    public class FPSVGParseResult
    {
        public readonly List<FPSVGRegion> Regions = new List<FPSVGRegion>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public Rect Bounds;

        public bool Success => Errors.Count == 0 && Regions.Count > 0;
    }

    public class FPSVGMeshBuildReport
    {
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool Success => Errors.Count == 0;
    }
}
