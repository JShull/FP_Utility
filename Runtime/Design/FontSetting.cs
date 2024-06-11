namespace FuzzPhyte.Utility
{
    using UnityEngine;

    /// <summary>
    /// Font Setting is a data class that holds information about a specific font type.
    /// </summary>
    [CreateAssetMenu(fileName = "FontSetting", menuName = "FuzzPhyte/Utility/Design/FontSetting", order = 13)]
    public class FontSetting : FP_Data
    {
        public FontSettingLabel Label; // e.g., Header1, Header2, Paragraph
        public Font Font;
        public int MinSize;
        public int MaxSize;
        public Color FontColor; // Specific color for this text type
    }
}
