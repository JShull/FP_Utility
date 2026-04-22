namespace FuzzPhyte.Utility.Video
{
    using System;
    using UnityEngine;

    [Serializable]
    public struct FPVideoQuadBuildSettings
    {
        [SerializeField] private string meshName;
        [SerializeField] private float width;
        [SerializeField] private float height;
        [SerializeField] private int widthSegments;
        [SerializeField] private int heightSegments;
        [SerializeField] private bool flipFacing;

        public string MeshName
        {
            get => meshName;
            set => meshName = value;
        }

        public float Width
        {
            get => width;
            set => width = value;
        }

        public float Height
        {
            get => height;
            set => height = value;
        }

        public int WidthSegments
        {
            get => widthSegments;
            set => widthSegments = value;
        }

        public int HeightSegments
        {
            get => heightSegments;
            set => heightSegments = value;
        }

        public bool FlipFacing
        {
            get => flipFacing;
            set => flipFacing = value;
        }

        public FPVideoQuadBuildSettings Sanitized()
        {
            FPVideoQuadBuildSettings sanitized = this;
            sanitized.meshName = string.IsNullOrWhiteSpace(meshName) ? "FP_VideoQuad" : meshName.Trim();
            sanitized.width = Mathf.Max(0.001f, width);
            sanitized.height = Mathf.Max(0.001f, height);
            sanitized.widthSegments = Mathf.Clamp(widthSegments, 1, 512);
            sanitized.heightSegments = Mathf.Clamp(heightSegments, 1, 512);
            return sanitized;
        }

        public int VertexCount
        {
            get
            {
                FPVideoQuadBuildSettings sanitized = Sanitized();
                return (sanitized.widthSegments + 1) * (sanitized.heightSegments + 1);
            }
        }

        public int TriangleCount
        {
            get
            {
                FPVideoQuadBuildSettings sanitized = Sanitized();
                return sanitized.widthSegments * sanitized.heightSegments * 2;
            }
        }

        public static FPVideoQuadBuildSettings Default => new FPVideoQuadBuildSettings
        {
            meshName = "FP_VideoQuad",
            width = 10f,
            height = 5.625f,
            widthSegments = 1,
            heightSegments = 1,
            flipFacing = false
        };
    }
}
