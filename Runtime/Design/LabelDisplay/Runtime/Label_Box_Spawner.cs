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

    public static class Label_Box_Spawner
    {
        public static void Spawn(TextAsset jsonFile, int labelIndex, Vector3 worldPosition, GameObject labelPrefab, FP_Theme theme, out GameObject spawnedLabel)
        {
            spawnedLabel = null;

            if (jsonFile == null)
            {
                Debug.LogError("Could not find dialogue JSON!");
                return;
            }

            var label = JSONParser.Parse(jsonFile.text, labelIndex);

            if (label == null)
            {
                Debug.LogError($"Could not load dialogue at index {labelIndex}.");
                return;
            }

            spawnedLabel = Object.Instantiate(labelPrefab, worldPosition, Quaternion.identity);

            spawnedLabel.GetComponent<Label_Box_Controller>().Init(theme, label, 5);
        }
    }
}