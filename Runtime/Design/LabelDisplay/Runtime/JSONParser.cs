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
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public static class JSONParser
    {
        public static List<LabelLine> Parse(string json, int labelIndex)
        {
            LabelJsonCollection jsonData = JsonUtility.FromJson<LabelJsonCollection>(json);

            if (jsonData == null ||
                jsonData.labels == null ||
                labelIndex < 0 ||
                labelIndex >= jsonData.labels.Count)
            {
                Debug.LogError($"Label index {labelIndex} does not exist.");
                return null;
            }

            LabelJson selectedLabel = jsonData.labels[labelIndex];

            List<LabelLine> labelLines = new List<LabelLine>();

            foreach (LabelJsonLine jsonLine in selectedLabel.labelLines)
            {
                LabelLine line = new LabelLine();

                line.speaker = jsonLine.speaker;
                line.message = jsonLine.message;
                line.headImage = jsonLine.headImage;
                line.secondImage = jsonLine.secondImage;

                if (!string.IsNullOrEmpty(jsonLine.speakerLabel))
                {
                    line.speakerLabel =
                        ParseLabel(jsonLine.speakerLabel);
                }

                if (!string.IsNullOrEmpty(jsonLine.messageLabel))
                {
                    line.messageLabel =
                        ParseLabel(jsonLine.messageLabel);
                }

                labelLines.Add(line);
            }

            return labelLines;
        }

        private static FontSettingLabel ParseLabel(string fontLabel)
        {
            return (FontSettingLabel)Enum.Parse(typeof(FontSettingLabel), fontLabel);
        }
    }
}