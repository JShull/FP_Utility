// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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
        [Space]
        public bool UseAutoSizing = false;
        public FontStyles FontStyle;
        public TextAlignmentOptions FontAlignment;
        public Color FontColor; // Specific color for this text type
    }
}
