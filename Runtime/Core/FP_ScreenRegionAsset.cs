using UnityEngine;

namespace FuzzPhyte.Utility
{
    [CreateAssetMenu(fileName = "FP_ScreenRegionAsset", menuName = "FuzzPhyte/Utility/Design/DrawScreenRegionAsset")]
    public class FP_ScreenRegionAsset : FP_Data
    {
        public FP_ScreenRegion[] Region;
    }
}
