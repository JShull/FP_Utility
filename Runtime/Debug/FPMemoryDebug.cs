namespace FuzzPhyte.Utility.Debug
{
    using UnityEngine;
    using Unity.Profiling;
    public static class FPMemoryDebug
    {
        private static ProfilerRecorder _totalUsed;
        private static ProfilerRecorder _totalReserved;
        private static ProfilerRecorder _gcUsed;
        private static ProfilerRecorder _gfxUsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            _totalUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            _totalReserved = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory");
            _gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _gfxUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
        }

        public static void Log(string label)
        {
            Debug.Log(
                $"[FP_MEM] {label} | " +
                $"Used={ToMB(_totalUsed.LastValue):F1}MB | " +
                $"Reserved={ToMB(_totalReserved.LastValue):F1}MB | " +
                $"GC={ToMB(_gcUsed.LastValue):F1}MB | " +
                $"Gfx={ToMB(_gfxUsed.LastValue):F1}MB | " +
                $"Textures={Resources.FindObjectsOfTypeAll<Texture2D>().Length} | " +
                $"Audio={Resources.FindObjectsOfTypeAll<AudioClip>().Length} | " +
                $"Meshes={Resources.FindObjectsOfTypeAll<Mesh>().Length} | " +
                $"Materials={Resources.FindObjectsOfTypeAll<Material>().Length} | " +
                $"RenderTextures={Resources.FindObjectsOfTypeAll<RenderTexture>().Length} | " +
                $"ScriptableObjects={Resources.FindObjectsOfTypeAll<ScriptableObject>().Length}"
            );
        }

        private static double ToMB(long bytes)
        {
            return bytes / (1024.0 * 1024.0);
        }
    }
}
