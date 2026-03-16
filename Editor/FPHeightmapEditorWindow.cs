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

        [SerializeField]
        private FPMeshGridData meshDataAsset;
        [SerializeField]
        private Texture2D directHeightmap;
        [SerializeField]
        private HeightmapPreviewMode previewMode = HeightmapPreviewMode.Grayscale;
        [SerializeField]
        private bool fitToPanel = true;
        [SerializeField]
        private float zoom = 1f;
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

        private Texture2D cachedSourceTexture;
        private Texture2D cachedReadableTexture;
        private Texture2D cachedPreviewTexture;
        private HeightmapPreviewMode cachedPreviewMode;
        private Texture2D cachedBrushMaskSource;
        private Texture2D cachedBrushMaskReadable;
        private float[] histogramBins;
        private bool destroyReadableTexture;
        private bool destroyPreviewTexture;
        private bool destroyBrushMaskReadable;
        private Vector2 previewScroll;
        private Texture2D workingHeightmap;
        private Rect lastPreviewDrawRect;
        private Rect lastPreviewInnerRect;

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
            window.RefreshCache();
            window.Repaint();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            RefreshCache();
        }

        private void OnDisable()
        {
            ReleaseCachedTextures();
            ReleaseWorkingTexture();
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
            }
        }

        private void DrawParameterPanel()
        {
            meshDataAsset = (FPMeshGridData)EditorGUILayout.ObjectField("Mesh Data", meshDataAsset, typeof(FPMeshGridData), false);
            directHeightmap = (Texture2D)EditorGUILayout.ObjectField("Direct Heightmap", directHeightmap, typeof(Texture2D), false);
            previewMode = (HeightmapPreviewMode)EditorGUILayout.EnumPopup("Preview Mode", previewMode);
            fitToPanel = EditorGUILayout.Toggle("Fit To Panel", fitToPanel);

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
                if (GUILayout.Button(workingHeightmap == null ? "Create Working Copy" : "Rebuild Working Copy"))
                {
                    CreateWorkingCopyFromSource();
                }

                using (new EditorGUI.DisabledScope(workingHeightmap == null))
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

            EditorGUILayout.LabelField("Status", workingHeightmap == null ? "Using source texture preview" : $"Editing copy: {workingHeightmap.name}");
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

            using (new EditorGUI.DisabledScope(workingHeightmap == null))
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

            if (workingHeightmap == null)
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
            Texture2D previewTexture = GetPreviewTexture();
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
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                float leftColumnWidth = Mathf.Max(220f, position.width * 0.25f);
                DrawLeftColumn(leftColumnWidth);
                GUILayout.Space(8f);
                DrawPreviewPanelLayout();
            }
        }

        private void DrawLeftColumn(float width)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                DrawParameterPanelContainer(width);
                GUILayout.Space(8f);
                DrawHistogramPanel(width);
            }
        }

        private void DrawParameterPanelContainer(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                DrawParameterPanel();
            }
        }

        private void DrawPreviewPanelLayout()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                Rect rect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawPreviewPanel(rect);
            }
        }

        private void DrawHistogramPanel(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                Texture2D source = ResolveHeightmap();
                if (source == null || cachedReadableTexture == null)
                {
                    EditorGUILayout.LabelField("Histogram", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("No heightmap selected.");
                    GUILayout.FlexibleSpace();
                    return;
                }

                EditorGUILayout.LabelField("Texture Stats", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Resolution", $"{source.width} x {source.height}");
                EditorGUILayout.LabelField("Preview", previewMode.ToString());

                ComputeStats(out float minValue, out float maxValue, out float averageValue);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Sample Stats", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Min", minValue.ToString("F3"));
                EditorGUILayout.LabelField("Max", maxValue.ToString("F3"));
                EditorGUILayout.LabelField("Average", averageValue.ToString("F3"));

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Histogram", EditorStyles.boldLabel);
                Rect histogramRect = GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true));
                DrawHistogram(histogramRect);

                GUILayout.FlexibleSpace();
            }
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
            RefreshCache(true);
        }

        private void RefreshCache(bool force = false)
        {
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
            histogramBins = BuildHistogram(cachedReadableTexture, previewMode, 64);
        }

        private Texture2D GetPreviewTexture()
        {
            if (cachedPreviewTexture == null)
            {
                RefreshCache();
            }

            return cachedPreviewTexture;
        }

        private void ReleaseCachedTextures()
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
            histogramBins = null;
            destroyReadableTexture = false;
            destroyPreviewTexture = false;
            ReleaseBrushMaskTexture();
        }

        private Texture2D ResolveHeightmap()
        {
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

        private void ComputeStats(out float minValue, out float maxValue, out float averageValue)
        {
            minValue = 1f;
            maxValue = 0f;
            averageValue = 0f;

            if (cachedReadableTexture == null)
            {
                return;
            }

            Color[] pixels = cachedReadableTexture.GetPixels();
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
            if (workingHeightmap == null)
            {
                return;
            }

            string defaultName = $"{workingHeightmap.name}.png";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Working Heightmap",
                defaultName,
                "png",
                "Choose where to save the painted working copy.");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            byte[] pngBytes = workingHeightmap.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                Debug.LogWarning("[FP Heightmap Editor] Failed to encode working copy to PNG.");
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
        }

        private void ReleaseWorkingTexture()
        {
            if (workingHeightmap != null)
            {
                DestroyImmediate(workingHeightmap);
                workingHeightmap = null;
            }
        }

        private void HandleBrushPainting(Event currentEvent, Rect drawRect, Texture2D previewTexture)
        {
            if (!enableBrushEditing || workingHeightmap == null || !fitToPanel || previewTexture == null)
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

            PaintBrushStroke(uv);
            currentEvent.Use();
        }

        private void DrawBrushOverlay(Event currentEvent, Rect drawRect, Texture2D previewTexture)
        {
            if (!enableBrushEditing || workingHeightmap == null || previewTexture == null)
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
                GUI.color = new Color(1f, 1f, 1f, 0.28f);
                GUI.DrawTexture(maskRect, brushMask, ScaleMode.StretchToFill, true);
                GUI.color = previousGuiColor;
                GUI.matrix = previousMatrix;
            }
            else
            {
                Handles.color = new Color(1f, 1f, 1f, 0.95f);
                Handles.DrawWireDisc(currentEvent.mousePosition, Vector3.forward, radiusX);

                if (brushSoftness > 0f)
                {
                    Handles.color = new Color(FP_Utility_Editor.OkayColor.r, FP_Utility_Editor.OkayColor.g, FP_Utility_Editor.OkayColor.b, 0.95f);
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
            Repaint();
        }

        private float ApplyBrushMode(float currentValue, float influence)
        {
            switch (brushMode)
            {
                case HeightBrushMode.Lower:
                    return Mathf.Clamp01(currentValue - influence);
                case HeightBrushMode.Set:
                    return Mathf.Lerp(currentValue, brushSetValue, influence);
                case HeightBrushMode.Raise:
                default:
                    return Mathf.Clamp01(currentValue + influence);
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
    }
}
