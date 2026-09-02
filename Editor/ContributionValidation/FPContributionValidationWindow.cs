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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Runs selectable contribution checks against a Unity folder or package.
    /// </summary>
    public sealed class FPContributionValidationWindow : EditorWindow
    {
        private const float DefaultSettingsPanelWidth = 364f;
        private const float MinimumSettingsPanelWidth = 300f;
        private const float MinimumResultsPanelWidth = 320f;
        private const float PanelSplitterWidth = 8f;
        private const float BottomStatusHeight = 112f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private static readonly int PanelSplitterControlHash = "FPContributionValidationPanelSplitter".GetHashCode();

        [SerializeField] private DefaultAsset rootFolder;
        [SerializeField] private bool includeRuntime = true;
        [SerializeField] private bool includeEditor = true;
        [SerializeField] private bool includeTests = true;
        [SerializeField] private bool includeSamples = true;
        [SerializeField] private bool useRandomSample;
        [SerializeField] private int randomFileCount = 25;
        [SerializeField] private int randomSeed = 12345;

        [SerializeField] private bool checkNaming = true;
        [SerializeField] private bool checkAssemblies = true;
        [SerializeField] private bool checkNamespaces = true;
        [SerializeField] private bool checkHeaders = true;
        [SerializeField] private bool checkRuntimeEditorSeparation = true;
        [SerializeField] private bool checkObjectIdentity = true;
        [SerializeField] private bool checkRepositoryCleanliness = true;

        [SerializeField] private string namespaceRoot = "FuzzPhyte";
        [SerializeField] private string assemblyNamePrefix = "com.fuzzphyte.";
        [SerializeField] private string forbiddenTerms = string.Empty;
        [SerializeField] private bool requireInterfacePrefix = true;
        [SerializeField] private bool requireUsingsInsideNamespace = true;
        [SerializeField] private bool requireAutoReferenced;
        [SerializeField] private bool requireAllowUnsafeCode;

        [SerializeField] private bool showScope = true;
        [SerializeField] private bool showChecks = true;
        [SerializeField] private bool showStandards = true;
        [SerializeField] private bool showSamplePromotion;
        [SerializeField] private bool showPasses = true;
        [SerializeField] private bool showWarnings = true;
        [SerializeField] private bool showFailures = true;
        [SerializeField] private bool showManualReview = true;
        [SerializeField] private DefaultAsset stagingSampleFolder;
        [SerializeField] private DefaultAsset promotionPackageRoot;
        [SerializeField] private string promotionFolderName = string.Empty;
        [SerializeField] private string promotionDisplayName = string.Empty;
        [SerializeField] private string promotionDescription = string.Empty;
        [SerializeField] private FPSamplePromotionRecord lastPromotion;
        [SerializeField] private float settingsPanelWidth = DefaultSettingsPanelWidth;

        private Vector2 settingsScrollPosition;
        private Vector2 resultsScrollPosition;
        private Vector2 statusScrollPosition;
        private bool draggingPanelSplitter;
        private FPContributionValidationReport report;
        private string statusMessage = "Choose a Unity folder and run the selected checks.";

        [MenuItem(
            FP_UtilityData.MENU_UTILITY_EDITOR_PATH + "Testing/Contribution Validator",
            priority = FP_UtilityData.MENU_UTILITY_EDITOR + 12)]
        public static void ShowWindow()
        {
            FPContributionValidationWindow window = GetWindow<FPContributionValidationWindow>("Contribution Validator");
            window.minSize = new Vector2(900f, 600f);
            window.Show();
        }

        private void OnEnable()
        {
            if (rootFolder == null)
            {
                rootFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/FP_Utility");
            }
            if (promotionPackageRoot == null)
            {
                promotionPackageRoot = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/FP_Utility");
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("FP Contribution Validator", EditorStyles.boldLabel);
            DrawWorkspace();
        }

        private void DrawWorkspace()
        {
            Rect previousRect = GUILayoutUtility.GetLastRect();
            float workspaceTop = previousRect.yMax + 4f;
            Rect workspaceRect = new Rect(
                WorkspacePadding,
                workspaceTop,
                Mathf.Max(100f, position.width - (WorkspacePadding * 2f)),
                Mathf.Max(100f, position.height - workspaceTop - WorkspacePadding));

            float topHeight = Mathf.Max(220f, workspaceRect.height - BottomStatusHeight - PanelGap);
            Rect topRect = new Rect(workspaceRect.x, workspaceRect.y, workspaceRect.width, topHeight);
            Rect statusRect = new Rect(
                workspaceRect.x,
                topRect.yMax + PanelGap,
                workspaceRect.width,
                workspaceRect.height - topHeight - PanelGap);

            float maximumSettingsPanelWidth = Mathf.Max(
                MinimumSettingsPanelWidth,
                topRect.width - MinimumResultsPanelWidth - PanelSplitterWidth);
            settingsPanelWidth = Mathf.Clamp(
                settingsPanelWidth,
                MinimumSettingsPanelWidth,
                maximumSettingsPanelWidth);

            Rect settingsRect = new Rect(topRect.x, topRect.y, settingsPanelWidth, topRect.height);
            Rect splitterRect = new Rect(
                settingsRect.xMax,
                topRect.y,
                PanelSplitterWidth,
                topRect.height);
            Rect resultsRect = new Rect(
                splitterRect.xMax,
                topRect.y,
                Mathf.Max(MinimumResultsPanelWidth, topRect.xMax - splitterRect.xMax),
                topRect.height);

            DrawSettingsPanel(settingsRect);
            DrawPanelSplitter(splitterRect, topRect, maximumSettingsPanelWidth);
            DrawResultsPanel(resultsRect);
            DrawStatusPanel(statusRect);
        }

        private void DrawPanelSplitter(Rect splitterRect, Rect workspaceRect, float maximumSettingsPanelWidth)
        {
            int controlId = GUIUtility.GetControlID(
                PanelSplitterControlHash,
                FocusType.Passive,
                splitterRect);
            Event currentEvent = Event.current;

            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
            if (currentEvent.type == EventType.Repaint)
            {
                Color splitterColor = draggingPanelSplitter
                    ? FP_Utility_Editor.TextActiveColor
                    : FP_Utility_Editor.UnityEditorLiteGrey;
                Rect lineRect = new Rect(splitterRect.center.x - 1f, splitterRect.y, 2f, splitterRect.height);
                EditorGUI.DrawRect(lineRect, splitterColor);
            }

            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button != 0 || !splitterRect.Contains(currentEvent.mousePosition))
                    {
                        break;
                    }

                    GUIUtility.hotControl = controlId;
                    draggingPanelSplitter = true;
                    currentEvent.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                    {
                        break;
                    }

                    settingsPanelWidth = Mathf.Clamp(
                        currentEvent.mousePosition.x - workspaceRect.x,
                        MinimumSettingsPanelWidth,
                        maximumSettingsPanelWidth);
                    Repaint();
                    currentEvent.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId)
                    {
                        break;
                    }

                    GUIUtility.hotControl = 0;
                    draggingPanelSplitter = false;
                    EditorUtility.SetDirty(this);
                    currentEvent.Use();
                    break;
            }
        }

        private void DrawSettingsPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, rect.height - 14f);
            Rect actionsRect = new Rect(innerRect.x, innerRect.yMax - 82f, innerRect.width, 82f);
            Rect scrollRect = new Rect(innerRect.x, innerRect.y, innerRect.width, Mathf.Max(60f, innerRect.height - 88f));
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, 1280f);

            settingsScrollPosition = GUI.BeginScrollView(scrollRect, settingsScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawSettings();
            GUILayout.EndArea();
            GUI.EndScrollView();

            GUILayout.BeginArea(actionsRect);
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawActions();
            GUILayout.EndArea();
        }

        private void DrawSettings()
        {
            showScope = EditorGUILayout.Foldout(showScope, "Validation Scope", true, EditorStyles.foldoutHeader);
            if (showScope)
            {
                rootFolder = (DefaultAsset)EditorGUILayout.ObjectField("Root Folder", rootFolder, typeof(DefaultAsset), false);
                if (GUILayout.Button("Use Selected Folder"))
                {
                    UseSelectedFolder();
                }

                EditorGUILayout.LabelField("Include", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    includeRuntime = EditorGUILayout.ToggleLeft("Runtime", includeRuntime, GUILayout.Width(78f));
                    includeEditor = EditorGUILayout.ToggleLeft("Editor", includeEditor, GUILayout.Width(68f));
                    includeTests = EditorGUILayout.ToggleLeft("Tests", includeTests, GUILayout.Width(62f));
                    includeSamples = EditorGUILayout.ToggleLeft("Samples", includeSamples);
                }

                useRandomSample = EditorGUILayout.Toggle("Random Sample", useRandomSample);
                using (new EditorGUI.DisabledScope(!useRandomSample))
                {
                    randomFileCount = Mathf.Max(0, EditorGUILayout.IntField("Non-critical Count", randomFileCount));
                    randomSeed = EditorGUILayout.IntField("Seed", randomSeed);
                    if (GUILayout.Button("New Seed"))
                    {
                        randomSeed = unchecked((int)DateTime.UtcNow.Ticks);
                    }
                }

                EditorGUILayout.HelpBox(
                    "package.json, assembly definitions, and repository-junk candidates always bypass random sampling.",
                    MessageType.None);
            }

            FPMeshPreviewEditorUtility.DrawSectionDivider();
            showChecks = EditorGUILayout.Foldout(showChecks, "Checks to Run", true, EditorStyles.foldoutHeader);
            if (showChecks)
            {
                checkNaming = EditorGUILayout.ToggleLeft("Naming and Organization", checkNaming);
                checkAssemblies = EditorGUILayout.ToggleLeft("Assembly Definitions", checkAssemblies);
                checkNamespaces = EditorGUILayout.ToggleLeft("Namespaces", checkNamespaces);
                checkHeaders = EditorGUILayout.ToggleLeft("Script Headers", checkHeaders);
                checkRuntimeEditorSeparation = EditorGUILayout.ToggleLeft("Runtime / Editor Separation", checkRuntimeEditorSeparation);
                checkObjectIdentity = EditorGUILayout.ToggleLeft("Object Identity", checkObjectIdentity);
                checkRepositoryCleanliness = EditorGUILayout.ToggleLeft("Repository Cleanliness", checkRepositoryCleanliness);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select All"))
                    {
                        SetAllChecks(true);
                    }
                    if (GUILayout.Button("Clear All"))
                    {
                        SetAllChecks(false);
                    }
                }
            }

            FPMeshPreviewEditorUtility.DrawSectionDivider();
            showStandards = EditorGUILayout.Foldout(showStandards, "Configurable Standards", true, EditorStyles.foldoutHeader);
            if (showStandards)
            {
                namespaceRoot = EditorGUILayout.TextField("Namespace Root", namespaceRoot);
                assemblyNamePrefix = EditorGUILayout.TextField("Assembly Prefix", assemblyNamePrefix);
                requireInterfacePrefix = EditorGUILayout.Toggle("Require IFP Interfaces", requireInterfacePrefix);
                requireUsingsInsideNamespace = EditorGUILayout.Toggle("Usings Inside Namespace", requireUsingsInsideNamespace);
                requireAutoReferenced = EditorGUILayout.Toggle("Require Auto Referenced", requireAutoReferenced);
                requireAllowUnsafeCode = EditorGUILayout.Toggle("Require Unsafe Code", requireAllowUnsafeCode);
                EditorGUILayout.LabelField("Forbidden Terms", EditorStyles.miniBoldLabel);
                forbiddenTerms = EditorGUILayout.TextArea(forbiddenTerms, GUILayout.MinHeight(48f));
                EditorGUILayout.HelpBox("Separate forbidden terms with commas, semicolons, or new lines.", MessageType.None);
            }

            FPMeshPreviewEditorUtility.DrawSectionDivider();
            showSamplePromotion = EditorGUILayout.Foldout(
                showSamplePromotion,
                "Sample Promotion",
                true,
                EditorStyles.foldoutHeader);
            if (showSamplePromotion)
            {
                DrawSamplePromotion();
            }
        }

        private void DrawSamplePromotion()
        {
            EditorGUILayout.HelpBox(
                "Promotes a passing, unchanged staging sample into Samples~ and registers it in package.json.",
                MessageType.Info);

            stagingSampleFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Staging Folder",
                stagingSampleFolder,
                typeof(DefaultAsset),
                false);
            promotionPackageRoot = (DefaultAsset)EditorGUILayout.ObjectField(
                "Package Root",
                promotionPackageRoot,
                typeof(DefaultAsset),
                false);

            if (GUILayout.Button("Use Validation Root as Staging"))
            {
                stagingSampleFolder = rootFolder;
                FillPromotionNamesFromStaging();
            }

            promotionFolderName = EditorGUILayout.TextField("Destination Folder", promotionFolderName);
            promotionDisplayName = EditorGUILayout.TextField("Display Name", promotionDisplayName);
            EditorGUILayout.LabelField("Description", EditorStyles.miniBoldLabel);
            promotionDescription = EditorGUILayout.TextArea(promotionDescription, GUILayout.MinHeight(44f));

            string destinationPath = string.IsNullOrWhiteSpace(promotionFolderName)
                ? "Samples~/<folder>"
                : $"Samples~/{promotionFolderName.Trim()}";
            EditorGUILayout.LabelField("Manifest Path", destinationPath);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Changes"))
                {
                    PreviewPromotion();
                }

                using (new EditorGUI.DisabledScope(!CanPromoteSample()))
                {
                    Color previous = GUI.color;
                    GUI.color = FP_Utility_Editor.OkayColor;
                    if (GUILayout.Button("Promote Sample"))
                    {
                        PromoteSample();
                    }
                    GUI.color = previous;
                }
            }

            using (new EditorGUI.DisabledScope(lastPromotion == null || !lastPromotion.IsValid))
            {
                Color previous = GUI.color;
                GUI.color = FP_Utility_Editor.WarningColor;
                if (GUILayout.Button("Roll Back Last Promotion"))
                {
                    RollbackPromotion();
                }
                GUI.color = previous;
            }

            if (!CanPromoteSample())
            {
                EditorGUILayout.HelpBox(
                    "Promotion requires a completed validation of this exact staging root with zero failures.",
                    MessageType.None);
            }
        }

        private void DrawActions()
        {
            string rootPath = GetRootAssetPath();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(rootPath) || !AnyCheckEnabled()))
            {
                Color previous = GUI.color;
                GUI.color = FP_Utility_Editor.OkayColor;
                if (GUILayout.Button("Run Selected Checks", GUILayout.Height(32f)))
                {
                    RunValidation();
                }
                GUI.color = previous;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(report == null))
                {
                    if (GUILayout.Button("Export Markdown"))
                    {
                        ExportReport();
                    }
                }

                using (new EditorGUI.DisabledScope(!HasHeaderFailures()))
                {
                    if (GUILayout.Button("Fix Headers"))
                    {
                        OpenHeaderFailures();
                    }
                }
            }
        }

        private void DrawResultsPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, rect.height - 14f);
            GUILayout.BeginArea(innerRect);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
                if (report != null)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        $"{report.Count(FPContributionValidationSeverity.Failure)} failures, " +
                        $"{report.Count(FPContributionValidationSeverity.Warning)} warnings",
                        EditorStyles.miniLabel);
                }
            }

            DrawResultFilters();
            FPMeshPreviewEditorUtility.DrawSectionDivider();

            resultsScrollPosition = EditorGUILayout.BeginScrollView(resultsScrollPosition);
            if (report == null)
            {
                EditorGUILayout.HelpBox("Run validation to populate this workspace.", MessageType.None);
            }
            else if (report.Findings.Count == 0)
            {
                EditorGUILayout.HelpBox("No results were produced by the selected checks.", MessageType.None);
            }
            else
            {
                DrawFindings();
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawResultFilters()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                showPasses = GUILayout.Toggle(showPasses, "Pass", EditorStyles.toolbarButton);
                showWarnings = GUILayout.Toggle(showWarnings, "Warning", EditorStyles.toolbarButton);
                showFailures = GUILayout.Toggle(showFailures, "Failure", EditorStyles.toolbarButton);
                showManualReview = GUILayout.Toggle(showManualReview, "Manual", EditorStyles.toolbarButton);
            }
        }

        private void DrawFindings()
        {
            IEnumerable<IGrouping<string, FPContributionValidationFinding>> groups = report.Findings
                .Where(ShouldShow)
                .GroupBy(finding => finding.Rule)
                .OrderBy(group => group.Key);

            foreach (IGrouping<string, FPContributionValidationFinding> group in groups)
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                foreach (FPContributionValidationFinding finding in group)
                {
                    DrawFinding(finding);
                }
                GUILayout.Space(4f);
            }
        }

        private void DrawFinding(FPContributionValidationFinding finding)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUIStyle severityStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = GetSeverityColor(finding.Severity) }
                };
                EditorGUILayout.LabelField(finding.Severity.ToString(), severityStyle);
                EditorGUILayout.LabelField(finding.Message, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(finding.AssetPath))
                {
                    string label = finding.LineNumber > 0
                        ? $"{finding.AssetPath}:{finding.LineNumber}"
                        : finding.AssetPath;
                    if (GUILayout.Button(label, EditorStyles.linkLabel))
                    {
                        OpenFinding(finding);
                    }
                }

                if (!string.IsNullOrEmpty(finding.Remediation))
                {
                    EditorGUILayout.LabelField($"Suggested: {finding.Remediation}", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawStatusPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 7f, rect.y + 7f, rect.width - 14f, rect.height - 14f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, Mathf.Max(innerRect.height, 74f));
            statusScrollPosition = GUI.BeginScrollView(innerRect, statusScrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(statusMessage, GetStatusMessageType());
            if (report != null)
            {
                EditorGUILayout.LabelField(
                    $"Eligible: {report.EligibleAssetPaths.Count}   Scanned: {report.ScannedAssetPaths.Count}   " +
                    $"Fingerprint: {ShortFingerprint(report.Fingerprint)}",
                    EditorStyles.miniLabel);
            }
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void RunValidation()
        {
            FPContributionValidationOptions options = BuildOptions();
            statusMessage = "Validation is running...";
            try
            {
                report = FPContributionValidationUtility.Run(options, (assetPath, progress) =>
                    EditorUtility.DisplayCancelableProgressBar(
                        "FP Contribution Validator",
                        $"Inspecting {assetPath}",
                        progress));

                if (report.WasCancelled)
                {
                    statusMessage = "Validation was cancelled. Partial results were discarded.";
                    report = null;
                }
                else if (report.HasFailures)
                {
                    statusMessage = "Validation completed with failures. Review the result workspace before accepting or promoting the contribution.";
                }
                else
                {
                    statusMessage = "Validation completed without failures. Warnings and manual-review items may still require attention.";
                }
            }
            catch (Exception exception)
            {
                report = null;
                statusMessage = $"Validation could not complete: {exception.Message}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private FPContributionValidationOptions BuildOptions()
        {
            return new FPContributionValidationOptions
            {
                RootAssetPath = GetRootAssetPath(),
                IncludeRuntime = includeRuntime,
                IncludeEditor = includeEditor,
                IncludeTests = includeTests,
                IncludeSamples = includeSamples,
                UseRandomSample = useRandomSample,
                RandomFileCount = randomFileCount,
                RandomSeed = randomSeed,
                CheckNaming = checkNaming,
                CheckAssemblies = checkAssemblies,
                CheckNamespaces = checkNamespaces,
                CheckHeaders = checkHeaders,
                CheckRuntimeEditorSeparation = checkRuntimeEditorSeparation,
                CheckObjectIdentity = checkObjectIdentity,
                CheckRepositoryCleanliness = checkRepositoryCleanliness,
                NamespaceRoot = namespaceRoot,
                AssemblyNamePrefix = assemblyNamePrefix,
                ForbiddenTerms = forbiddenTerms,
                RequireInterfacePrefix = requireInterfacePrefix,
                RequireUsingsInsideNamespace = requireUsingsInsideNamespace,
                RequireAutoReferenced = requireAutoReferenced,
                RequireAllowUnsafeCode = requireAllowUnsafeCode,
                ExpectedHeader = FPScriptHeaderUtility.GetConfiguredHeaderText()
            };
        }

        private void UseSelectedFolder()
        {
            if (Selection.activeObject is DefaultAsset selectedFolder &&
                AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(selectedFolder)))
            {
                Undo.RecordObject(this, "Select Contribution Validation Folder");
                rootFolder = selectedFolder;
                EditorUtility.SetDirty(this);
                statusMessage = $"Selected validation root: {GetRootAssetPath()}";
            }
            else
            {
                statusMessage = "Select a folder in the Project window before using this action.";
            }
        }

        private void ExportReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Contribution Validation Report",
                Application.dataPath,
                "FP_ContributionValidationReport.md",
                "md");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, FPContributionValidationUtility.ToMarkdown(report));
            statusMessage = $"Exported validation report to {path}";
            if (path.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.Refresh();
            }
        }

        private void OpenHeaderFailures()
        {
            List<string> paths = report.Findings
                .Where(finding => finding.Rule == "Script Headers" &&
                                  finding.Severity != FPContributionValidationSeverity.Pass &&
                                  !string.IsNullOrEmpty(finding.AssetPath))
                .Select(finding => finding.AssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            FPScriptHeaderEditorWindow.OpenWithScripts(paths);
        }

        private void PreviewPromotion()
        {
            string source = GetFolderAssetPath(stagingSampleFolder);
            string package = GetFolderAssetPath(promotionPackageRoot);
            string destination = string.IsNullOrEmpty(package) || string.IsNullOrWhiteSpace(promotionFolderName)
                ? "Not ready"
                : $"{package}/Samples~/{promotionFolderName.Trim()}";
            string manifestEntry =
                $"Display Name: {promotionDisplayName}\n" +
                $"Description: {promotionDescription}\n" +
                $"Path: Samples~/{promotionFolderName.Trim()}";

            EditorUtility.DisplayDialog(
                "Sample Promotion Preview",
                $"Source:\n{source}\n\nDestination:\n{destination}\n\npackage.json entry:\n{manifestEntry}",
                "Close");
        }

        private void PromoteSample()
        {
            string source = GetFolderAssetPath(stagingSampleFolder);
            string package = GetFolderAssetPath(promotionPackageRoot);
            if (!EditorUtility.DisplayDialog(
                    "Promote Validated Sample?",
                    $"Move the validated sample into {package}/Samples~/{promotionFolderName.Trim()} and update package.json? " +
                    "A backup will be created for rollback.",
                    "Promote",
                    "Cancel"))
            {
                return;
            }

            FPContributionValidationOptions validationOptions = BuildOptions();
            validationOptions.RootAssetPath = source;
            var request = new FPSamplePromotionRequest
            {
                SourceAssetPath = source,
                PackageRootAssetPath = package,
                DestinationFolderName = promotionFolderName.Trim(),
                DisplayName = promotionDisplayName.Trim(),
                Description = promotionDescription.Trim(),
                ExpectedValidationFingerprint = report.Fingerprint,
                ValidationOptions = validationOptions
            };

            FPSamplePromotionResult result = FPSamplePromotionUtility.Promote(request);
            statusMessage = result.Message;
            if (result.Success)
            {
                lastPromotion = result.Record;
                stagingSampleFolder = null;
                report = null;
                EditorUtility.SetDirty(this);
            }
        }

        private void RollbackPromotion()
        {
            if (!EditorUtility.DisplayDialog(
                    "Roll Back Sample Promotion?",
                    "Restore the staging folder and the package.json backup from the last promotion?",
                    "Roll Back",
                    "Cancel"))
            {
                return;
            }

            FPSamplePromotionResult result = FPSamplePromotionUtility.Rollback(lastPromotion);
            statusMessage = result.Message;
            if (result.Success)
            {
                stagingSampleFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(lastPromotion.SourceAssetPath);
                lastPromotion = null;
                EditorUtility.SetDirty(this);
            }
        }

        private bool CanPromoteSample()
        {
            if (report == null || report.WasCancelled || report.HasFailures ||
                stagingSampleFolder == null || promotionPackageRoot == null ||
                string.IsNullOrWhiteSpace(promotionFolderName) ||
                string.IsNullOrWhiteSpace(promotionDisplayName))
            {
                return false;
            }

            return string.Equals(
                report.RootAssetPath,
                GetFolderAssetPath(stagingSampleFolder),
                StringComparison.OrdinalIgnoreCase);
        }

        private void FillPromotionNamesFromStaging()
        {
            string path = GetFolderAssetPath(stagingSampleFolder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string folderName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(promotionFolderName))
            {
                promotionFolderName = folderName;
            }
            if (string.IsNullOrWhiteSpace(promotionDisplayName))
            {
                promotionDisplayName = ObjectNames.NicifyVariableName(folderName);
            }
        }

        private void OpenFinding(FPContributionValidationFinding finding)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(finding.AssetPath);
            if (asset == null)
            {
                string fullPath = FPScriptHeaderUtility.GetFullProjectPath(finding.AssetPath);
                if (File.Exists(fullPath))
                {
                    EditorUtility.RevealInFinder(fullPath);
                }
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            if (finding.AssetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.OpenAsset(asset, Mathf.Max(1, finding.LineNumber));
            }
        }

        private bool ShouldShow(FPContributionValidationFinding finding)
        {
            return finding.Severity switch
            {
                FPContributionValidationSeverity.Pass => showPasses,
                FPContributionValidationSeverity.Warning => showWarnings,
                FPContributionValidationSeverity.Failure => showFailures,
                FPContributionValidationSeverity.ManualReview => showManualReview,
                _ => true
            };
        }

        private static Color GetSeverityColor(FPContributionValidationSeverity severity)
        {
            return severity switch
            {
                FPContributionValidationSeverity.Pass => FP_Utility_Editor.OkayColor,
                FPContributionValidationSeverity.Warning => FP_Utility_Editor.WarningColor,
                FPContributionValidationSeverity.Failure => new Color(0.95f, 0.35f, 0.3f),
                FPContributionValidationSeverity.ManualReview => FP_Utility_Editor.TextActiveColor,
                _ => Color.white
            };
        }

        private MessageType GetStatusMessageType()
        {
            if (report == null)
            {
                return statusMessage.IndexOf("could not", StringComparison.OrdinalIgnoreCase) >= 0
                    ? MessageType.Error
                    : MessageType.Info;
            }
            if (report.HasFailures)
            {
                return MessageType.Error;
            }
            if (report.Count(FPContributionValidationSeverity.Warning) > 0 ||
                report.Count(FPContributionValidationSeverity.ManualReview) > 0)
            {
                return MessageType.Warning;
            }
            return MessageType.Info;
        }

        private string GetRootAssetPath()
        {
            if (rootFolder == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(rootFolder);
            return AssetDatabase.IsValidFolder(path) ? path.Replace('\\', '/') : string.Empty;
        }

        private static string GetFolderAssetPath(DefaultAsset folder)
        {
            if (folder == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(folder);
            return AssetDatabase.IsValidFolder(path) ? path.Replace('\\', '/') : string.Empty;
        }

        private bool AnyCheckEnabled()
        {
            return checkNaming || checkAssemblies || checkNamespaces || checkHeaders ||
                   checkRuntimeEditorSeparation || checkObjectIdentity || checkRepositoryCleanliness;
        }

        private void SetAllChecks(bool enabled)
        {
            Undo.RecordObject(this, enabled ? "Select All Contribution Checks" : "Clear Contribution Checks");
            checkNaming = enabled;
            checkAssemblies = enabled;
            checkNamespaces = enabled;
            checkHeaders = enabled;
            checkRuntimeEditorSeparation = enabled;
            checkObjectIdentity = enabled;
            checkRepositoryCleanliness = enabled;
            EditorUtility.SetDirty(this);
        }

        private bool HasHeaderFailures()
        {
            return report != null && report.Findings.Any(finding =>
                finding.Rule == "Script Headers" &&
                finding.Severity != FPContributionValidationSeverity.Pass &&
                !string.IsNullOrEmpty(finding.AssetPath));
        }

        private static string ShortFingerprint(string fingerprint)
        {
            return string.IsNullOrEmpty(fingerprint)
                ? "none"
                : fingerprint.Substring(0, Mathf.Min(12, fingerprint.Length));
        }
    }
}
