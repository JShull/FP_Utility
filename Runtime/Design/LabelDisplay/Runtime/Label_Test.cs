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

    public class Label_Test : MonoBehaviour
    {

        //    public static void Spawn(TextAsset jsonFile, int labelIndex, Vector3 worldPosition, GameObject labelPrefab, FP_Theme theme, out GameObject spawnedLabel)

        public TextAsset jsonFile;
        public int LabelIndex;
        public Vector3 worldPosition;
        public GameObject labelPrefab;
        public FP_Theme theme;

        GameObject dud;

        [ContextMenu("Spawn Label")]
        void TestSpawnLabel()
        {
            Label_Box_Spawner.Spawn(jsonFile, LabelIndex, worldPosition, labelPrefab, theme, out dud);
        }
    }
}
