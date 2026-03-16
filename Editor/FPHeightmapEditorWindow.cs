namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public class FPHeightmapEditorWindow : EditorWindow
    {
        private enum HeightmapPreviewMode
        {
            Source = 0,
            Grayscale = 1,
            Red = 2,
            Green = 3,
            Blue = 4,
            Alpha = 5
        }

        private enum HeightBrushMode
        {
            Raise = 0,
            Lower = 1,
            Set = 2
        }

        private enum GpuDebugMode
        {
            Off = 0,
            Source = 1,
            ShaderSource = 2,
            MaskInfluence = 3,
            FinalInfluence = 4
        }

        [SerializeField]
        private FPMeshGridData meshDataAsset;
        [SerializeField]
        private Texture2D directHeightmap;
        [SerializeField]
        private FPMeshGridInstance livePreviewInstance;
        [SerializeField]
        private HeightmapPreviewMode previewMode = HeightmapPreviewMode.Grayscale;
        [SerializeField]
        private bool fitToPanel = true;
        [SerializeField]
        private float zoom = 1f;
        [SerializeField]
        private bool useGpuWorkingCopy;
        [SerializeField]
        private bool enableBrushEditing;
        [SerializeField]
        private FPHeightBrushData brushPreset;
        [SerializeField]
        private Texture2D brushMask;
        [SerializeField]
        private HeightBrushMode brushMode = HeightBrushMode.Raise;
        [SerializeField]
        private int brushSizePixels = 24;
        [SerializeField]
        private float brushRotationDegrees;
        [SerializeField]
        private float brushSoftness = 0.5f;
        [SerializeField]
        private float brushStrength = 0.15f;
        [SerializeField]
        private float brushSetValue = 1f;
        [SerializeField]
        private GpuDebugMode gpuDebugMode = GpuDebugMode.Off;

        private Texture2D cachedSourceTexture;
        private Texture2D cachedReadableTexture;
        private Texture2D cachedPreviewTexture;
        private HeightmapPreviewMode cachedPreviewMode;
        private Texture2D cachedBrushMaskSource;
        private Texture2D cachedBrushMaskReadable;
        private float[] histogramBins;
        private bool analysisDirty = true;
        private bool hasAnalysisData;
        private float cachedMinValue;
        private float cachedMaxValue;
        private float cachedAverageValue;
        private bool destroyReadableTexture;
        private bool destroyPreviewTexture;
        private bool destroyBrushMaskReadable;
        private Vector2 previewScroll;
        private Vector2 parameterScroll;
        private Vector2 histogramScroll;
        private bool skipMarkAnalysisDirtyThisFrame;
        private bool analysisRefreshQueued;
        private bool liveMeshPreview = true;
        private bool liveMeshPreviewQueued;
        private double lastLiveMeshPreviewTime;
        private double lastBrushEditTime;
        private Texture2D workingHeightmap;
        private RenderTexture gpuWorkingHeightmap;
        private Rect lastPreviewDrawRect;
        private Rect lastPreviewInnerRect;
        [SerializeField]
        private float leftColumnTopRatio = 0.62f;
        [SerializeField]
        private bool autoRefreshAnalysis;
        private bool resizingLeftColumn;
        private const float LeftColumnSplitterHeight = 6f;
        private const float LeftColumnMinPanelHeight = 120f;
        private const double AnalysisRefreshDelaySeconds = 0.35d;
        private const double LiveMeshPreviewDelaySeconds = 0.12d;

        [MenuItem("FuzzPhyte/Utility/Rendering/FP Heightmap Editor", priority = FP_UtilityData.ORDER_MENU + 7)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPHeightmapEditorWindow>("FP Heightmap Editor");
            window.minSize = new Vector2(640f, 420f);
        }

        public static void OpenWindow(FPMeshGridData dataAsset, Texture2D heightmap = null)
        {
            var window = GetWindow<FPHeightmapEditorWindow>("FP Heightmap Editor");
            window.minSize = new Vector2(640f, 420f);
            window.meshDataAsset = dataAsset;
            window.directHeightmap = heightmap;
            window.livePreviewInstance = null;
            window.RefreshCache();
            window.MarkAnalysisDirty();
            window.Repaint();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            EditorApplication.update += HandleEditorUpdate;
            RefreshCache();
            MarkAnalysisDirty();
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            ReleaseCachedTextures(false);
            ReleaseWorkingTexture();
            ReleaseGpuWorkingTexture();
            FPGPUHeightmapUtility.ReleaseResources();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }

            EditorGUI.BeginChangeCheck();

            GUILayout.Label("FP Heightmap Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawWorkspace();

            if (EditorGUI.EndChangeCheck())
            {
                RefreshCache();
                if (!skipMarkAnalysisDirtyThisFrame)
                {
                    MarkAnalysisDirty();
                }
            }

            skipMarkAnalysisDirtyThisFrame = false;
        }

        private void DrawParameterPanel()
        {
            meshDataAsset = (FPMeshGridData)EditorGUILayout.ObjectField("Mesh Data", meshDataAsset, typeof(FPMeshGridData), false);
            directHeightmap = (Texture2D)EditorGUILayout.ObjectField("Direct Heightmap", directHeightmap, typeof(Texture2D), false);
            livePreviewInstance = (FPMeshGridInstance)EditorGUILayout.ObjectField("Live Preview Mesh", livePreviewInstance, typeof(FPMeshGridInstance), true);
            previewMode = (HeightmapPreviewMode)EditorGUILayout.EnumPopup("Preview Mode", previewMode);
            fitToPanel = EditorGUILayout.Toggle("Fit To Panel", fitToPanel);
            useGpuWorkingCopy = EditorGUILayout.Toggle("Use GPU Working Copy", useGpuWorkingCopy);
            if (useGpuWorkingCopy)
            {
                gpuDebugMode = (GpuDebugMode)EditorGUILayout.EnumPopup("GPU Debug View", gpuDebugMode);
            }
            autoRefreshAnalysis = EditorGUILayout.Toggle("Auto Refresh Analysis", autoRefreshAnalysis);
            liveMeshPreview = EditorGUILayout.Toggle("Live Mesh Preview", liveMeshPreview);

            using (new EditorGUI.DisabledScope(fitToPanel))
            {
                zoom = EditorGUILayout.Slider("Zoom", zoom, 0.1f, 8f);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected Grid Instance"))
                {
                    LoadFromSelectedGridInstance();
                }

                if (GUILayout.Button("Refresh Preview"))
                {
                    RefreshCache(true);
                }
            }

            using (new EditorGUI.DisabledScope(livePreviewInstance == null))
            {
                if (GUILayout.Button("Refresh Live Mesh"))
                {
                    RefreshLiveMeshPreview();
                }
            }

            DrawWorkingCopyControls();
            DrawBrushControls();

            EditorGUILayout.HelpBox(
                "Paint on a non-destructive working copy of the heightmap. The original source texture stays untouched until you save a copy.",
                MessageType.Info);
        }

        private void DrawWorkingCopyControls()
        {
            EditorGUILayout.LabelField("Working Copy", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool hasWorkingCopy = HasWorkingCopy();
                if (GUILayout.Button(hasWorkingCopy ? "Rebuild Working Copy" : "Create Working Copy"))
                {
                    CreateWorkingCopyFromSource();
                }

                using (new EditorGUI.DisabledScope(!hasWorkingCopy))
                {
                    if (GUILayout.Button("Reset Working Copy"))
                    {
                        ResetWorkingCopy();
                    }

                    if (GUILayout.Button("Save Working Copy As PNG"))
                    {
                        SaveWorkingCopyAsPng();
                    }
                }
            }

            string status = "Using source texture preview";
            if (useGpuWorkingCopy && gpuWorkingHeightmap != null)
            {
                status = $"Editing GPU copy: {gpuWorkingHeightmap.name}";
            }
            else if (workingHeightmap != null)
            {
                status = $"Editing copy: {workingHeightmap.name}";
            }

            EditorGUILayout.LabelField("Status", status);
        }

        private void DrawBrushControls()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

            brushPreset = (FPHeightBrushData)EditorGUILayout.ObjectField("Brush Preset", brushPreset, typeof(FPHeightBrushData), false);
            brushMask = (Texture2D)EditorGUILayout.ObjectField("Brush Mask", brushMask, typeof(Texture2D), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(brushPreset == null))
                {
                    if (GUILayout.Button("Load Preset"))
                    {
                        LoadBrushPreset();
                    }

                    if (GUILayout.Button("Save To Preset"))
                    {
                        SaveBrushPreset();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!HasWorkingCopy()))
            {
                enableBrushEditing = EditorGUILayout.Toggle("Enable Brush Editing", enableBrushEditing);
                brushMode = (HeightBrushMode)EditorGUILayout.EnumPopup("Brush Mode", brushMode);
                brushSizePixels = EditorGUILayout.IntSlider("Brush Size", brushSizePixels, 1, 256);
                brushRotationDegrees = EditorGUILayout.Slider("Rotation", brushRotationDegrees, 0f, 360f);
                brushSoftness = EditorGUILayout.Slider("Softness", brushSoftness, 0f, 1f);
                brushStrength = EditorGUILayout.Slider("Strength", brushStrength, 0.001f, 1f);

                using (new EditorGUI.DisabledScope(brushMode != HeightBrushMode.Set))
                {
                    brushSetValue = EditorGUILayout.Slider("Set Value", brushSetValue, 0f, 1f);
                }
            }

            if (brushMask != null)
            {
                EditorGUILayout.HelpBox(
                    "Brush mask uses the texture's white and alpha values as the stamp shape. Size, softness, and strength still apply.",
                    MessageType.None);
            }

            if (workingHeightmap == null && gpuWorkingHeightmap == null)
            {
                EditorGUILayout.HelpBox(
                    "Create a working copy before painting. Brush edits only affect the duplicated in-memory texture.",
                    MessageType.None);
            }
            else if (!fitToPanel)
            {
                EditorGUILayout.HelpBox(
                    "Brush painting currently works in Fit To Panel mode so the texture coordinates stay predictable.",
                    MessageType.None);
            }
        }

        private void DrawPreviewPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            Texture previewTexture = GetPreviewTexture();
            if (previewTexture == null)
            {
                EditorGUI.HelpBox(rect, "Assign a mesh data asset or direct heightmap to preview the texture here.", MessageType.Info);
                return;
            }

            Rect innerRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f);
            Rect drawRect = CalculateDrawRect(innerRect, previewTexture.width, previewTexture.height);
            lastPreviewInnerRect = innerRect;
            lastPreviewDrawRect = drawRect;

            if (fitToPanel)
            {
                GUI.DrawTexture(drawRect, previewTexture, ScaleMode.ScaleToFit, false);
                DrawBrushOverlay(Event.current, drawRect, previewTexture);
                HandleBrushPainting(Event.current, drawRect, previewTexture);
                return;
            }

            Rect viewRect = new Rect(0f, 0f, drawRect.width, drawRect.height);
            previewScroll = GUI.BeginScrollView(innerRect, previewScroll, viewRect);
            GUI.DrawTexture(new Rect(0f, 0f, drawRect.width, drawRect.height), previewTexture, ScaleMode.StretchToFill, false);
            GUI.EndScrollView();
        }

        private void DrawWorkspace()
        {
            Rect previousRect = GUILayoutUtility.GetLastRect();
            float workspaceTop = previousRect.yMax + 4f;
            Rect workspaceRect = new Rect(
                4f,
                workspaceTop,
                Mathf.Max(100f, position.width - 8f),
                Mathf.Max(100f, position.height - workspaceTop - 4f));
            float gap = 8f;
            float leftColumnWidth = Mathf.Clamp(workspaceRect.width * 0.25f, 220f, Mathf.Max(220f, workspaceRect.width - 220f - gap));
            Rect leftRect = new Rect(workspaceRect.x, workspaceRect.y, leftColumnWidth, workspaceRect.height);
            Rect rightRect = new Rect(leftRect.xMax + gap, workspaceRect.y, Mathf.Max(100f, workspaceRect.width - leftColumnWidth - gap), workspaceRect.height);

            DrawLeftColumnLayout(leftRect);
            DrawPreviewPanelContainer(rightRect);
        }

        private void DrawLeftColumnLayout(Rect rect)
        {
            float availableHeight = Mathf.Max((LeftColumnMinPanelHeight * 2f) + LeftColumnSplitterHeight, rect.height);
            float topHeight = Mathf.Clamp(
                availableHeight * leftColumnTopRatio,
                LeftColumnMinPanelHeight,
                availableHeight - LeftColumnMinPanelHeight - LeftColumnSplitterHeight);
            float bottomHeight = availableHeight - topHeight - LeftColumnSplitterHeight;

            Rect topRect = new Rect(rect.x, rect.y, rect.width, topHeight);
            Rect splitterRect = new Rect(rect.x, topRect.yMax, rect.width, LeftColumnSplitterHeight);
            Rect bottomRect = new Rect(rect.x, splitterRect.yMax, rect.width, bottomHeight);

            DrawParameterPanelContainer(topRect);
            DrawLeftColumnSplitter(splitterRect, rect, availableHeight);
            DrawHistogramPanel(bottomRect);
        }

        private void DrawParameterPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, CalculateParameterContentHeight(innerRect.width - 16f));

            parameterScroll = GUI.BeginScrollView(innerRect, parameterScroll, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            {
                DrawParameterPanel();
            }
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawPreviewPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
            DrawPreviewPanel(innerRect);
        }

        private void DrawHistogramPanel(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, 360f);

            histogramScroll = GUI.BeginScrollView(innerRect, histogramScroll, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            Texture source = GetAnalysisDisplayTexture();
            if (source == null)
            {
                EditorGUILayout.LabelField("Histogram", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("No heightmap selected.");
                GUILayout.FlexibleSpace();
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Texture Stats", EditorStyles.boldLabel);
                    if (GUILayout.Button("Refresh Analysis"))
                    {
                        skipMarkAnalysisDirtyThisFrame = true;
                        RefreshAnalysis(true);
                    }
                }

                if (!hasAnalysisData)
                {
                    EditorGUILayout.HelpBox(
                        "Analysis is stale or unavailable. Click Refresh Analysis to rebuild stats and histogram.",
                        MessageType.None);
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    if (analysisDirty)
                    {
                        EditorGUILayout.HelpBox(
                            "Showing the last computed analysis. It will refresh after painting settles.",
                            MessageType.None);
                    }

                    EditorGUILayout.LabelField("Resolution", $"{source.width} x {source.height}");
                    EditorGUILayout.LabelField("Preview", previewMode.ToString());
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Sample Stats", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Min", cachedMinValue.ToString("F3"));
                    EditorGUILayout.LabelField("Max", cachedMaxValue.ToString("F3"));
                    EditorGUILayout.LabelField("Average", cachedAverageValue.ToString("F3"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Histogram", EditorStyles.boldLabel);
                    Rect histogramRect = GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true));
                    DrawHistogram(histogramRect);

                    GUILayout.FlexibleSpace();
                }
            }
            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawLeftColumnSplitter(Rect splitterRect, Rect columnRect, float totalHeight)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
            EditorGUI.DrawRect(splitterRect, new Color(0.25f, 0.25f, 0.25f, 1f));

            Event current = Event.current;
            switch (current.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(current.mousePosition) && current.button == 0)
                    {
                        resizingLeftColumn = true;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (resizingLeftColumn)
                    {
                        float localY = current.mousePosition.y - columnRect.y;
                        float clampedTopHeight = Mathf.Clamp(
                            localY,
                            LeftColumnMinPanelHeight,
                            totalHeight - LeftColumnMinPanelHeight - LeftColumnSplitterHeight);
                        leftColumnTopRatio = Mathf.Clamp01(clampedTopHeight / totalHeight);
                        Repaint();
                        current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (resizingLeftColumn)
                    {
                        resizingLeftColumn = false;
                        current.Use();
                    }
                    break;
            }
        }

        private float CalculateParameterContentHeight(float width)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float estimatedLines = 34f;
            return Mathf.Max(480f, (estimatedLines * (line + spacing)) + 80f);
        }

        private void LoadFromSelectedGridInstance()
        {
            if (Selection.activeGameObject == null)
            {
                return;
            }

            var instance = Selection.activeGameObject.GetComponent<FPMeshGridInstance>();
            if (instance == null)
            {
                return;
            }

            meshDataAsset = instance.DataAsset;
            directHeightmap = meshDataAsset != null ? meshDataAsset.HeightmapSettings.Heightmap : null;
            livePreviewInstance = instance;
            RefreshCache(true);
        }

        private void RefreshCache(bool force = false)
        {
            if (gpuWorkingHeightmap != null)
            {
                RefreshCacheFromGpu(force);
                return;
            }

            Texture2D source = ResolveHeightmap();
            if (!force && source == cachedSourceTexture && previewMode == cachedPreviewMode && cachedPreviewTexture != null)
            {
                return;
            }

            ReleaseCachedTextures();

            cachedSourceTexture = source;
            if (cachedSourceTexture == null)
            {
                return;
            }

            cachedReadableTexture = GetReadableTexture(cachedSourceTexture, out destroyReadableTexture);
            if (cachedReadableTexture == null)
            {
                return;
            }

            if (previewMode == HeightmapPreviewMode.Source)
            {
                cachedPreviewTexture = cachedReadableTexture;
                destroyPreviewTexture = false;
            }
            else
            {
                cachedPreviewTexture = BuildPreviewTexture(cachedReadableTexture, previewMode);
                destroyPreviewTexture = true;
            }

            cachedPreviewMode = previewMode;
        }

        private Texture GetPreviewTexture()
        {
            if (gpuWorkingHeightmap != null && previewMode == HeightmapPreviewMode.Source)
            {
                return gpuWorkingHeightmap;
            }

            if (cachedPreviewTexture == null)
            {
                RefreshCache();
            }

            return cachedPreviewTexture;
        }

        private void ReleaseCachedTextures(bool clearAnalysis = true)
        {
            if (destroyPreviewTexture && cachedPreviewTexture != null)
            {
                DestroyImmediate(cachedPreviewTexture);
            }

            if (destroyReadableTexture && cachedReadableTexture != null)
            {
                DestroyImmediate(cachedReadableTexture);
            }

            cachedSourceTexture = null;
            cachedReadableTexture = null;
            cachedPreviewTexture = null;
            destroyReadableTexture = false;
            destroyPreviewTexture = false;
            ReleaseBrushMaskTexture();

            if (clearAnalysis)
            {
                histogramBins = null;
                hasAnalysisData = false;
            }
        }

        private Texture2D ResolveHeightmap()
        {
            if (gpuWorkingHeightmap != null)
            {
                return cachedReadableTexture;
            }

            if (workingHeightmap != null)
            {
                return workingHeightmap;
            }

            if (meshDataAsset != null && meshDataAsset.HeightmapSettings.Heightmap != null)
            {
                return meshDataAsset.HeightmapSettings.Heightmap;
            }

            return directHeightmap;
        }

        private Texture2D ResolveSourceHeightmap()
        {
            if (meshDataAsset != null && meshDataAsset.HeightmapSettings.Heightmap != null)
            {
                return meshDataAsset.HeightmapSettings.Heightmap;
            }

            return directHeightmap;
        }

        private static Texture2D GetReadableTexture(Texture2D source, out bool destroyWhenDone)
        {
            destroyWhenDone = false;
            if (source == null)
            {
                return null;
            }

            try
            {
                source.GetPixel(0, 0);
                return source;
            }
            catch (UnityException)
            {
                destroyWhenDone = true;
                return source.Decompress();
            }
        }

        private static Texture2D BuildPreviewTexture(Texture2D source, HeightmapPreviewMode mode)
        {
            Texture2D preview = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = $"{source.name}_{mode}_Preview"
            };

            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = EvaluateSample(pixels[i], mode);
                pixels[i] = new Color(value, value, value, 1f);
            }

            preview.SetPixels(pixels);
            preview.Apply(false, false);
            return preview;
        }

        private static float[] BuildHistogram(Texture2D source, HeightmapPreviewMode mode, int binCount)
        {
            float[] bins = new float[binCount];
            Color[] pixels = source.GetPixels();
            if (pixels == null || pixels.Length == 0)
            {
                return bins;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                float value = Mathf.Clamp01(EvaluateSample(pixels[i], mode));
                int bin = Mathf.Clamp(Mathf.FloorToInt(value * (binCount - 1)), 0, binCount - 1);
                bins[bin] += 1f;
            }

            float maxCount = 1f;
            for (int i = 0; i < bins.Length; i++)
            {
                maxCount = Mathf.Max(maxCount, bins[i]);
            }

            for (int i = 0; i < bins.Length; i++)
            {
                bins[i] /= maxCount;
            }

            return bins;
        }

        private void ComputeStats(Texture2D sourceTexture, out float minValue, out float maxValue, out float averageValue)
        {
            minValue = 1f;
            maxValue = 0f;
            averageValue = 0f;

            if (sourceTexture == null)
            {
                return;
            }

            Color[] pixels = sourceTexture.GetPixels();
            if (pixels == null || pixels.Length == 0)
            {
                return;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                float value = Mathf.Clamp01(EvaluateSample(pixels[i], previewMode));
                minValue = Mathf.Min(minValue, value);
                maxValue = Mathf.Max(maxValue, value);
                averageValue += value;
            }

            averageValue /= pixels.Length;
        }

        private void DrawHistogram(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f, 1f));
            if (histogramBins == null || histogramBins.Length == 0)
            {
                return;
            }

            float barWidth = rect.width / histogramBins.Length;
            for (int i = 0; i < histogramBins.Length; i++)
            {
                float height = rect.height * histogramBins[i];
                Rect barRect = new Rect(rect.x + (i * barWidth), rect.yMax - height, Mathf.Max(1f, barWidth - 1f), height);
                EditorGUI.DrawRect(barRect, FP_Utility_Editor.OkayColor);
            }
        }

        private Rect CalculateDrawRect(Rect bounds, int textureWidth, int textureHeight)
        {
            if (fitToPanel)
            {
                float aspect = textureWidth / (float)textureHeight;
                float width = bounds.width;
                float height = width / aspect;
                if (height > bounds.height)
                {
                    height = bounds.height;
                    width = height * aspect;
                }

                return new Rect(
                    bounds.x + ((bounds.width - width) * 0.5f),
                    bounds.y + ((bounds.height - height) * 0.5f),
                    width,
                    height);
            }

            return new Rect(0f, 0f, textureWidth * zoom, textureHeight * zoom);
        }

        private static float EvaluateSample(Color color, HeightmapPreviewMode mode)
        {
            switch (mode)
            {
                case HeightmapPreviewMode.Red:
                    return color.r;
                case HeightmapPreviewMode.Green:
                    return color.g;
                case HeightmapPreviewMode.Blue:
                    return color.b;
                case HeightmapPreviewMode.Alpha:
                    return color.a;
                case HeightmapPreviewMode.Grayscale:
                    return color.grayscale;
                case HeightmapPreviewMode.Source:
                default:
                    return color.grayscale;
            }
        }

        private void CreateWorkingCopyFromSource()
        {
            Texture2D source = ResolveSourceHeightmap();
            if (source == null)
            {
                return;
            }

            ReleaseWorkingTexture();
            ReleaseGpuWorkingTexture();

            if (useGpuWorkingCopy)
            {
                gpuWorkingHeightmap = FPGPUHeightmapUtility.CreateWorkingCopy(source, $"{source.name}_GPUWorkingCopy");
                enableBrushEditing = false;
                RefreshCache(true);
                return;
            }

            Texture2D readableSource = GetReadableTexture(source, out bool destroyReadable);
            if (readableSource == null)
            {
                return;
            }

            workingHeightmap = new Texture2D(readableSource.width, readableSource.height, TextureFormat.RGBA32, false)
            {
                name = $"{source.name}_WorkingCopy"
            };
            workingHeightmap.SetPixels(readableSource.GetPixels());
            workingHeightmap.Apply(false, false);

            if (destroyReadable)
            {
                DestroyImmediate(readableSource);
            }

            enableBrushEditing = true;
            RefreshCache(true);
            MarkAnalysisDirty();
        }

        private void ResetWorkingCopy()
        {
            if (workingHeightmap == null)
            {
                return;
            }

            CreateWorkingCopyFromSource();
        }

        private void SaveWorkingCopyAsPng()
        {
            bool hasCpuCopy = workingHeightmap != null;
            bool hasGpuCopy = gpuWorkingHeightmap != null;
            if (!hasCpuCopy && !hasGpuCopy)
            {
                return;
            }

            string exportName = hasGpuCopy ? gpuWorkingHeightmap.name : workingHeightmap.name;
            string defaultName = $"{exportName}.png";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Working Heightmap",
                defaultName,
                "png",
                "Choose where to save the painted working copy.");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Texture2D exportTexture = hasGpuCopy
                ? FPGPUHeightmapUtility.ReadbackToTexture2D(gpuWorkingHeightmap)
                : workingHeightmap;

            byte[] pngBytes = exportTexture != null ? exportTexture.EncodeToPNG() : null;
            if (pngBytes == null || pngBytes.Length == 0)
            {
                Debug.LogWarning("[FP Heightmap Editor] Failed to encode working copy to PNG.");
                if (hasGpuCopy && exportTexture != null)
                {
                    DestroyImmediate(exportTexture);
                }
                return;
            }

            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            File.WriteAllBytes(absolutePath, pngBytes);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();

            var savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (meshDataAsset != null && savedTexture != null)
            {
                Undo.RecordObject(meshDataAsset, "Assign Saved Heightmap Copy");
                meshDataAsset.HeightmapSettings.Heightmap = savedTexture;
                EditorUtility.SetDirty(meshDataAsset);
                AssetDatabase.SaveAssets();
            }

            directHeightmap = savedTexture != null ? savedTexture : directHeightmap;
            if (hasGpuCopy && exportTexture != null)
            {
                DestroyImmediate(exportTexture);
            }
            MarkAnalysisDirty();
        }

        private void ReleaseWorkingTexture()
        {
            if (workingHeightmap != null)
            {
                DestroyImmediate(workingHeightmap);
                workingHeightmap = null;
            }
        }

        private void ReleaseGpuWorkingTexture()
        {
            if (gpuWorkingHeightmap != null)
            {
                FPGPUHeightmapUtility.Release(gpuWorkingHeightmap);
                gpuWorkingHeightmap = null;
            }
        }

        private void HandleBrushPainting(Event currentEvent, Rect drawRect, Texture previewTexture)
        {
            if (!enableBrushEditing || !HasWorkingCopy() || !fitToPanel || previewTexture == null)
            {
                return;
            }

            bool isPaintEvent =
                (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag) &&
                currentEvent.button == 0 &&
                drawRect.Contains(currentEvent.mousePosition);

            if (!isPaintEvent)
            {
                return;
            }

            Vector2 uv = new Vector2(
                Mathf.InverseLerp(drawRect.xMin, drawRect.xMax, currentEvent.mousePosition.x),
                1f - Mathf.InverseLerp(drawRect.yMin, drawRect.yMax, currentEvent.mousePosition.y));

            if (gpuWorkingHeightmap != null)
            {
                PaintGpuBrushStroke(uv);
            }
            else
            {
                PaintBrushStroke(uv);
            }
            currentEvent.Use();
        }

        private void DrawBrushOverlay(Event currentEvent, Rect drawRect, Texture previewTexture)
        {
            if (!enableBrushEditing || !HasWorkingCopy() || previewTexture == null)
            {
                return;
            }

            if (!drawRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            float scaleX = drawRect.width / previewTexture.width;
            float scaleY = drawRect.height / previewTexture.height;
            float radiusPixels = Mathf.Max(1f, brushSizePixels);
            float radiusX = radiusPixels * scaleX;
            float radiusY = radiusPixels * scaleY;
            float hardRadius = Mathf.Max(0.001f, radiusPixels * (1f - brushSoftness));
            float hardRadiusX = hardRadius * scaleX;
            float hardRadiusY = hardRadius * scaleY;
            Color brushOverlayColor = GetBrushOverlayColor();

            Handles.BeginGUI();
            Color previousColor = Handles.color;

            if (brushMask != null)
            {
                Color previousGuiColor = GUI.color;
                Matrix4x4 previousMatrix = GUI.matrix;
                Rect maskRect = new Rect(
                    currentEvent.mousePosition.x - radiusX,
                    currentEvent.mousePosition.y - radiusY,
                    radiusX * 2f,
                    radiusY * 2f);
                GUIUtility.RotateAroundPivot(brushRotationDegrees, currentEvent.mousePosition);
                GUI.color = new Color(brushOverlayColor.r, brushOverlayColor.g, brushOverlayColor.b, 0.28f);
                GUI.DrawTexture(maskRect, brushMask, ScaleMode.StretchToFill, true);
                GUI.color = previousGuiColor;
                GUI.matrix = previousMatrix;
            }
            else
            {
                Handles.color = new Color(brushOverlayColor.r, brushOverlayColor.g, brushOverlayColor.b, 0.95f);
                Handles.DrawWireDisc(currentEvent.mousePosition, Vector3.forward, radiusX);

                if (brushSoftness > 0f)
                {
                    Handles.color = new Color(brushOverlayColor.r, brushOverlayColor.g, brushOverlayColor.b, 0.55f);
                    Handles.DrawWireDisc(currentEvent.mousePosition, Vector3.forward, hardRadiusX);
                }
            }

            Handles.color = previousColor;
            Handles.EndGUI();

            Repaint();
        }

        private void PaintBrushStroke(Vector2 uv)
        {
            if (workingHeightmap == null)
            {
                return;
            }

            int width = workingHeightmap.width;
            int height = workingHeightmap.height;
            int centerX = Mathf.RoundToInt(uv.x * (width - 1));
            int centerY = Mathf.RoundToInt(uv.y * (height - 1));
            int radius = Mathf.Max(1, brushSizePixels);
            int minX = Mathf.Max(0, centerX - radius);
            int maxX = Mathf.Min(width - 1, centerX + radius);
            int minY = Mathf.Max(0, centerY - radius);
            int maxY = Mathf.Min(height - 1, centerY + radius);

            float hardRadius = Mathf.Max(0.001f, radius * (1f - brushSoftness));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    if (distance > radius)
                    {
                        continue;
                    }

                    float radialFalloff = distance <= hardRadius
                        ? 1f
                        : 1f - Mathf.InverseLerp(hardRadius, radius, distance);
                    float maskInfluence = EvaluateBrushMask(
                        x,
                        y,
                        centerX,
                        centerY,
                        radius,
                        radialFalloff);
                    float influence = maskInfluence * brushStrength;
                    if (influence <= 0f)
                    {
                        continue;
                    }

                    Color pixel = workingHeightmap.GetPixel(x, y);
                    float currentValue = pixel.grayscale;
                    float nextValue = ApplyBrushMode(currentValue, influence);
                    workingHeightmap.SetPixel(x, y, new Color(nextValue, nextValue, nextValue, 1f));
                }
            }

            workingHeightmap.Apply(false, false);
            RefreshCache(true);
            MarkAnalysisDirty();
            QueueDeferredAnalysisRefresh();
            QueueLiveMeshPreviewRefresh();
            Repaint();
        }

        private void PaintGpuBrushStroke(Vector2 uv)
        {
            if (gpuWorkingHeightmap == null)
            {
                return;
            }

            bool applied = FPGPUHeightmapUtility.ApplyBrushStroke(
                gpuWorkingHeightmap,
                brushMask,
                uv,
                brushSizePixels,
                brushSoftness,
                brushStrength,
                brushSetValue,
                brushRotationDegrees,
                (int)brushMode,
                (int)gpuDebugMode);

            if (!applied)
            {
                return;
            }

            if (previewMode != HeightmapPreviewMode.Source)
            {
                RefreshCache(true);
            }
            MarkAnalysisDirty();
            QueueDeferredAnalysisRefresh();
            QueueLiveMeshPreviewRefresh();
            Repaint();
        }

        private bool HasWorkingCopy()
        {
            return workingHeightmap != null || gpuWorkingHeightmap != null;
        }

        private void RefreshCacheFromGpu(bool force)
        {
            if (gpuWorkingHeightmap == null)
            {
                return;
            }

            if (!force && cachedPreviewMode == previewMode && cachedReadableTexture != null)
            {
                return;
            }

            ReleaseCachedTextures(false);
            cachedReadableTexture = FPGPUHeightmapUtility.ReadbackToTexture2D(gpuWorkingHeightmap);
            cachedSourceTexture = cachedReadableTexture;
            destroyReadableTexture = true;

            if (previewMode == HeightmapPreviewMode.Source)
            {
                cachedPreviewTexture = cachedReadableTexture;
                destroyPreviewTexture = false;
            }
            else
            {
                cachedPreviewTexture = BuildPreviewTexture(cachedReadableTexture, previewMode);
                destroyPreviewTexture = true;
            }

            cachedPreviewMode = previewMode;
        }

        private float ApplyBrushMode(float currentValue, float influence)
        {
            switch (brushMode)
            {
                case HeightBrushMode.Lower:
                    return Mathf.Lerp(currentValue, 0f, influence);
                case HeightBrushMode.Set:
                    return Mathf.Lerp(currentValue, brushSetValue, influence);
                case HeightBrushMode.Raise:
                default:
                    return Mathf.Lerp(currentValue, 1f, influence);
            }
        }

        private void LoadBrushPreset()
        {
            if (brushPreset == null)
            {
                return;
            }

            enableBrushEditing = brushPreset.EnableBrushEditing;
            brushMask = brushPreset.BrushMask;
            brushMode = (HeightBrushMode)Mathf.Clamp(brushPreset.BrushMode, 0, Enum.GetValues(typeof(HeightBrushMode)).Length - 1);
            brushSizePixels = Mathf.Max(1, brushPreset.BrushSizePixels);
            brushRotationDegrees = Mathf.Repeat(brushPreset.BrushRotationDegrees, 360f);
            brushSoftness = Mathf.Clamp01(brushPreset.BrushSoftness);
            brushStrength = Mathf.Clamp01(brushPreset.BrushStrength);
            brushSetValue = Mathf.Clamp01(brushPreset.BrushSetValue);
            Repaint();
        }

        private void SaveBrushPreset()
        {
            if (brushPreset == null)
            {
                return;
            }

            Undo.RecordObject(brushPreset, "Update Height Brush Preset");
            brushPreset.Capture(
                enableBrushEditing,
                brushMask,
                (int)brushMode,
                brushSizePixels,
                brushRotationDegrees,
                brushSoftness,
                brushStrength,
                brushSetValue);
            EditorUtility.SetDirty(brushPreset);
            AssetDatabase.SaveAssets();
        }

        private float EvaluateBrushMask(int x, int y, int centerX, int centerY, int radius, float radialFalloff)
        {
            if (brushMask == null)
            {
                return radialFalloff;
            }

            Texture2D readableMask = GetReadableBrushMask();
            if (readableMask == null)
            {
                return radialFalloff;
            }

            float u = Mathf.InverseLerp(centerX - radius, centerX + radius, x);
            float v = Mathf.InverseLerp(centerY - radius, centerY + radius, y);
            Vector2 rotatedUv = RotateUv(new Vector2(u, v), brushRotationDegrees);
            Color maskSample = readableMask.GetPixelBilinear(rotatedUv.x, rotatedUv.y);
            float maskValue = Mathf.Clamp01(maskSample.grayscale * maskSample.a);
            return radialFalloff * maskValue;
        }

        private static Vector2 RotateUv(Vector2 uv, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            Vector2 centered = uv - new Vector2(0.5f, 0.5f);
            Vector2 rotated = new Vector2(
                (centered.x * cos) - (centered.y * sin),
                (centered.x * sin) + (centered.y * cos));
            rotated += new Vector2(0.5f, 0.5f);
            rotated.x = Mathf.Clamp01(rotated.x);
            rotated.y = Mathf.Clamp01(rotated.y);
            return rotated;
        }

        private Texture2D GetReadableBrushMask()
        {
            if (brushMask == null)
            {
                ReleaseBrushMaskTexture();
                return null;
            }

            if (cachedBrushMaskSource == brushMask && cachedBrushMaskReadable != null)
            {
                return cachedBrushMaskReadable;
            }

            ReleaseBrushMaskTexture();
            cachedBrushMaskSource = brushMask;
            cachedBrushMaskReadable = GetReadableTexture(brushMask, out destroyBrushMaskReadable);
            return cachedBrushMaskReadable;
        }

        private void ReleaseBrushMaskTexture()
        {
            if (destroyBrushMaskReadable && cachedBrushMaskReadable != null)
            {
                DestroyImmediate(cachedBrushMaskReadable);
            }

            cachedBrushMaskSource = null;
            cachedBrushMaskReadable = null;
            destroyBrushMaskReadable = false;
        }

        private Color GetBrushOverlayColor()
        {
            switch (brushMode)
            {
                case HeightBrushMode.Lower:
                    return new Color(0.08f, 0.08f, 0.08f, 1f);
                case HeightBrushMode.Set:
                    return new Color(0.15f, 0.7f, 1f, 1f);
                case HeightBrushMode.Raise:
                default:
                    return Color.white;
            }
        }

        private void MarkAnalysisDirty()
        {
            analysisDirty = true;
            if (autoRefreshAnalysis)
            {
                RefreshAnalysis(true);
            }
        }

        private void QueueDeferredAnalysisRefresh()
        {
            analysisRefreshQueued = true;
            lastBrushEditTime = EditorApplication.timeSinceStartup;
        }

        private void QueueLiveMeshPreviewRefresh()
        {
            if (!liveMeshPreview || livePreviewInstance == null)
            {
                return;
            }

            liveMeshPreviewQueued = true;
            lastLiveMeshPreviewTime = EditorApplication.timeSinceStartup;
        }

        private void RefreshAnalysis(bool force = false)
        {
            if (!force && !analysisDirty)
            {
                return;
            }

            if (gpuWorkingHeightmap != null)
            {
                ReleaseCachedAnalysisTextureIfNeeded();
                cachedReadableTexture = FPGPUHeightmapUtility.ReadbackToTexture2D(gpuWorkingHeightmap);
                cachedSourceTexture = null;
                destroyReadableTexture = true;
            }
            else
            {
                Texture2D source = ResolveHeightmap();
                if (source == null)
                {
                    hasAnalysisData = false;
                    return;
                }

                if (cachedReadableTexture == null || cachedSourceTexture != source)
                {
                    ReleaseCachedAnalysisTextureIfNeeded();
                    cachedReadableTexture = GetReadableTexture(source, out destroyReadableTexture);
                    cachedSourceTexture = source;
                }
            }

            if (cachedReadableTexture == null)
            {
                hasAnalysisData = false;
                return;
            }

            ComputeStats(cachedReadableTexture, out cachedMinValue, out cachedMaxValue, out cachedAverageValue);
            histogramBins = BuildHistogram(cachedReadableTexture, previewMode, 64);
            analysisDirty = false;
            hasAnalysisData = true;
            analysisRefreshQueued = false;
        }

        private void ReleaseCachedAnalysisTextureIfNeeded()
        {
            if (destroyReadableTexture && cachedReadableTexture != null)
            {
                DestroyImmediate(cachedReadableTexture);
            }

            cachedReadableTexture = null;
            destroyReadableTexture = false;
        }

        private Texture GetAnalysisDisplayTexture()
        {
            if (gpuWorkingHeightmap != null)
            {
                return gpuWorkingHeightmap;
            }

            if (workingHeightmap != null)
            {
                return workingHeightmap;
            }

            if (meshDataAsset != null && meshDataAsset.HeightmapSettings.Heightmap != null)
            {
                return meshDataAsset.HeightmapSettings.Heightmap;
            }

            return directHeightmap;
        }

        private void HandleEditorUpdate()
        {
            if (liveMeshPreviewQueued && !Application.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - lastLiveMeshPreviewTime >= LiveMeshPreviewDelaySeconds)
                {
                    RefreshLiveMeshPreview();
                    liveMeshPreviewQueued = false;
                }
            }

            if (!analysisRefreshQueued || autoRefreshAnalysis)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - lastBrushEditTime < AnalysisRefreshDelaySeconds)
            {
                return;
            }

            RefreshAnalysis(true);
            Repaint();
        }

        private void RefreshLiveMeshPreview()
        {
            if (Application.isPlaying || livePreviewInstance == null)
            {
                return;
            }

            Texture2D overrideHeightmap = null;
            bool destroyOverride = false;

            if (gpuWorkingHeightmap != null)
            {
                overrideHeightmap = FPGPUHeightmapUtility.ReadbackToTexture2D(gpuWorkingHeightmap);
                destroyOverride = overrideHeightmap != null;
            }
            else if (workingHeightmap != null)
            {
                overrideHeightmap = workingHeightmap;
            }

            livePreviewInstance.RegenerateWithHeightmapOverride(overrideHeightmap);

            if (destroyOverride && overrideHeightmap != null)
            {
                DestroyImmediate(overrideHeightmap);
            }
        }
    }
}
