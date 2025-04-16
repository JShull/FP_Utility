namespace FuzzPhyte.Utility
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName = "Theme", menuName = "FuzzPhyte/Utility/Design/Theme", order = 12)]
    public class FP_Theme : FP_Data
    {
        [Header("General")]
        [Tooltip("Label for the theme.")]
        public string ThemeLabel;

        [Header("Theme Colors")]
        [Tooltip("Main color of the theme.")]
        public Color MainColor;

        [Tooltip("Secondary color of the theme.")]
        public Color SecondaryColor;

        [Tooltip("Tertiary color of the theme.")]
        public Color TertiaryColor;

        [Header("Fonts and Sizes")]
        [Tooltip("List of font settings for different text types.")]
        public List<FontSetting> FontSettings;
        [Obsolete("Use FontSetting")]
        [Header("Font Colors")]
        [Tooltip("Primary font color used in the theme.")]
        public Color FontPrimaryColor;
        [Obsolete("Use FontSetting")]
        [Tooltip("Secondary font color used in the theme.")]
        public Color FontSecondaryColor;

        [Header("Icons and Images")]
        [Tooltip("Primary icon displayed in most UI use cases.")]
        public Sprite Icon;

        [Tooltip("Texture icon used in the theme.")]
        public Texture TexIcon;

        [Tooltip("Background image used in the theme.")]
        public Sprite BackgroundImage;

        [Header("Description")]
        [Tooltip("Description of the theme.")]
        [TextArea(2, 4)]
        public string Description;
        [TextArea(2,3)]
        public string Notes;
    }
}
