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
#if ((UNITY_WEBGL || UNITY_IOS) && !UNITY_EDITOR) || UNITY_STANDALONE_WIN
    using System.Runtime.InteropServices;
#endif
#if UNITY_STANDALONE_WIN
    using System.Text;
#endif

    public delegate bool FPFileExportHandler(
        byte[] data,
        string fileName,
        string mimeType,
        out string deliveredLocation,
        out string message);

    /// <summary>
    /// Delivers generated binary files to the user on supported platforms.
    /// WebGL triggers a browser download, iOS opens the Files export picker,
    /// Windows uses Save As, and other platforms write beneath Application.persistentDataPath.
    /// </summary>
    public static class FPFileExportUtility
    {
        private const string ExportFolderName = "FP_Exports";

        public static FPFileExportHandler PlatformSaveHandler { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void FP_DownloadBytes(
            byte[] data,
            int dataLength,
            string fileName,
            string mimeType);
#endif

#if UNITY_STANDALONE_WIN
        private const int OfnOverwritePrompt = 0x00000002;
        private const int OfnNoChangeDirectory = 0x00000008;
        private const int OfnPathMustExist = 0x00000800;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int StructureSize;
            public IntPtr OwnerWindow;
            public IntPtr Instance;
            public string Filter;
            public string CustomFilter;
            public int MaximumCustomFilter;
            public int FilterIndex;
            public StringBuilder File;
            public int MaximumFile;
            public StringBuilder FileTitle;
            public int MaximumFileTitle;
            public string InitialDirectory;
            public string Title;
            public int Flags;
            public short FileOffset;
            public short FileExtension;
            public string DefaultExtension;
            public IntPtr CustomData;
            public IntPtr Hook;
            public string TemplateName;
            public IntPtr Reserved;
            public int ReservedFlags;
            public int ExtendedFlags;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetSaveFileNameW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSaveFileName(ref OpenFileName openFileName);

        [DllImport("comdlg32.dll")]
        private static extern int CommDlgExtendedError();
#endif

        /// <summary>
        /// Registers a host-specific save prompt. FP Utility's editor assembly uses this
        /// to keep UnityEditor APIs out of the runtime assembly.
        /// </summary>
        public static void SetPlatformSaveHandler(FPFileExportHandler handler)
        {
            PlatformSaveHandler = handler;
        }

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
                if (PlatformSaveHandler != null)
                {
                    return PlatformSaveHandler(
                        data,
                        safeFileName,
                        safeMimeType,
                        out deliveredLocation,
                        out message);
                }

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
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return TrySaveWithWindowsPrompt(
                    data,
                    safeFileName,
                    out deliveredLocation,
                    out message);
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

#if UNITY_STANDALONE_WIN
        private static bool TrySaveWithWindowsPrompt(
            byte[] data,
            string fileName,
            out string deliveredLocation,
            out string message)
        {
            deliveredLocation = string.Empty;
            string extension = Path.GetExtension(fileName);
            string extensionWithoutDot = extension.TrimStart('.');
            var fileBuffer = new StringBuilder(1024);
            fileBuffer.Append(fileName);
            var dialog = new OpenFileName
            {
                StructureSize = Marshal.SizeOf(typeof(OpenFileName)),
                Filter = BuildWindowsFilter(extension),
                FilterIndex = 1,
                File = fileBuffer,
                MaximumFile = fileBuffer.Capacity,
                InitialDirectory = Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                Title = "Save Export",
                Flags = OfnOverwritePrompt | OfnNoChangeDirectory | OfnPathMustExist,
                DefaultExtension = extensionWithoutDot
            };

            if (!GetSaveFileName(ref dialog))
            {
                int errorCode = CommDlgExtendedError();
                if (errorCode == 0)
                {
                    message = "File export was cancelled.";
                    return true;
                }

                message = $"Windows Save As failed with dialog error 0x{errorCode:X}.";
                return false;
            }

            string path = dialog.File.ToString();
            File.WriteAllBytes(path, data);
            deliveredLocation = path;
            message = $"Saved '{fileName}' to '{path}'.";
            return true;
        }

        private static string BuildWindowsFilter(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "All files (*.*)\0*.*\0\0";
            }

            string label = string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)
                ? "ZIP archive"
                : extension.TrimStart('.').ToUpperInvariant() + " file";
            return $"{label} (*{extension})\0*{extension}\0All files (*.*)\0*.*\0\0";
        }
#endif

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
