namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName = "FP_HHeaderData", menuName = "FuzzPhyte/Utility/Editor/HeaderData")]
    public class FP_HHeaderData : FP_Data
    {
        public Texture2D CloseIcon;
        public Texture2D OpenIcon;
        public Texture2D SelectAllIcon;
        public Texture2D SelectAllIconActive;
        public Color ExpandedColor= new Color(0.412f, 0.678f, 0, 1);
        public Color CollapsedColor= new Color(0.728f, 0.678f, 0, 1);
        public Color HeaderColor = new Color(0.0f, 0.0f, 0.0f, 1);
        [Space]
        [Header("Headers")]
        public List<string> Headers = new List<string>(); 
    }
}
