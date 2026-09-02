// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.LabelDisplay
{
    using FuzzPhyte.Utility;
    using UnityEngine;

    public class LabelLine
    {
        public string speaker;
        public FontSettingLabel speakerLabel;

        [TextArea]
        public string message;
        public FontSettingLabel messageLabel;

        public string headImage;
        public string secondImage;
    }
}