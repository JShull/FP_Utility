using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FuzzPhyte.Utility
{
    [CreateAssetMenu(fileName = "Theme", menuName = "FuzzPhyte/Utility/Theme", order = 12)]
    public class FP_Theme : ScriptableObject
    {
        public string ThemeLabel;
        [Header("Theme Colors")]
        public Color MainColor;
        public Color SecondaryColor;
        public Color TertiaryColor;
        [Header("Font Colors")]
        public Color FontPrimaryColor;
        public Color FontSecondaryColor;
        [Tooltip("Primary Icon displayed in most UI use cases")]
        public Sprite Icon;
        public Texture TexIcon;
        [Tooltip("If we wanted to utilize a backdrop issue for other needs")]
        public Sprite BackgroundImage;
        [TextArea(2, 4)]
        public string Description;
    }
}
