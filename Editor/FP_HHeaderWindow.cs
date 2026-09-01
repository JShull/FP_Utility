// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public class FP_HHeaderWindow : EditorWindow
    {
        [SerializeField] private FP_HHeaderData headerData;
        [SerializeField] private Texture2D hierarchyIconOverride;
        [SerializeField] private bool includeChildren;

        private Vector2 scrollPosition;
        private string statusMessage;
        private MessageType statusMessageType = MessageType.Info;

        [MenuItem("FuzzPhyte/Header/Header Options", false, priority = FP_UtilityData.MENU_FUZZPHYTE_HEADER)]
        private static void OpenWindow()
        {
            FP_HHeaderWindow window = GetWindow<FP_HHeaderWindow>("Header Options");
            window.minSize = new Vector2(420f, 430f);
            window.Show();
        }

        private void OnEnable()
        {
            if (headerData == null)
            {
                headerData = FP_HHeader.GetActiveHeaderDataAsset();
            }

            Selection.selectionChanged -= Repaint;
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FP Header Options", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawHeaderStylePanel();
            EditorGUILayout.Space(6f);
            DrawHierarchyIconPanel();

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(statusMessage, statusMessageType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeaderStylePanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Header Style", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Assign an FP_HHeaderData asset to apply the current header visuals or create scene headers from that data.", MessageType.Info);

                EditorGUI.BeginChangeCheck();
                headerData = (FP_HHeaderData)EditorGUILayout.ObjectField("Header Data", headerData, typeof(FP_HHeaderData), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Repaint();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Use Active Scene Style") && FP_HHeader.GetActiveHeaderDataAsset() != null)
                    {
                        headerData = FP_HHeader.GetActiveHeaderDataAsset();
                    }

                    if (GUILayout.Button("Ping Asset") && headerData != null)
                    {
                        EditorGUIUtility.PingObject(headerData);
                        Selection.activeObject = headerData;
                    }
                }

                EditorGUILayout.Space();
                using (new EditorGUI.DisabledScope(headerData == null))
                {
                    Color previousColor = GUI.backgroundColor;
                    GUI.backgroundColor = FP_Utility_Editor.OkayColor;
                    if (GUILayout.Button("Apply Header Style", GUILayout.Height(28f)))
                    {
                        FP_HHeader.ApplyHeaderDataAsset(headerData, false);
                    }

                    if (GUILayout.Button("Create Headers From Data", GUILayout.Height(28f)))
                    {
                        FP_HHeader.ApplyHeaderDataAsset(headerData, true);
                    }
                    GUI.backgroundColor = previousColor;
                }

                if (headerData == null)
                {
                    EditorGUILayout.HelpBox("No FP_HHeaderData asset assigned.", MessageType.Warning);
                }
            }
        }

        private void DrawHierarchyIconPanel()
        {
            var targets = FPHierarchyIconUtility.CollectSelectionTargets(
                includeChildren,
                out int skippedHeaderCount,
                out int skippedNonSceneCount);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Hierarchy Icon Override", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Assign Unity's native custom icon to ordinary scene GameObjects. FP Header objects are always excluded.",
                    MessageType.Info);

                EditorGUILayout.LabelField("Eligible Objects", targets.Count.ToString());
                if (skippedHeaderCount > 0 || skippedNonSceneCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"Skipped {skippedHeaderCount} FP Header object(s) and {skippedNonSceneCount} non-scene object(s).",
                        MessageType.Warning);
                }

                if (targets.Count == 1)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField(
                            "Current Override",
                            FPHierarchyIconUtility.GetIcon(targets[0]),
                            typeof(Texture2D),
                            false);
                    }
                }

                hierarchyIconOverride = (Texture2D)EditorGUILayout.ObjectField(
                    "Icon Override",
                    hierarchyIconOverride,
                    typeof(Texture2D),
                    false);

                if (hierarchyIconOverride != null)
                {
                    Rect previewRect = GUILayoutUtility.GetRect(40f, 40f, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(previewRect, hierarchyIconOverride, ScaleMode.ScaleToFit, true);
                }

                if (GUILayout.Button("Import Icon..."))
                {
                    ImportHierarchyIcon();
                }

                includeChildren = EditorGUILayout.ToggleLeft(
                    "Include Children (Recursive)",
                    includeChildren);

                if (includeChildren)
                {
                    EditorGUILayout.HelpBox(
                        "Recursive mode includes inactive descendants. FP Header objects remain excluded.",
                        MessageType.Warning);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(targets.Count == 0 || hierarchyIconOverride == null))
                    {
                        Color previousColor = GUI.backgroundColor;
                        GUI.backgroundColor = FP_Utility_Editor.OkayColor;
                        if (GUILayout.Button("Apply To Selected", GUILayout.Height(28f)))
                        {
                            int changedCount = FPHierarchyIconUtility.ApplyIcon(targets, hierarchyIconOverride);
                            SetStatus($"Applied the icon override to {changedCount} GameObject(s).", MessageType.Info);
                        }
                        GUI.backgroundColor = previousColor;
                    }

                    using (new EditorGUI.DisabledScope(targets.Count == 0))
                    {
                        Color previousColor = GUI.backgroundColor;
                        GUI.backgroundColor = FP_Utility_Editor.WarningColor;
                        if (GUILayout.Button("Clear Selected Icons", GUILayout.Height(28f)))
                        {
                            int changedCount = FPHierarchyIconUtility.ClearIcons(targets);
                            SetStatus($"Cleared the icon override from {changedCount} GameObject(s).", MessageType.Info);
                        }
                        GUI.backgroundColor = previousColor;
                    }
                }

                if (targets.Count == 0)
                {
                    EditorGUILayout.HelpBox("Select one or more ordinary scene GameObjects to apply an icon override.", MessageType.None);
                }
            }
        }

        private void ImportHierarchyIcon()
        {
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Hierarchy Icon",
                string.Empty,
                new[] { "Image files", "png,jpg,jpeg,tga,psd", "All files", "*" });
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            string extension = Path.GetExtension(sourcePath).TrimStart('.');
            string defaultName = Path.GetFileNameWithoutExtension(sourcePath);
            string destinationPath = EditorUtility.SaveFilePanelInProject(
                "Import Hierarchy Icon",
                defaultName,
                extension,
                "Choose a project-owned location for the hierarchy icon.",
                "Assets");
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return;
            }

            try
            {
                destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);
                FileUtil.CopyFileOrDirectory(sourcePath, destinationPath);
                AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);

                Texture2D importedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath);
                if (importedIcon == null)
                {
                    SetStatus($"Unity imported the file but did not recognize it as a Texture2D: {destinationPath}", MessageType.Error);
                    return;
                }

                hierarchyIconOverride = importedIcon;
                EditorGUIUtility.PingObject(importedIcon);
                SetStatus($"Imported hierarchy icon: {destinationPath}", MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus($"Could not import the hierarchy icon: {exception.Message}", MessageType.Error);
            }
        }

        private void SetStatus(string message, MessageType messageType)
        {
            statusMessage = message;
            statusMessageType = messageType;
            Repaint();
        }
    }
}
