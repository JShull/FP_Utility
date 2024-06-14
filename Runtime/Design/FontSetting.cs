namespace FuzzPhyte.Utility
{
    using TMPro;
    using UnityEngine;

    /// <summary>
    /// Font Setting is a data class that holds information about a specific font type.
    /// </summary>
    [CreateAssetMenu(fileName = "FontSetting", menuName = "FuzzPhyte/Utility/Design/FontSetting", order = 13)]
    public class FontSetting : FP_Data
    {
        public FontSettingLabel Label; // e.g., Header1, Header2, Paragraph
        public TMP_FontAsset Font;
        public int MinSize;
        public int MaxSize;
        public bool UseAutoSizing = false;
        public Color FontColor; // Specific color for this text type
    }
}
