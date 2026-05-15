namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;

    [Serializable]
    public class FPSVGExtruderSettings
    {
        public TextAsset SvgFile;
        public float Scale = 0.01f;
        public float ExtrusionDepth = 0.1f;
        public float PathSampleDistance = 2f;
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
                PathSampleDistance = Mathf.Max(0.01f, PathSampleDistance),
                CenterPivot = CenterPivot,
                GenerateDoubleSided = GenerateDoubleSided,
                RecalculateNormals = RecalculateNormals,
                OutputMeshName = string.IsNullOrWhiteSpace(OutputMeshName) ? "GeneratedSVGMesh" : OutputMeshName.Trim(),
                OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? "Assets/_FPUtility" : OutputFolder.Trim()
            };
        }
    }
}
