namespace FuzzPhyte.Utility.Video
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Authoring settings for generating an equirectangular-ready sphere mesh.
    /// </summary>
    [Serializable]
    public struct FPVideoSphereBuildSettings
    {
        [SerializeField] private string meshName;
        [SerializeField] private float radius;
        [SerializeField] private int longitudeSegments;
        [SerializeField] private int latitudeSegments;
        [SerializeField] private bool generateInsideOut;

        public string MeshName
        {
            get => meshName;
            set => meshName = value;
        }

        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        public int LongitudeSegments
        {
            get => longitudeSegments;
            set => longitudeSegments = value;
        }

        public int LatitudeSegments
        {
            get => latitudeSegments;
            set => latitudeSegments = value;
        }

        public bool GenerateInsideOut
        {
            get => generateInsideOut;
            set => generateInsideOut = value;
        }

        public FPVideoSphereBuildSettings Sanitized()
        {
            FPVideoSphereBuildSettings sanitized = this;
            sanitized.meshName = string.IsNullOrWhiteSpace(meshName) ? "FP_VideoSphere" : meshName.Trim();
            sanitized.radius = Mathf.Max(0.001f, radius);
            sanitized.longitudeSegments = Mathf.Clamp(longitudeSegments, 3, 512);
            sanitized.latitudeSegments = Mathf.Clamp(latitudeSegments, 2, 256);
            return sanitized;
        }

        public int VertexCount
        {
            get
            {
                FPVideoSphereBuildSettings sanitized = Sanitized();
                return (sanitized.longitudeSegments + 1) * (sanitized.latitudeSegments + 1);
            }
        }

        public int TriangleCount
        {
            get
            {
                FPVideoSphereBuildSettings sanitized = Sanitized();
                return sanitized.longitudeSegments * Mathf.Max(1, (sanitized.latitudeSegments - 1)) * 2;
            }
        }

        public static FPVideoSphereBuildSettings Default => new FPVideoSphereBuildSettings
        {
            meshName = "FP_VideoSphere",
            radius = 10f,
            longitudeSegments = 64,
            latitudeSegments = 32,
            generateInsideOut = true
        };
    }
}
