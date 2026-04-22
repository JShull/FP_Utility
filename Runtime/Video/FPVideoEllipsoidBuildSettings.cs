namespace FuzzPhyte.Utility.Video
{
    using System;
    using UnityEngine;

    [Serializable]
    public struct FPVideoEllipsoidBuildSettings
    {
        [SerializeField] private string meshName;
        [SerializeField] private Vector3 radii;
        [SerializeField] private int longitudeSegments;
        [SerializeField] private int latitudeSegments;
        [SerializeField] private bool generateInsideOut;

        public string MeshName
        {
            get => meshName;
            set => meshName = value;
        }

        public Vector3 Radii
        {
            get => radii;
            set => radii = value;
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

        public FPVideoEllipsoidBuildSettings Sanitized()
        {
            FPVideoEllipsoidBuildSettings sanitized = this;
            sanitized.meshName = string.IsNullOrWhiteSpace(meshName) ? "FP_VideoEllipsoid" : meshName.Trim();
            sanitized.radii.x = Mathf.Max(0.001f, radii.x);
            sanitized.radii.y = Mathf.Max(0.001f, radii.y);
            sanitized.radii.z = Mathf.Max(0.001f, radii.z);
            sanitized.longitudeSegments = Mathf.Clamp(longitudeSegments, 3, 512);
            sanitized.latitudeSegments = Mathf.Clamp(latitudeSegments, 2, 256);
            return sanitized;
        }

        public int VertexCount
        {
            get
            {
                FPVideoEllipsoidBuildSettings sanitized = Sanitized();
                return (sanitized.longitudeSegments + 1) * (sanitized.latitudeSegments + 1);
            }
        }

        public int TriangleCount
        {
            get
            {
                FPVideoEllipsoidBuildSettings sanitized = Sanitized();
                return sanitized.longitudeSegments * Mathf.Max(1, (sanitized.latitudeSegments - 1)) * 2;
            }
        }

        public static FPVideoEllipsoidBuildSettings Default => new FPVideoEllipsoidBuildSettings
        {
            meshName = "FP_VideoEllipsoid",
            radii = new Vector3(10f, 8f, 10f),
            longitudeSegments = 64,
            latitudeSegments = 32,
            generateInsideOut = true
        };
    }
}
