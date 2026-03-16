namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;

    [CreateAssetMenu(fileName = "FPMeshGridData", menuName = "FuzzPhyte/Utility/Design/Mesh Grid Data", order = 14)]
    [Serializable]
    public class FPMeshGridData : FP_Data
    {
        public static event Action<FPMeshGridData> Changed;

        public FPMeshGridBuildSettings GridSettings = FPMeshGridBuildSettings.Default;
        public FPMeshHeightmapSettings HeightmapSettings = FPMeshHeightmapSettings.Default;
        public FPMeshHeightProcessSettings HeightProcessSettings = FPMeshHeightProcessSettings.Default;

        public void Capture(
            FPMeshGridBuildSettings gridSettings,
            FPMeshHeightmapSettings heightmapSettings,
            FPMeshHeightProcessSettings heightProcessSettings)
        {
            GridSettings = gridSettings.Sanitized();
            HeightmapSettings = heightmapSettings.Sanitized();
            HeightProcessSettings = heightProcessSettings.Sanitized();

            if (string.IsNullOrWhiteSpace(UniqueID))
            {
                UniqueID = Guid.NewGuid().ToString();
            }

            NotifyChanged();
        }

        private void OnValidate()
        {
            GridSettings = GridSettings.Sanitized();
            HeightmapSettings = HeightmapSettings.Sanitized();
            HeightProcessSettings = HeightProcessSettings.Sanitized();
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            Changed?.Invoke(this);
        }
    }
}
