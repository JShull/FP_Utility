namespace FuzzPhyte.Utility.Debug
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
