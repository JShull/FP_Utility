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
    using System.Collections.Generic;

    [System.Serializable]
    public class LabelJsonCollection
    {
        public List<LabelJson> labels;
    }

    [System.Serializable]
    public class LabelJson
    {
        public List<LabelJsonLine> labelLines;
    }

    [System.Serializable]
    public class LabelJsonLine
    {
        public string speaker;
        public string speakerLabel;
        public string message;
        public string messageLabel;
        public string headImage;
        public string secondImage;
    }
}
