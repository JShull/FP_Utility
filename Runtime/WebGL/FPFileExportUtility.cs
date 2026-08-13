// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System;
    using System.IO;
    using UnityEngine;
#if (UNITY_WEBGL || UNITY_IOS) && !UNITY_EDITOR
    using System.Runtime.InteropServices;
#endif

    /// <summary>
    /// Delivers generated binary files to the user on supported platforms.
    /// WebGL triggers a browser download, iOS opens the Files export picker,
    /// and other platforms write a unique file beneath Application.persistentDataPath.
    /// </summary>
    public static class FPFileExportUtility
    {
        private const string ExportFolderName = "FP_Exports";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void FP_DownloadBytes(
            byte[] data,
            int dataLength,
            string fileName,
            string mimeType);
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void FP_SaveBytesToFiles(
            byte[] data,
            int dataLength,
            string fileName);
#endif

        public static bool TrySaveOrDownload(
            byte[] data,
            string fileName,
            string mimeType,
            out string deliveredLocation,
            out string message)
        {
            deliveredLocation = string.Empty;
            message = string.Empty;
            if (data == null || data.Length == 0)
            {
                message = "There is no file data to export.";
                return false;
            }

            string safeFileName = SanitizeFileName(fileName);
            string safeMimeType = string.IsNullOrWhiteSpace(mimeType)
                ? "application/octet-stream"
                : mimeType;
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                FP_DownloadBytes(data, data.Length, safeFileName, safeMimeType);
                deliveredLocation = safeFileName;
                message = $"Started browser download for '{safeFileName}'.";
                return true;
#elif UNITY_IOS && !UNITY_EDITOR
                FP_SaveBytesToFiles(data, data.Length, safeFileName);
                deliveredLocation = safeFileName;
                message = $"Opened the Files export picker for '{safeFileName}'.";
                return true;
#else
                string directory = Path.Combine(Application.persistentDataPath, ExportFolderName);
                Directory.CreateDirectory(directory);
                string path = GetUniquePath(directory, safeFileName);
                File.WriteAllBytes(path, data);
                deliveredLocation = path;
                message = $"Saved '{safeFileName}' to '{path}'.";
                return true;
#endif
            }
            catch (Exception exception)
            {
                message = exception.Message;
                Debug.LogException(exception);
                return false;
            }
        }

        private static string GetUniquePath(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }

            string extension = Path.GetExtension(fileName);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            int suffix = 1;
            do
            {
                path = Path.Combine(directory, $"{stem}_{suffix}{extension}");
                suffix++;
            }
            while (File.Exists(path));

            return path;
        }

        private static string SanitizeFileName(string fileName)
        {
            string safeName = string.IsNullOrWhiteSpace(fileName)
                ? "FP_Export.bin"
                : Path.GetFileName(fileName.Trim());
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                safeName = safeName.Replace(invalidCharacters[i], '_');
            }
            return string.IsNullOrWhiteSpace(safeName) ? "FP_Export.bin" : safeName;
        }
    }
}
