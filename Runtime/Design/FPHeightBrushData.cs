namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(fileName = "FPHeightBrushData", menuName = "FuzzPhyte/Utility/Design/Height Brush Data", order = 15)]
    [Serializable]
    public class FPHeightBrushData : FP_Data
    {
        public bool EnableBrushEditing = true;
        public Texture2D BrushMask;
        public int BrushMode = 0;
        public int BrushSizePixels = 24;
        public float BrushRotationDegrees = 0f;
        public float BrushSoftness = 0.5f;
        public float BrushStrength = 0.15f;
        public float BrushSetValue = 1f;

        public void Capture(
            bool enableBrushEditing,
            Texture2D brushMask,
            int brushMode,
            int brushSizePixels,
            float brushRotationDegrees,
            float brushSoftness,
            float brushStrength,
            float brushSetValue)
        {
            EnableBrushEditing = enableBrushEditing;
            BrushMask = brushMask;
            BrushMode = brushMode;
            BrushSizePixels = Mathf.Max(1, brushSizePixels);
            BrushRotationDegrees = Mathf.Repeat(brushRotationDegrees, 360f);
            BrushSoftness = Mathf.Clamp01(brushSoftness);
            BrushStrength = Mathf.Clamp01(brushStrength);
            BrushSetValue = Mathf.Clamp01(brushSetValue);

            if (string.IsNullOrWhiteSpace(UniqueID))
            {
                UniqueID = Guid.NewGuid().ToString();
            }
        }

        private void OnValidate()
        {
            BrushSizePixels = Mathf.Max(1, BrushSizePixels);
            BrushRotationDegrees = Mathf.Repeat(BrushRotationDegrees, 360f);
            BrushSoftness = Mathf.Clamp01(BrushSoftness);
            BrushStrength = Mathf.Clamp01(BrushStrength);
            BrushSetValue = Mathf.Clamp01(BrushSetValue);
        }
    }
}
