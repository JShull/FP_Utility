namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.IO;
    using UnityEditor;

    /// <summary>
    /// Supplies FPFileExportUtility with Unity's native editor Save File panel.
    /// </summary>
    [InitializeOnLoad]
    internal static class FPFileExportUtilityEditorBridge
    {
        static FPFileExportUtilityEditorBridge()
        {
            FPFileExportUtility.SetPlatformSaveHandler(SaveWithPrompt);
        }

        private static bool SaveWithPrompt(
            byte[] data,
            string fileName,
            string mimeType,
            out string deliveredLocation,
            out string message)
        {
            string extension = Path.GetExtension(fileName).TrimStart('.');
            string defaultName = Path.GetFileNameWithoutExtension(fileName);
            string initialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
            string path = EditorUtility.SaveFilePanel(
                "Save Export",
                initialDirectory,
                defaultName,
                extension);
            if (string.IsNullOrWhiteSpace(path))
            {
                deliveredLocation = string.Empty;
                message = "File export was cancelled.";
                return true;
            }

            File.WriteAllBytes(path, data);
            deliveredLocation = path;
            message = $"Saved '{fileName}' to '{path}'.";
            return true;
        }
    }
}
