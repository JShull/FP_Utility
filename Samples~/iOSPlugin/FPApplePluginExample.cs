namespace FuzzPhyte.Utility
{
    using UnityEngine;

    public static class FPApplePluginExample
    {
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ShowiOSFileSaveDialog(string jsonText, string fileName);
    [DllImport("__Internal")]
    private static extern void ShowiOSWavSaveDialog(byte[] wavData, int dataLength, string fileName);
    [DllImport("__Internal")]
    private static extern void ShowiOSMarkdownImportDialog();
#endif
        public static void ExportJson(string jsonText, string fileName)
        {
#if UNITY_IOS && !UNITY_EDITOR
        ShowiOSFileSaveDialog(jsonText, fileName);
#else
            Debug.LogWarning($"Simulated export: {fileName} (JSON)");
#endif
        }

        public static void ExportWav(byte[] wavData, string fileName)
        {
#if UNITY_IOS && !UNITY_EDITOR
        ShowiOSWavSaveDialog(wavData, wavData.Length, fileName);
#else
            Debug.LogWarning($"Simulated export: {fileName} (WAV)");
#endif
        }
        public static void OpenJsonImportDialog()
        {
#if UNITY_IOS && !UNITY_EDITOR
            ShowiOSMarkdownImportDialog();
#else
            Debug.LogWarning("Simulated import dialog for JSON files.");
#endif
        }
    }
}