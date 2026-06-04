// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.DebugTools
{
    using UnityEngine;
    using System.Collections;

    public class FPMemoryLeakTest : MonoBehaviour
    {
        public IEnumerator RunLeakTest()
        {
            for (int i = 0; i < 5; i++)
            {
                FPMemoryDebug.Log($"Leak test cycle {i} - before load");

                // TODO: load or enable your module/page/system here

                yield return new WaitForSeconds(3f);

                FPMemoryDebug.Log($"Leak test cycle {i} - after load");

                // TODO: unload or disable your module/page/system here

                yield return Resources.UnloadUnusedAssets();
                System.GC.Collect();

                yield return new WaitForSeconds(3f);

                FPMemoryDebug.Log($"Leak test cycle {i} - after unload");
            }
        }
    }
}
