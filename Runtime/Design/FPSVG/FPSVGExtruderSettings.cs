namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;

    public enum FPSVGTriangulationBackend
    {
        CustomEarClipping = 0,
        UnityVectorGraphics = 1
    }

    [Serializable]
    public class FPSVGExtruderSettings
    {
        public TextAsset SvgFile;
        public float Scale = 0.01f;
        public float ExtrusionDepth = 0.1f;
        public FPSVGTriangulationBackend TriangulationBackend = FPSVGTriangulationBackend.CustomEarClipping;
        public float PathSampleDistance = 2f;
        public float BoundarySimplifyTolerance = 0.001f;
        public float CollinearTolerance = 0.0001f;
        public bool OptimizeSurfaceTriangulation = true;
        public int SurfaceOptimizationPasses = 8;
        public bool UseZOrderEarSearch;
        public bool CenterPivot = true;
        public bool GenerateDoubleSided;
        public bool RecalculateNormals;
        public string OutputMeshName = "GeneratedSVGMesh";
        public string OutputFolder = "Assets/_FPUtility";

        public static FPSVGExtruderSettings Default => new FPSVGExtruderSettings();

        public FPSVGExtruderSettings Sanitized()
        {
            return new FPSVGExtruderSettings
            {
                SvgFile = SvgFile,
                Scale = Mathf.Max(0.0001f, Scale),
                ExtrusionDepth = Mathf.Max(0.0001f, ExtrusionDepth),
                TriangulationBackend = TriangulationBackend,
                PathSampleDistance = Mathf.Max(0.01f, PathSampleDistance),
                BoundarySimplifyTolerance = Mathf.Max(0f, BoundarySimplifyTolerance),
                CollinearTolerance = Mathf.Max(0f, CollinearTolerance),
                OptimizeSurfaceTriangulation = OptimizeSurfaceTriangulation,
                SurfaceOptimizationPasses = Mathf.Clamp(SurfaceOptimizationPasses, 0, 32),
                UseZOrderEarSearch = UseZOrderEarSearch,
                CenterPivot = CenterPivot,
                GenerateDoubleSided = GenerateDoubleSided,
                RecalculateNormals = RecalculateNormals,
                OutputMeshName = string.IsNullOrWhiteSpace(OutputMeshName) ? "GeneratedSVGMesh" : OutputMeshName.Trim(),
                OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? "Assets/_FPUtility" : OutputFolder.Trim()
            };
        }
    }
}
