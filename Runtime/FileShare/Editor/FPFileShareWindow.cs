// Copyright (c) 2026 John B. Shull. FuzzPhyte LLC.
// Public license: GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.FileShare.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using UnityEditor;
    using UnityEngine;
    using FuzzPhyte.Utility.Editor;

    public sealed class FPFileShareWindow : EditorWindow
    {
        [SerializeField] private string inbox = "";
        [SerializeField] private string address = "127.0.0.1";
        [SerializeField] private int port = 18765;
        [SerializeField] private int maximumMegabytes = 128;
        [SerializeField] private string sourcePath = "";
        [SerializeField] private string receiverUrl = "";
        [NonSerialized] private string senderToken = "";
        private FPFileShareReceiver receiver;
        private CancellationTokenSource sending;
        private Guid transferId;
        private float progress;
        private string senderStatus = "Choose a local file to test the runtime sender.";
        private string receiverStatus = "Stopped";
        private readonly List<FPFileShareReceivedFile> files = new List<FPFileShareReceivedFile>();
        private Vector2 scroll;

        [MenuItem(FP_UtilityData.MENU_UTILITY_EDITOR_PATH + "File Share", priority = FP_UtilityData.MENU_UTILITY_EDITOR + 5)]
        public static void ShowWindow() => GetWindow<FPFileShareWindow>("File Share");

        private void OnEnable()
        {
            minSize = new Vector2(540, 530);
            if (string.IsNullOrEmpty(inbox)) inbox = Path.Combine(Application.persistentDataPath, "FP_FileShareInbox");
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += StopAll;
            EditorApplication.quitting += StopAll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            StopAll();
            EditorApplication.update -= Poll;
            AssemblyReloadEvents.beforeAssemblyReload -= StopAll;
            EditorApplication.quitting -= StopAll;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode) StopAll();
        }

        private void StopAll()
        {
            sending?.Cancel();
            receiver?.Dispose();
            receiver = null;
            receiverStatus = "Stopped";
        }

        private void Poll()
        {
            bool changed = false;
            if (receiver != null)
            {
                changed = receiverStatus != receiver.Status;
                receiverStatus = receiver.Status;
                while (receiver.TryDequeue(out var file))
                {
                    files.RemoveAll(f => f.Receipt.TransferId == file.Receipt.TransferId);
                    files.Insert(0, file);
                    if (files.Count > 30) files.RemoveAt(files.Count - 1);
                    changed = true;
                }
            }
            if (sending != null)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                changed = true;
            }
            if (changed) Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Receive files", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Start only on a trusted local network. This first version uses HTTP: the pairing token limits access but does not encrypt files. No Play Mode is required.", MessageType.Info);
                using (new EditorGUI.DisabledScope(receiver != null))
                {
                    address = EditorGUILayout.TextField("Listen address", address);
                    if (GUILayout.Button("Choose local address")) ShowAddresses();
                    port = EditorGUILayout.IntField("Port", port);
                    maximumMegabytes = EditorGUILayout.IntField("Maximum file (MB)", maximumMegabytes);
                    EditorGUILayout.SelectableLabel(inbox, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("Choose inbox…"))
                    {
                        string selected = EditorUtility.OpenFolderPanel("File Share inbox (outside Assets and Packages)", inbox, "");
                        if (!string.IsNullOrEmpty(selected)) inbox = selected;
                    }
                }
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = FP_Utility_Editor.OkayColor;
                if (GUILayout.Button(receiver == null ? "Start receiver" : "Stop receiver", GUILayout.Height(26)))
                {
                    if (receiver == null) StartReceiver();
                    else { receiver.Dispose(); receiver = null; receiverStatus = "Stopped"; }
                }
                GUI.backgroundColor = oldColor;
                if (receiver != null)
                {
                    EditorGUILayout.LabelField("Receiver address");
                    EditorGUILayout.SelectableLabel(receiver.Endpoint, GUILayout.Height(18));
                    EditorGUILayout.LabelField("Pairing token (changes on restart)");
                    EditorGUILayout.SelectableLabel(receiver.PairingToken, GUILayout.Height(18));
                    if (GUILayout.Button("Use this receiver in test sender"))
                    { receiverUrl = receiver.Endpoint; senderToken = receiver.PairingToken; }
                }
                EditorGUILayout.HelpBox(receiverStatus, receiver != null && receiver.IsRunning ? MessageType.Info : MessageType.None);
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Test sender", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(sending != null))
                {
                    receiverUrl = EditorGUILayout.TextField("Receiver address", receiverUrl);
                    senderToken = EditorGUILayout.PasswordField("Pairing token", senderToken);
                    EditorGUILayout.SelectableLabel(sourcePath, GUILayout.Height(18));
                    if (GUILayout.Button("Choose file…"))
                    {
                        string selected = EditorUtility.OpenFilePanel("Send a file", "", "");
                        if (!string.IsNullOrEmpty(selected)) { sourcePath = selected; transferId = Guid.NewGuid(); }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Send / retry same file")) Send();
                        if (GUILayout.Button("New transfer ID")) transferId = Guid.NewGuid();
                    }
                }
                if (sending != null)
                {
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, "Sending file");
                    if (GUILayout.Button("Cancel upload")) sending.Cancel();
                }
                EditorGUILayout.HelpBox(senderStatus, MessageType.None);
            }
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Received files", EditorStyles.boldLabel);
                if (files.Count == 0) EditorGUILayout.LabelField("Completed, verified files will appear here.");
                foreach (var file in files)
                {
                    EditorGUILayout.LabelField(file.Receipt.FileName, $"{file.Receipt.ByteLength:N0} bytes");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Reveal")) EditorUtility.RevealInFinder(file.Path);
                        using (new EditorGUI.DisabledScope(!CanImport(file.Path)))
                            if (GUILayout.Button("Import into Assets…")) Import(file);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void ShowAddresses()
        {
            var menu = new GenericMenu();
            var addresses = new HashSet<string> { "127.0.0.1" };
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                if (network.OperationalStatus == OperationalStatus.Up)
                    foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork) addresses.Add(unicast.Address.ToString());
            foreach (string item in addresses.OrderBy(a => a))
                menu.AddItem(new GUIContent(item), item == address, () => address = item);
            menu.ShowAsContext();
        }

        private void StartReceiver()
        {
            try
            {
                string path = Path.GetFullPath(inbox).Replace('\\', '/').TrimEnd('/');
                string project = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
                foreach (string root in new[] { project + "/Assets", project + "/Packages", project + "/Library" })
                    if (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("Choose an inbox outside Assets, Packages, and Library. Import completed files using the Import button.");
                if (maximumMegabytes < 1 || maximumMegabytes > 2048) throw new ArgumentException("Choose a limit between 1 and 2048 MB.");
                receiver = new FPFileShareReceiver(address, port, inbox, maximumMegabytes * 1024L * 1024);
                receiverStatus = "Listening";
            }
            catch (HttpListenerException ex)
            {
                receiverStatus = "Could not listen: " + ex.Message + " Check the address/port. Windows may require a URL reservation for this exact address; see the FileShare README. No firewall or system settings were changed.";
            }
            catch (Exception ex) { receiverStatus = ex.Message; }
        }

        private async void Send()
        {
            if (sending != null) return;
            if (transferId == Guid.Empty) transferId = Guid.NewGuid();
            var cancellation = new CancellationTokenSource();
            sending = cancellation;
            progress = 0;
            senderStatus = "Hashing and sending…";
            try
            {
                var receipt = await FPFileShareSender.SendAsync(sourcePath, receiverUrl, senderToken, transferId,
                    new Progress<float>(value => progress = value), cancellation.Token);
                senderStatus = (receipt.AlreadyReceived ? "Already received and verified: " : "Received and verified: ") + receipt.FileName;
            }
            catch (OperationCanceledException) { senderStatus = "Cancelled. Local file retained; retry with the same ID to check whether it arrived."; }
            catch (Exception ex) { senderStatus = ex.Message; }
            finally { sending = null; cancellation.Dispose(); }
        }

        private static bool CanImport(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return new[] { ".json", ".wav", ".png", ".jpg", ".jpeg", ".txt", ".csv" }.Contains(extension);
        }

        private void Import(FPFileShareReceivedFile file)
        {
            try
            {
                string extension = Path.GetExtension(file.Receipt.FileName).TrimStart('.');
                string path = EditorUtility.SaveFilePanelInProject("Import received file", Path.GetFileNameWithoutExtension(file.Receipt.FileName), extension, "Choose a project destination.");
                if (string.IsNullOrEmpty(path)) return;
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                File.Copy(file.Path, path, false);
                AssetDatabase.ImportAsset(path);
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(path);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            catch (Exception ex) { receiverStatus = "Import failed: " + ex.Message; }
        }
    }
}
