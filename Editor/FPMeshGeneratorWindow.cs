// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Creates a flat rectangular grid mesh that can later be used for heightmap deformation.
    /// </summary>
    public class FPMeshGeneratorWindow : EditorWindow
    {
        [SerializeField]
        private FPMeshGridBuildSettings gridSettings = FPMeshGridBuildSettings.Default;
        [SerializeField]
        private FPMeshGridData meshDataAsset;
        [SerializeField]
        private GameObject lastGeneratedObject;
        [SerializeField]
        private Transform targetParent;
        [SerializeField]
        private Material previewMaterial;
        [SerializeField]
        private bool mapHeightmapToSurface = true;
        [SerializeField]
        private Texture2D surfaceTextureOverride;
        [SerializeField]
        private bool addMeshCollider;
        [SerializeField]
        private FPMeshHeightmapSettings heightmapSettings = FPMeshHeightmapSettings.Default;
        [SerializeField]
        private FPMeshHeightProcessSettings heightProcessSettings = FPMeshHeightProcessSettings.Default;
        [SerializeField]
        private bool autoUpdatePreviewObject = false;
        [SerializeField]
        private bool autoRebuildPreview = false;
        [SerializeField]
        private FPMeshPreviewProjection cameraProjection = FPMeshPreviewProjection.Perspective;
        [SerializeField]
        private bool invertCameraOrbit = false;
        [SerializeField]
        private bool showSurfaces = true;
        [SerializeField]
        private bool showVertices = false;
        [SerializeField]
        private bool showEdges = false;

        private Vector2 scrollPosition;
        private Mesh previewMesh;
        private PreviewRenderUtility previewUtility;
        private Material generatedPreviewMaterial;
        private Material texturedPreviewMaterial;
        private Material texturedPreviewSourceMaterial;
        private Material generatedSceneMaterial;
        private Material generatedSceneSourceMaterial;
        private Quaternion previewRotation = Quaternion.Euler(38f, -35f, 0f);
        private float previewZoom = 1.35f;
        private int activeOrbitAxis = -1;
        private bool previewDirty = true;
        private bool effectiveGridDirty = true;
        private FPMeshGridBuildSettings effectiveGridSettings = FPMeshGridBuildSettings.Default;
        private bool geoTiffInspectionDirty = true;
        private string geoTiffInspectionPath = string.Empty;
        private FPMeshGeoTiffRaster geoTiffInspectionRaster;
        private string geoTiffInspectionError = string.Empty;
        private bool sonarLogInspectionDirty = true;
        private string sonarLogInspectionPath = string.Empty;
        private string sonarLogInspectionSettingsKey = string.Empty;
        private FPMeshSonarLogRaster sonarLogInspectionRaster;
        private string sonarLogInspectionError = string.Empty;

        private const float ParameterPanelWidth = 352f;
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 6f;
        private const float ActionPanelHeight = 128f;
        private const float ParameterViewHeight = 1780f;
        private const float Omniscan450SsDefaultRangeMeters = 30f;
        private const float Omniscan450SsMaxRangeMeters = 150f;
        private const float Omniscan450SsDefaultPingRateHz = 20f;
        private const int Omniscan450SsRangeSamples = 1200;

        [MenuItem("FuzzPhyte/Utility/Mesh/Mesh Generator", priority = FP_UtilityData.MENU_UTILITY_MESH + 4)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPMeshGeneratorWindow>("Mesh Generator");
            window.minSize = new Vector2(760f, 520f);
            window.SyncSelectionDefaults();
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(gridSettings.MeshName))
            {
                gridSettings = FPMeshGridBuildSettings.Default;
            }

            SyncSelectionDefaults();
            EnsurePreviewUtility();
            previewDirty = true;
        }

        private void OnDisable()
        {
            CleanupPreviewMesh();

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }

            if (generatedPreviewMaterial != null)
            {
                DestroyImmediate(generatedPreviewMaterial);
                generatedPreviewMaterial = null;
            }

            if (texturedPreviewMaterial != null)
            {
                DestroyImmediate(texturedPreviewMaterial);
                texturedPreviewMaterial = null;
                texturedPreviewSourceMaterial = null;
            }

            generatedSceneMaterial = null;
            generatedSceneSourceMaterial = null;
        }

        private void OnGUI()
        {
            GUILayout.Label("Mesh Generator", EditorStyles.boldLabel);
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

            float leftWidth = Mathf.Clamp(ParameterPanelWidth, 260f, Mathf.Max(260f, workspaceRect.width - 280f - PanelGap));
            Rect parameterRect = new Rect(workspaceRect.x, workspaceRect.y, leftWidth, workspaceRect.height);
            Rect previewRect = new Rect(parameterRect.xMax + PanelGap, workspaceRect.y, Mathf.Max(100f, workspaceRect.xMax - parameterRect.xMax - PanelGap), workspaceRect.height);

            DrawParameterPanelContainer(parameterRect);
            DrawPreviewPanelContainer(previewRect);
        }

        private void DrawParameterPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            Rect actionRect = new Rect(innerRect.x, innerRect.yMax - ActionPanelHeight, innerRect.width, ActionPanelHeight);
            Rect scrollRect = new Rect(innerRect.x, innerRect.y, innerRect.width, Mathf.Max(40f, innerRect.height - ActionPanelHeight - 6f));
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, ParameterViewHeight);

            scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));
            DrawParameterPanel();
            GUILayout.EndArea();
            GUI.EndScrollView();

            GUILayout.BeginArea(actionRect);
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawActions();
            GUILayout.EndArea();
        }

        private void DrawParameterPanel()
        {
            EditorGUI.BeginChangeCheck();

            DrawHeader();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawDataAssetSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawCameraSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawGridSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawHeightmapSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawHeightProcessSettings();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawSceneSettings();

            if (EditorGUI.EndChangeCheck())
            {
                previewDirty = true;
                effectiveGridDirty = true;
                Repaint();
            }
        }

        private void DrawHeader()
        {
            GUILayout.Label("Mesh Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build a flat rectangular grid mesh on the XZ plane. " +
                "This first pass is intended as the base surface for future heightmap deformation.",
                MessageType.Info);
        }

        private void DrawCameraSettings()
        {
            EditorGUILayout.LabelField("Camera Properties", EditorStyles.boldLabel);
            cameraProjection = FPMeshPreviewEditorUtility.DrawProjectionPopup(cameraProjection);
            invertCameraOrbit = FPMeshPreviewEditorUtility.DrawRightAlignedToggle("Invert Camera Orbit", invertCameraOrbit);
        }

        private void DrawGridSettings()
        {
            EditorGUILayout.LabelField("Grid Settings", EditorStyles.boldLabel);

            FPMeshGridBuildSettings effectiveSettings = GetEffectiveGridSettings();
            bool sourceScaleRequested = IsSourceRealScaleRequested();
            if (sourceScaleRequested)
            {
                SyncGridDimensionsToEffectiveBounds(effectiveSettings);
            }

            gridSettings.MeshName = EditorGUILayout.TextField("Mesh Name", gridSettings.MeshName);
            using (new EditorGUI.DisabledScope(sourceScaleRequested))
            {
                float width = sourceScaleRequested ? effectiveSettings.Width : gridSettings.Width;
                float length = sourceScaleRequested ? effectiveSettings.Length : gridSettings.Length;
                width = EditorGUILayout.FloatField("Width", width);
                length = EditorGUILayout.FloatField("Length", length);

                if (!sourceScaleRequested)
                {
                    gridSettings.Width = width;
                    gridSettings.Length = length;
                }
            }

            gridSettings.XSegments = EditorGUILayout.IntField("X Segments", gridSettings.XSegments);
            gridSettings.YSegments = EditorGUILayout.IntField("Y Segments", gridSettings.YSegments);
            gridSettings.CenterPivot = EditorGUILayout.Toggle("Center Pivot", gridSettings.CenterPivot);

            var safeSettings = gridSettings.Sanitized();
            int vertexCount = (safeSettings.XSegments + 1) * (safeSettings.YSegments + 1);
            int quadCount = safeSettings.XSegments * safeSettings.YSegments;
            int triangleCount = quadCount * 2;

            if (sourceScaleRequested)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Effective Bounds", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField("Width", effectiveSettings.Width);
                    EditorGUILayout.FloatField("Length", effectiveSettings.Length);
                }

                EditorGUILayout.HelpBox(
                    "Match source real scale is active. Width and Length are locked to the active source bounds so the editable grid values and generated mesh stay in sync.",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Stats", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Vertices", vertexCount.ToString());
            EditorGUILayout.LabelField("Quads", quadCount.ToString());
            EditorGUILayout.LabelField("Triangles", triangleCount.ToString());
        }

        private void DrawDataAssetSettings()
        {
            EditorGUILayout.LabelField("Mesh Data", EditorStyles.boldLabel);

            meshDataAsset = (FPMeshGridData)EditorGUILayout.ObjectField("Data Asset", meshDataAsset, typeof(FPMeshGridData), false);

            using (new EditorGUI.DisabledScope(meshDataAsset == null))
            {
                if (GUILayout.Button("Load Settings From Data Asset"))
                {
                    LoadSettingsFromDataAsset();
                }

                if (GUILayout.Button("Save Current Settings To Data Asset"))
                {
                    SaveSettingsToDataAsset();
                }
            }

            if (meshDataAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an FPMeshGridData asset if you want to store and regenerate a mesh recipe from a reusable data file.",
                    MessageType.None);
            }
        }

        private void DrawSceneSettings()
        {
            EditorGUILayout.LabelField("Scene Output", EditorStyles.boldLabel);

            targetParent = (Transform)EditorGUILayout.ObjectField("Parent", targetParent, typeof(Transform), true);
            previewMaterial = (Material)EditorGUILayout.ObjectField("Material", previewMaterial, typeof(Material), false);
            mapHeightmapToSurface = EditorGUILayout.Toggle("Map Image To Surface", mapHeightmapToSurface);
            using (new EditorGUI.DisabledScope(!mapHeightmapToSurface))
            {
                surfaceTextureOverride = (Texture2D)EditorGUILayout.ObjectField("Surface Texture", surfaceTextureOverride, typeof(Texture2D), false);
            }

            if (mapHeightmapToSurface && ResolveSurfaceTexture() == null)
            {
                EditorGUILayout.HelpBox("Assign a Heightmap texture or Surface Texture to preview the raster on the generated mesh surface.", MessageType.None);
            }

            addMeshCollider = EditorGUILayout.Toggle("Add MeshCollider", addMeshCollider);
            using (new EditorGUI.DisabledScope(IsDirectSourceModeActive()))
            {
                autoRebuildPreview = EditorGUILayout.Toggle("Auto Rebuild Preview", autoRebuildPreview);
            }

            autoUpdatePreviewObject = EditorGUILayout.Toggle("Update Scene Object On Refresh", autoUpdatePreviewObject);
            if (IsDirectSourceModeActive())
            {
                EditorGUILayout.HelpBox("Auto preview rebuild is disabled for direct GeoTIFF/sonar sources. Use Refresh Preview Mesh to control expensive source reads.", MessageType.None);
            }

            if (GUILayout.Button("Use Current Selection As Parent"))
            {
                SyncSelectionDefaults();
            }
        }

        private void DrawHeightmapSettings()
        {
            EditorGUILayout.LabelField("Heightmap", EditorStyles.boldLabel);

            Texture2D previousHeightmap = heightmapSettings.Heightmap;
            heightmapSettings.Heightmap = (Texture2D)EditorGUILayout.ObjectField("Heightmap", heightmapSettings.Heightmap, typeof(Texture2D), false);
            bool useGeoTiff = EditorGUILayout.Toggle("Use GeoTIFF Elevation", heightmapSettings.UseGeoTiffElevationData);
            if (useGeoTiff && !heightmapSettings.UseGeoTiffElevationData)
            {
                heightmapSettings.UseSonarLogDepthData = false;
            }

            heightmapSettings.UseGeoTiffElevationData = useGeoTiff;
            if (heightmapSettings.UseGeoTiffElevationData)
            {
                SyncGeoTiffSourcePathFromHeightmap(previousHeightmap);
            }

            bool useSonarLog = EditorGUILayout.Toggle("Use Sonar Log Depth", heightmapSettings.UseSonarLogDepthData);
            if (useSonarLog && !heightmapSettings.UseSonarLogDepthData)
            {
                heightmapSettings.UseGeoTiffElevationData = false;
            }

            heightmapSettings.UseSonarLogDepthData = useSonarLog;

            heightmapSettings.HeightScale = EditorGUILayout.FloatField("Height Scale", heightmapSettings.HeightScale);
            heightmapSettings.HeightOffset = EditorGUILayout.FloatField("Height Offset", heightmapSettings.HeightOffset);
            heightmapSettings.Channel = (FPMeshHeightmapChannel)EditorGUILayout.EnumPopup("Channel", heightmapSettings.Channel);

            if (heightmapSettings.UseGeoTiffElevationData)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("GeoTIFF Reference", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(heightmapSettings.Heightmap != null))
                {
                    heightmapSettings.GeoTiffSourcePath = EditorGUILayout.TextField("GeoTIFF File", heightmapSettings.GeoTiffSourcePath);
                }

                using (new EditorGUI.DisabledScope(heightmapSettings.Heightmap != null))
                {
                    if (GUILayout.Button("Browse", GUILayout.Width(64f)))
                    {
                        string selectedPath = EditorUtility.OpenFilePanel("Select GeoTIFF Elevation File", GetGeoTiffBrowseFolder(), "tif,tiff");
                        if (!string.IsNullOrWhiteSpace(selectedPath))
                        {
                            heightmapSettings.GeoTiffSourcePath = ToProjectRelativePath(selectedPath);
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
                heightmapSettings.GeoTiffCoordinateSystem = (FPMeshGeoTiffCoordinateSystem)EditorGUILayout.EnumPopup("Coordinate System", heightmapSettings.GeoTiffCoordinateSystem);
                using (new EditorGUI.DisabledScope(heightmapSettings.GeoTiffCoordinateSystem == FPMeshGeoTiffCoordinateSystem.WGS84))
                {
                    heightmapSettings.GeoTiffProjectedCrs = EditorGUILayout.TextField("Projected CRS", heightmapSettings.GeoTiffProjectedCrs);
                }

                heightmapSettings.GeoTiffGridAnchor = (FPMeshGeoTiffGridAnchor)EditorGUILayout.EnumPopup("Grid Anchor", heightmapSettings.GeoTiffGridAnchor);
                heightmapSettings.GeoTiffNorthToPositiveZ = EditorGUILayout.Toggle("North To +Z", heightmapSettings.GeoTiffNorthToPositiveZ);
                heightmapSettings.MatchGridToGeoTiffScale = EditorGUILayout.Toggle("Match Grid Real Scale", heightmapSettings.MatchGridToGeoTiffScale);
                using (new EditorGUI.DisabledScope(heightmapSettings.GeoTiffCoordinateSystem == FPMeshGeoTiffCoordinateSystem.WGS84 || !heightmapSettings.MatchGridToGeoTiffScale))
                {
                    heightmapSettings.GeoTiffHorizontalUnitsToMeters = EditorGUILayout.FloatField("Units To Meters", heightmapSettings.GeoTiffHorizontalUnitsToMeters);
                }

                EditorGUILayout.HelpBox(
                    "GeoTIFF files are sampled directly against the existing local grid UVs. If a Heightmap asset is assigned, its project path is used automatically; use GeoTIFF File only for external files.",
                    MessageType.None);

                DrawGeoTiffInspectionPanel();
            }

            if (heightmapSettings.UseSonarLogDepthData)
            {
                DrawSonarLogSettings();
            }

            heightmapSettings.Invert = EditorGUILayout.Toggle("Invert", heightmapSettings.Invert);
            heightmapSettings.FlipX = EditorGUILayout.Toggle("Flip X", heightmapSettings.FlipX);
            heightmapSettings.FlipY = EditorGUILayout.Toggle("Flip Y", heightmapSettings.FlipY);

            if (heightmapSettings.UseGeoTiffElevationData)
            {
                EditorGUILayout.HelpBox(
                    "GeoTIFF elevation mode treats sampled raster values as height data: Y = Height Offset + sample * Height Scale. Height processing remap, falloff, and terracing are skipped.",
                    MessageType.Info);
            }
            else if (heightmapSettings.UseSonarLogDepthData)
            {
                EditorGUILayout.HelpBox(
                    "Sonar log mode reads Cerulean Surveyor point packets and grids depth samples into the mesh. Use Refresh Preview to parse large files on demand.",
                    MessageType.Info);
            }
            else if (heightmapSettings.Heightmap == null)
            {
                EditorGUILayout.HelpBox(
                    "Leave the heightmap empty to generate a flat grid. Assign a texture to displace the grid using UV0 sampling.",
                    MessageType.None);
            }

            if (GUILayout.Button("Open Heightmap Editor"))
            {
                FPHeightmapEditorWindow.OpenWindow(meshDataAsset, heightmapSettings.Heightmap);
            }
        }

        private void DrawGeoTiffInspectionPanel()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("GeoTIFF Inspection", EditorStyles.boldLabel);

            FPMeshHeightmapSettings safeSettings = heightmapSettings.Sanitized();
            if (string.IsNullOrWhiteSpace(safeSettings.GeoTiffSourcePath))
            {
                EditorGUILayout.HelpBox("Assign a GeoTIFF heightmap or source path to inspect raster metadata.", MessageType.None);
                return;
            }

            if (GUILayout.Button("Inspect GeoTIFF"))
            {
                geoTiffInspectionDirty = true;
                effectiveGridDirty = true;
                TryGetGeoTiffInspection(out _, out _);
            }

            bool hasCurrentInspection = geoTiffInspectionRaster != null
                && string.Equals(geoTiffInspectionPath, safeSettings.GeoTiffSourcePath, System.StringComparison.OrdinalIgnoreCase);
            if (!hasCurrentInspection)
            {
                if (!string.IsNullOrWhiteSpace(geoTiffInspectionError))
                {
                    EditorGUILayout.HelpBox($"Could not inspect GeoTIFF: {geoTiffInspectionError}", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Inspection is manual for large files. Click Inspect GeoTIFF or Refresh Preview Mesh when ready.", MessageType.None);
                }

                return;
            }

            FPMeshGeoTiffRaster raster = geoTiffInspectionRaster;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Source", geoTiffInspectionPath);
                EditorGUILayout.TextField("Raster Size", $"{raster.Width} x {raster.Height} px");
                EditorGUILayout.TextField("Band", $"{raster.BitsPerSample}-bit {FormatSampleFormat(raster.SampleFormat)}, {raster.SamplesPerPixel} sample(s)/px");
                EditorGUILayout.TextField("Compression", FormatCompression(raster.Compression));
                EditorGUILayout.TextField("Layout", FormatRasterLayout(raster));
                EditorGUILayout.TextField("NoData", raster.NoDataValue.HasValue ? FormatFloat(raster.NoDataValue.Value) : "Not specified");
                EditorGUILayout.TextField("Values", FormatValueRange(raster));

                if (raster.TryGetPixelScale(out double pixelScaleX, out double pixelScaleY, out string pixelScaleError))
                {
                    EditorGUILayout.TextField("Pixel Scale", $"{pixelScaleX:0.########} x {pixelScaleY:0.########} source units/px");
                }
                else
                {
                    EditorGUILayout.TextField("Pixel Scale", $"Unavailable: {pixelScaleError}");
                }

                if (raster.TryGetRealSizeMeters(
                        safeSettings.GeoTiffCoordinateSystem,
                        safeSettings.GeoTiffHorizontalUnitsToMeters,
                        out float widthMeters,
                        out float lengthMeters,
                        out string realSizeError))
                {
                    EditorGUILayout.TextField("Real Size", $"{widthMeters:0.###} x {lengthMeters:0.###} m");
                }
                else
                {
                    EditorGUILayout.TextField("Real Size", $"Unavailable: {realSizeError}");
                }

                EditorGUILayout.TextField("GDAL Scale", raster.GdalScale.HasValue ? $"{raster.GdalScale.Value:0.########}" : "Not found");
                EditorGUILayout.TextField("GDAL Offset", raster.GdalOffset.HasValue ? $"{raster.GdalOffset.Value:0.########}" : "Not found");
            }

            if (LooksLikeImageIntensityRaster(raster))
            {
                EditorGUILayout.HelpBox(
                    "The value range looks like 8-bit image/intensity data. With Height Scale = 1, raw values up to 255 become up to 255 Unity meters.",
                    MessageType.Warning);
            }
            else if (raster.GdalScale.HasValue || raster.GdalOffset.HasValue)
            {
                EditorGUILayout.HelpBox(
                    "GDAL vertical scale/offset metadata was found. Current mesh generation still uses raw sample values multiplied by Height Scale.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No GDAL vertical scale/offset metadata was found. Treat Height Scale as the vertical conversion from raster samples to Unity meters.",
                MessageType.None);
            }
        }

        private void DrawSonarLogSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sonar Log Reference", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            heightmapSettings.SonarLogSourcePath = EditorGUILayout.TextField("Sonar Log File", heightmapSettings.SonarLogSourcePath);
            if (GUILayout.Button("Browse", GUILayout.Width(64f)))
            {
                string selectedPath = EditorUtility.OpenFilePanel("Select SonarView Log File", GetSonarLogBrowseFolder(), "svlog,svlz");
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    heightmapSettings.SonarLogSourcePath = ToProjectRelativePath(selectedPath);
                    sonarLogInspectionDirty = true;
                    effectiveGridDirty = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            heightmapSettings.SonarLogMeshMode = (FPMeshSonarLogMeshMode)EditorGUILayout.EnumPopup("Mesh Mode", heightmapSettings.SonarLogMeshMode);
            heightmapSettings.MatchGridToSonarLogBounds = EditorGUILayout.Toggle("Match Grid Log Bounds", heightmapSettings.MatchGridToSonarLogBounds);

            if (heightmapSettings.SonarLogMeshMode == FPMeshSonarLogMeshMode.GeospatialMosaic)
            {
                heightmapSettings.SonarLogNavSource = (FPMeshSonarLogNavSource)EditorGUILayout.EnumPopup("Nav Source", heightmapSettings.SonarLogNavSource);
                heightmapSettings.SonarLogHeadingSource = (FPMeshSonarLogHeadingSource)EditorGUILayout.EnumPopup("Heading Source", heightmapSettings.SonarLogHeadingSource);
                heightmapSettings.SonarLogOverlapMode = (FPMeshSonarLogOverlapMode)EditorGUILayout.EnumPopup("Overlap Mode", heightmapSettings.SonarLogOverlapMode);
                heightmapSettings.SonarLogCellSizeMeters = EditorGUILayout.FloatField("Cell Size Meters", heightmapSettings.SonarLogCellSizeMeters);
                heightmapSettings.SonarLogTimeOffsetMs = EditorGUILayout.IntField("Time Offset ms", heightmapSettings.SonarLogTimeOffsetMs);

                EditorGUILayout.HelpBox(
                    "Geospatial mosaic mode uses MAVLink position and heading to place each Omniscan profile sample into a local meter grid. LOCAL_POSITION_NED is usually the best first nav source in Unity.",
                    MessageType.None);
            }

            DrawOmniscan450SsSettings();
            using (new EditorGUI.DisabledScope(heightmapSettings.SonarLogMeshMode == FPMeshSonarLogMeshMode.GeospatialMosaic))
            {
                heightmapSettings.SonarLogForwardStepMeters = EditorGUILayout.FloatField("Ping Step Meters", heightmapSettings.SonarLogForwardStepMeters);
            }

            heightmapSettings.SonarLogRasterWidth = EditorGUILayout.IntField("Raster Width", heightmapSettings.SonarLogRasterWidth);
            heightmapSettings.SonarLogRasterLength = EditorGUILayout.IntField("Raster Length", heightmapSettings.SonarLogRasterLength);

            string sonarModeHelp = heightmapSettings.SonarLogMeshMode == FPMeshSonarLogMeshMode.GeospatialMosaic
                ? "Raster Width/Length still cap sampling density for non-geospatial sources; geospatial Omniscan profiles use Cell Size Meters to derive raster resolution from the MAVLink bounds."
                : "Waterfall mode maps cross-track offsets to grid width and ping number times Ping Step Meters to grid length.";
            EditorGUILayout.HelpBox(sonarModeHelp, MessageType.None);

            DrawSonarLogInspectionPanel();
        }

        private void DrawOmniscan450SsSettings()
        {
            if (heightmapSettings.SonarLogPingRateHz <= 0f)
            {
                heightmapSettings.SonarLogPingRateHz = Omniscan450SsDefaultPingRateHz;
            }

            if (heightmapSettings.SonarLogRangeMeters <= 0f)
            {
                heightmapSettings.SonarLogRangeMeters = Omniscan450SsDefaultRangeMeters;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Omniscan 450 SS", EditorStyles.boldLabel);
            heightmapSettings.SonarLogSurveySpeedMetersPerSecond = EditorGUILayout.FloatField("Survey Speed m/s", heightmapSettings.SonarLogSurveySpeedMetersPerSecond);
            heightmapSettings.SonarLogRangeMeters = EditorGUILayout.FloatField("Range Meters", heightmapSettings.SonarLogRangeMeters);
            heightmapSettings.SonarLogPingRateHz = EditorGUILayout.FloatField("Ping Rate Hz", heightmapSettings.SonarLogPingRateHz);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Estimate Rate From Range"))
            {
                heightmapSettings.SonarLogPingRateHz = EstimateOmniscan450SsPingRate(heightmapSettings.SonarLogRangeMeters);
            }

            if (GUILayout.Button("Calculate Ping Step"))
            {
                heightmapSettings.SonarLogForwardStepMeters = CalculateSonarLogForwardStepMeters(
                    heightmapSettings.SonarLogSurveySpeedMetersPerSecond,
                    heightmapSettings.SonarLogPingRateHz);
            }

            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Max Range", Omniscan450SsMaxRangeMeters);
                EditorGUILayout.IntField("Range Samples", Omniscan450SsRangeSamples);
            }

            EditorGUILayout.HelpBox(
                "Omniscan 450 SS helper: Ping Step Meters = Survey Speed / Ping Rate. The 450 SS is listed at 20 Hz up to 30 m range, with lower range-limited examples at longer ranges.",
                MessageType.None);
        }

        private void DrawSonarLogInspectionPanel()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sonar Log Inspection", EditorStyles.boldLabel);

            FPMeshHeightmapSettings safeSettings = heightmapSettings.Sanitized();
            if (string.IsNullOrWhiteSpace(safeSettings.SonarLogSourcePath))
            {
                EditorGUILayout.HelpBox("Assign a .svlog or .svlz file to inspect packet-derived point data.", MessageType.None);
                return;
            }

            if (GUILayout.Button("Inspect Sonar Log"))
            {
                sonarLogInspectionDirty = true;
                effectiveGridDirty = true;
                TryGetSonarLogInspection(out _, out _);
            }

            string inspectionSettingsKey = GetSonarLogInspectionSettingsKey(safeSettings);
            bool hasCurrentInspection = sonarLogInspectionRaster != null
                && !sonarLogInspectionDirty
                && string.Equals(sonarLogInspectionPath, safeSettings.SonarLogSourcePath, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(sonarLogInspectionSettingsKey, inspectionSettingsKey, System.StringComparison.Ordinal);
            if (!hasCurrentInspection)
            {
                if (!string.IsNullOrWhiteSpace(sonarLogInspectionError))
                {
                    EditorGUILayout.HelpBox($"Could not inspect sonar log: {sonarLogInspectionError}", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Inspection is manual for large logs. Click Inspect Sonar Log or Refresh Preview when ready.", MessageType.None);
                }

                return;
            }

            FPMeshSonarLogRaster raster = sonarLogInspectionRaster;
            using (new EditorGUI.DisabledScope(true))
            {
                string sourceKind = string.IsNullOrWhiteSpace(raster.SourceDataKind) ? "Sonar data" : raster.SourceDataKind;
                string sampleLabel = string.IsNullOrWhiteSpace(raster.SampleValueLabel) ? "Sample Values" : raster.SampleValueLabel;
                EditorGUILayout.TextField("Source", sonarLogInspectionPath);
                EditorGUILayout.TextField("Data Kind", sourceKind);
                EditorGUILayout.TextField("Packets", $"{raster.PacketCount:n0} total, {raster.PointPacketCount:n0} source packets");
                EditorGUILayout.TextField("Samples", $"{raster.PointCount:n0} samples, {raster.ValidCellCount:n0} gridded cells");
                if (safeSettings.SonarLogMeshMode == FPMeshSonarLogMeshMode.GeospatialMosaic)
                {
                    EditorGUILayout.TextField("MAVLink Nav", $"{raster.NavigationPositionCount:n0} positions, {raster.NavigationHeadingCount:n0} headings");
                }

                EditorGUILayout.TextField("Raster", $"{raster.Width} x {raster.Height}");
                string xLabel = safeSettings.SonarLogMeshMode == FPMeshSonarLogMeshMode.GeospatialMosaic ? "East / X" : "Cross Track";
                string zLabel = safeSettings.SonarLogMeshMode == FPMeshSonarLogMeshMode.GeospatialMosaic ? "North / Z" : "Along Track";
                EditorGUILayout.TextField(xLabel, $"{raster.MinCrossTrack:0.###} to {raster.MaxCrossTrack:0.###} m");
                EditorGUILayout.TextField(zLabel, $"{raster.MinAlongTrack:0.###} to {raster.MaxAlongTrack:0.###} m");
                EditorGUILayout.TextField(sampleLabel, $"{raster.MinDepth:0.###} to {raster.MaxDepth:0.###}");
                EditorGUILayout.TextField("Checksum Warnings", raster.InvalidChecksumCount.ToString("n0"));
            }

            if (string.Equals(raster.SampleValueLabel, "Power dB", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox(
                    "Omniscan profile logs contain acoustic intensity, not seafloor elevation. Mesh height is currently signal power multiplied by Height Scale.",
                    MessageType.Info);
            }

            if (raster.InvalidChecksumCount > 0)
            {
                EditorGUILayout.HelpBox("Some packets did not match the expected checksum. The parser still used supported point packets so partially valid logs can be inspected.", MessageType.Warning);
            }
        }

        private void DrawHeightProcessSettings()
        {
            EditorGUILayout.LabelField("Height Processing", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(heightmapSettings.UseGeoTiffElevationData || heightmapSettings.UseSonarLogDepthData))
            {
                heightProcessSettings.UseRemap = EditorGUILayout.Toggle("Use Remap", heightProcessSettings.UseRemap);
                using (new EditorGUI.DisabledScope(!heightProcessSettings.UseRemap))
                {
                    heightProcessSettings.RemapMin = EditorGUILayout.Slider("Remap Min", heightProcessSettings.RemapMin, 0f, 1f);
                    heightProcessSettings.RemapMax = EditorGUILayout.Slider("Remap Max", heightProcessSettings.RemapMax, 0f, 1f);
                }

                heightProcessSettings.EdgeFalloffMode = (FPMeshEdgeFalloffMode)EditorGUILayout.EnumPopup("Falloff Mode", heightProcessSettings.EdgeFalloffMode);
                using (new EditorGUI.DisabledScope(heightProcessSettings.EdgeFalloffMode == FPMeshEdgeFalloffMode.None))
                {
                    heightProcessSettings.EdgeFalloffStart = EditorGUILayout.Slider("Falloff Start", heightProcessSettings.EdgeFalloffStart, 0f, 1f);
                    heightProcessSettings.EdgeFalloffStrength = EditorGUILayout.Slider("Falloff Strength", heightProcessSettings.EdgeFalloffStrength, 0f, 8f);
                }

                heightProcessSettings.UseTerracing = EditorGUILayout.Toggle("Use Terracing", heightProcessSettings.UseTerracing);
                using (new EditorGUI.DisabledScope(!heightProcessSettings.UseTerracing))
                {
                    heightProcessSettings.TerraceSteps = EditorGUILayout.IntSlider("Terrace Steps", heightProcessSettings.TerraceSteps, 2, 64);
                }
            }

            string helpText = heightmapSettings.UseGeoTiffElevationData || heightmapSettings.UseSonarLogDepthData
                ? "Height processing is skipped while direct source data is enabled so sampled elevation/depth values stay in their original range."
                : "Use remap to isolate the useful height range, falloff to soften edges or create island shapes, and terracing for stepped surfaces.";
            EditorGUILayout.HelpBox(helpText, MessageType.None);
        }

        private void DrawActions()
        {
            EditorGUILayout.Space();

            Color originalColor = GUI.color;
            GUI.color = FP_Utility_Editor.OkayColor;

            if (GUILayout.Button("Refresh Preview Mesh", GUILayout.Height(28f)))
            {
                RefreshPreviewMesh();
            }

            if (GUILayout.Button("Create Scene Object", GUILayout.Height(32f)))
            {
                CreateSceneObject();
            }

            if (GUILayout.Button("Save Mesh Asset", GUILayout.Height(32f)))
            {
                SaveMeshAsset();
            }

            GUI.color = originalColor;
        }

        private void SyncSelectionDefaults()
        {
            if (Selection.activeTransform != null)
            {
                targetParent = Selection.activeTransform;
            }
        }

        private void LoadSettingsFromDataAsset()
        {
            if (meshDataAsset == null)
            {
                return;
            }

            gridSettings = meshDataAsset.GridSettings.Sanitized();
            heightmapSettings = meshDataAsset.HeightmapSettings.Sanitized();
            heightProcessSettings = meshDataAsset.HeightProcessSettings.Sanitized();
            effectiveGridDirty = true;
            previewDirty = true;
            geoTiffInspectionDirty = true;
            sonarLogInspectionDirty = true;
            sonarLogInspectionRaster = null;
            sonarLogInspectionError = string.Empty;
            Repaint();
        }

        private void SaveSettingsToDataAsset()
        {
            if (meshDataAsset == null)
            {
                return;
            }

            Undo.RecordObject(meshDataAsset, "Update Mesh Grid Data");
            meshDataAsset.Capture(gridSettings, heightmapSettings, heightProcessSettings);
            EditorUtility.SetDirty(meshDataAsset);
            AssetDatabase.SaveAssets();
        }

        private void RefreshPreviewMesh()
        {
            RefreshActiveSourceInspection();
            effectiveGridDirty = true;
            RebuildPreview(true);
            if (autoUpdatePreviewObject)
            {
                RefreshLastGeneratedPreview();
            }

            Repaint();
        }

        private void RefreshActiveSourceInspection()
        {
            FPMeshHeightmapSettings safeSettings = heightmapSettings.Sanitized();
            if (safeSettings.UseSonarLogDepthData && !string.IsNullOrWhiteSpace(safeSettings.SonarLogSourcePath))
            {
                sonarLogInspectionDirty = true;
                TryGetSonarLogInspection(out _, out _);
            }
            else if (safeSettings.UseGeoTiffElevationData && !string.IsNullOrWhiteSpace(safeSettings.GeoTiffSourcePath))
            {
                geoTiffInspectionDirty = true;
                TryGetGeoTiffInspection(out _, out _);
            }
        }

        private Mesh BuildMesh(bool forceSourceLoad = false)
        {
            if (forceSourceLoad)
            {
                RefreshActiveSourceInspection();
            }

            FPMeshGridBuildSettings buildSettings = forceSourceLoad
                ? FPMeshHeightmapUtility.ApplySourceRealScaleToGrid(gridSettings, heightmapSettings)
                : GetEffectiveGridSettings();
            Mesh mesh = FPMeshGridBuilder.Build(buildSettings);
            FPMeshHeightmapUtility.ApplyHeightmap(mesh, heightmapSettings, heightProcessSettings);
            return mesh;
        }

        private void DrawPreviewPanelContainer(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect previewRect = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f);

            if (Event.current.type == EventType.Repaint && previewDirty && ShouldAutoRebuildPreview())
            {
                RebuildPreview(true);
            }

            DrawMeshPreview(previewRect);
        }

        private void DrawMeshPreview(Rect rect)
        {
            HandlePreviewInput(rect);

            if (previewMesh == null)
            {
                GUI.Label(rect, "Click Refresh Preview Mesh to build the generated mesh.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewMaterials();

            if (Event.current.type != EventType.Repaint)
            {
                DrawPreviewDisplayControls(rect);
                DrawOrbitGizmo(rect);
                return;
            }

            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            previewUtility.camera.clearFlags = CameraClearFlags.Color;
            previewUtility.camera.fieldOfView = FPMeshPreviewEditorUtility.DefaultFieldOfView;
            previewUtility.lights[0].intensity = 1.1f;
            previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            previewUtility.lights[1].intensity = 0.55f;

            Bounds bounds = CalculatePreviewBounds();
            float distance = FPMeshPreviewEditorUtility.CalculateFitDistance(bounds, rect) * Mathf.Max(0.5f, previewZoom);
            Vector3 forward = previewRotation * Vector3.forward;
            previewUtility.camera.transform.position = bounds.center - (forward * distance);
            previewUtility.camera.transform.rotation = previewRotation;
            previewUtility.camera.orthographic = cameraProjection == FPMeshPreviewProjection.Orthographic;
            if (previewUtility.camera.orthographic)
            {
                previewUtility.camera.orthographicSize = FPMeshPreviewEditorUtility.CalculateOrthographicSize(bounds, rect, previewRotation) * Mathf.Max(0.5f, previewZoom);
            }

            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            previewUtility.camera.nearClipPlane = Mathf.Max(0.001f, distance - (radius * 2.4f));
            previewUtility.camera.farClipPlane = distance + (radius * 3.4f);

            if (showSurfaces)
            {
                DrawPreviewMesh(previewMesh, GetPreviewSurfaceMaterial());
            }

            previewUtility.camera.Render();
            Texture result = previewUtility.EndPreview();
            GUI.DrawTexture(rect, result, ScaleMode.StretchToFill, false);

            if (showEdges)
            {
                FPMeshPreviewEditorUtility.DrawMeshEdgeOverlay(previewUtility.camera, rect, previewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.EdgeOverlayColor, 1.5f);
            }

            if (showVertices)
            {
                FPMeshPreviewEditorUtility.DrawMeshVertexOverlay(previewUtility.camera, rect, previewMesh, Matrix4x4.identity, FPMeshPreviewEditorUtility.VertexOverlayColor, 2.5f);
            }

            DrawPreviewOverlay(rect);
            DrawPreviewDisplayControls(rect);
            FPMeshPreviewEditorUtility.DrawSceneOrientationGizmo(rect, previewUtility.camera, cameraProjection);
            DrawOrbitGizmo(rect);
        }

        private void RebuildPreview(bool forceSourceLoad)
        {
            previewDirty = false;
            CleanupPreviewMesh();
            previewMesh = BuildMesh(forceSourceLoad);
            if (previewMesh != null)
            {
                previewMesh.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private Bounds CalculatePreviewBounds()
        {
            if (previewMesh == null)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = previewMesh.bounds;
            if (bounds.size.sqrMagnitude <= 0.0000001f)
            {
                bounds.Expand(0.1f);
            }

            return bounds;
        }

        private void DrawPreviewMesh(Mesh mesh, Material material)
        {
            if (mesh == null || material == null)
            {
                return;
            }

            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int i = 0; i < subMeshCount; i++)
            {
                previewUtility.DrawMesh(mesh, Matrix4x4.identity, material, i);
            }
        }

        private void DrawPreviewOverlay(Rect rect)
        {
            Rect overlayRect = new Rect(rect.x + 8f, rect.y + 8f, 226f, 108f);
            GUI.Box(overlayRect, GUIContent.none, EditorStyles.helpBox);

            Rect lineRect = new Rect(overlayRect.x + 6f, overlayRect.y + 5f, overlayRect.width - 12f, 18f);
            GUI.Label(lineRect, $"Preview Vertices: {GetVertexCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Preview Triangles: {GetTriangleCount(previewMesh)}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            FPMeshGridBuildSettings effectiveSettings = GetEffectiveGridSettings();
            GUI.Label(lineRect, $"Grid: {effectiveSettings.XSegments} x {effectiveSettings.YSegments}", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Size: {effectiveSettings.Width:0.###} x {effectiveSettings.Length:0.###}m", EditorStyles.miniLabel);
            lineRect.y += 18f;
            GUI.Label(lineRect, $"Zoom: {previewZoom:0.##}x", EditorStyles.miniLabel);
        }

        private void DrawPreviewDisplayControls(Rect rect)
        {
            Rect controlsRect = GetPreviewDisplayControlsRect(rect);
            GUI.Box(controlsRect, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(controlsRect.x + 6f, controlsRect.y + 4f, controlsRect.width - 12f, 16f), "Display", EditorStyles.centeredGreyMiniLabel);

            const float gap = 3f;
            Rect buttonRect = new Rect(controlsRect.x + 6f, controlsRect.y + 24f, controlsRect.width - 12f, 22f);
            float buttonWidth = (buttonRect.width - (gap * 2f)) / 3f;

            bool nextShowSurfaces = GUI.Toggle(new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height), showSurfaces, "Surfaces", EditorStyles.miniButtonLeft);
            bool nextShowEdges = GUI.Toggle(new Rect(buttonRect.x + buttonWidth + gap, buttonRect.y, buttonWidth, buttonRect.height), showEdges, "Edges", EditorStyles.miniButtonMid);
            bool nextShowVertices = GUI.Toggle(new Rect(buttonRect.x + ((buttonWidth + gap) * 2f), buttonRect.y, buttonWidth, buttonRect.height), showVertices, "Vertices", EditorStyles.miniButtonRight);

            if (nextShowSurfaces != showSurfaces || nextShowEdges != showEdges || nextShowVertices != showVertices)
            {
                showSurfaces = nextShowSurfaces;
                showEdges = nextShowEdges;
                showVertices = nextShowVertices;
                Repaint();
            }
        }

        private static Rect GetPreviewDisplayControlsRect(Rect previewRect)
        {
            return new Rect(previewRect.x + 8f, previewRect.y + 104f, 226f, 54f);
        }

        private static int GetVertexCount(Mesh mesh)
        {
            return mesh == null ? 0 : mesh.vertexCount;
        }

        private static int GetTriangleCount(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            int triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                triangles += (int)(mesh.GetIndexCount(i) / 3);
            }

            return triangles;
        }

        private void DrawOrbitGizmo(Rect previewRect)
        {
            Rect gizmoRect = GetOrbitGizmoRect(previewRect);
            GUI.Box(gizmoRect, GUIContent.none, EditorStyles.helpBox);

            GUI.Label(new Rect(gizmoRect.x + 6f, gizmoRect.y + 4f, gizmoRect.width - 12f, 16f), "Orbit", EditorStyles.centeredGreyMiniLabel);

            DrawAxisHandle(GetOrbitAxisRect(gizmoRect, 0), "X", new Color(0.9f, 0.22f, 0.18f, 1f));
            DrawAxisHandle(GetOrbitAxisRect(gizmoRect, 1), "Y", new Color(0.3f, 0.78f, 0.28f, 1f));
            DrawAxisHandle(GetOrbitAxisRect(gizmoRect, 2), "Z", new Color(0.22f, 0.48f, 0.95f, 1f));

            Rect buttonsRect = new Rect(gizmoRect.x + 6f, gizmoRect.y + 88f, gizmoRect.width - 12f, 52f);
            DrawSnapButtons(buttonsRect);
        }

        private static void DrawAxisHandle(Rect rect, string label, Color color)
        {
            EditorGUI.DrawRect(rect, color * new Color(1f, 1f, 1f, 0.78f));
            GUI.Label(rect, label, EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawSnapButtons(Rect rect)
        {
            const float gap = 3f;
            float width = (rect.width - (gap * 2f)) / 3f;
            float height = 22f;

            if (GUI.Button(new Rect(rect.x, rect.y, width, height), "+X", EditorStyles.miniButtonLeft))
            {
                SetPreviewView(Vector3.right, Vector3.up);
            }

            if (GUI.Button(new Rect(rect.x + width + gap, rect.y, width, height), "+Y", EditorStyles.miniButtonMid))
            {
                SetPreviewView(Vector3.up, Vector3.back);
            }

            if (GUI.Button(new Rect(rect.x + ((width + gap) * 2f), rect.y, width, height), "+Z", EditorStyles.miniButtonRight))
            {
                SetPreviewView(Vector3.forward, Vector3.up);
            }

            float y = rect.y + height + 4f;
            if (GUI.Button(new Rect(rect.x, y, width, height), "-X", EditorStyles.miniButtonLeft))
            {
                SetPreviewView(Vector3.left, Vector3.up);
            }

            if (GUI.Button(new Rect(rect.x + width + gap, y, width, height), "-Y", EditorStyles.miniButtonMid))
            {
                SetPreviewView(Vector3.down, Vector3.forward);
            }

            if (GUI.Button(new Rect(rect.x + ((width + gap) * 2f), y, width, height), "-Z", EditorStyles.miniButtonRight))
            {
                SetPreviewView(Vector3.back, Vector3.up);
            }
        }

        private void SetPreviewView(Vector3 viewDirection, Vector3 up)
        {
            previewRotation = Quaternion.LookRotation(viewDirection, up);
            Repaint();
        }

        private static Rect GetOrbitGizmoRect(Rect previewRect)
        {
            return new Rect(previewRect.xMax - 128f, previewRect.y + 104f, 120f, 148f);
        }

        private static Rect GetOrbitAxisRect(Rect gizmoRect, int axis)
        {
            return new Rect(gizmoRect.x + 8f, gizmoRect.y + 24f + (axis * 20f), gizmoRect.width - 16f, 16f);
        }

        private static int GetOrbitAxisAtPosition(Rect previewRect, Vector2 mousePosition)
        {
            Rect gizmoRect = GetOrbitGizmoRect(previewRect);
            for (int i = 0; i < 3; i++)
            {
                if (GetOrbitAxisRect(gizmoRect, i).Contains(mousePosition))
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition))
            {
                return;
            }

            if (GetPreviewDisplayControlsRect(rect).Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.ScrollWheel)
            {
                float zoomFactor = Mathf.Exp(current.delta.y * 0.08f);
                previewZoom = Mathf.Clamp(previewZoom * zoomFactor, 0.5f, 12f);
                current.Use();
                Repaint();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                int axis = GetOrbitAxisAtPosition(rect, current.mousePosition);
                if (axis >= 0)
                {
                    activeOrbitAxis = axis;
                    current.Use();
                    return;
                }
            }

            if ((current.type == EventType.MouseUp || current.type == EventType.Ignore) && activeOrbitAxis >= 0)
            {
                activeOrbitAxis = -1;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && current.button == 0 && activeOrbitAxis >= 0)
            {
                float degrees = (current.delta.x + current.delta.y) * 0.5f;
                Vector3 axis = activeOrbitAxis == 0
                    ? Vector3.right
                    : activeOrbitAxis == 1
                        ? Vector3.up
                        : Vector3.forward;
                previewRotation = Quaternion.AngleAxis(degrees, axis) * previewRotation;
                current.Use();
                Repaint();
                return;
            }

            if (GetOrbitGizmoRect(rect).Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.MouseDrag && current.button == 0)
            {
                previewRotation = FPMeshPreviewEditorUtility.ApplyUnityStyleOrbit(previewRotation, current.delta, invertCameraOrbit);
                current.Use();
                Repaint();
            }
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.nearClipPlane = 0.01f;
            previewUtility.camera.farClipPlane = 5000f;
        }

        private void EnsurePreviewMaterials()
        {
            if (generatedPreviewMaterial != null)
            {
                return;
            }

            generatedPreviewMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            generatedPreviewMaterial.SetColor("_Color", FPMeshPreviewEditorUtility.PreviewMeshColor);
            generatedPreviewMaterial.SetInt("_Cull", (int)CullMode.Off);
            generatedPreviewMaterial.SetInt("_ZWrite", 1);
            generatedPreviewMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        }

        private Material GetPreviewSurfaceMaterial()
        {
            Texture2D surfaceTexture = ResolveSurfaceTexture();
            if (surfaceTexture == null)
            {
                return generatedPreviewMaterial;
            }

            if (texturedPreviewMaterial == null || texturedPreviewSourceMaterial != previewMaterial)
            {
                if (texturedPreviewMaterial != null)
                {
                    DestroyImmediate(texturedPreviewMaterial);
                }

                texturedPreviewMaterial = CreateSurfaceMaterialInstance(previewMaterial, HideFlags.HideAndDontSave, "FP Mesh Preview Surface");
                texturedPreviewSourceMaterial = previewMaterial;
            }

            ApplySurfaceTexture(texturedPreviewMaterial, surfaceTexture);
            return texturedPreviewMaterial != null ? texturedPreviewMaterial : generatedPreviewMaterial;
        }

        private Material GetSceneSurfaceMaterial()
        {
            Texture2D surfaceTexture = ResolveSurfaceTexture();
            if (surfaceTexture == null)
            {
                return previewMaterial;
            }

            if (generatedSceneMaterial == null || generatedSceneSourceMaterial != previewMaterial)
            {
                generatedSceneMaterial = CreateSurfaceMaterialInstance(previewMaterial, HideFlags.None, "FP Mesh Surface Material");
                generatedSceneSourceMaterial = previewMaterial;
            }

            ApplySurfaceTexture(generatedSceneMaterial, surfaceTexture);
            return generatedSceneMaterial != null ? generatedSceneMaterial : previewMaterial;
        }

        private Texture2D ResolveSurfaceTexture()
        {
            if (!mapHeightmapToSurface)
            {
                return null;
            }

            return surfaceTextureOverride != null ? surfaceTextureOverride : heightmapSettings.Heightmap;
        }

        private static Material CreateSurfaceMaterialInstance(Material sourceMaterial, HideFlags hideFlags, string materialName)
        {
            Material material = sourceMaterial != null
                ? new Material(sourceMaterial)
                : CreateDefaultSurfaceMaterial();

            if (material == null)
            {
                return null;
            }

            material.name = materialName;
            material.hideFlags = hideFlags;
            SetMaterialColor(material, Color.white);
            return material;
        }

        private static Material CreateDefaultSurfaceMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            return shader == null ? null : new Material(shader);
        }

        private static void ApplySurfaceTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            SetMaterialColor(material, Color.white);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private void CleanupPreviewMesh()
        {
            if (previewMesh == null)
            {
                return;
            }

            DestroyImmediate(previewMesh);
            previewMesh = null;
        }

        private void CreateSceneObject()
        {
            Mesh mesh = BuildMesh(true);
            GameObject go = new GameObject(mesh.name);
            Undo.RegisterCreatedObjectUndo(go, "Create FP Mesh Grid");

            if (targetParent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, targetParent.gameObject);
                go.transform.SetParent(targetParent, false);
            }

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = go.AddComponent<MeshRenderer>();
            Material sceneMaterial = GetSceneSurfaceMaterial();
            if (sceneMaterial != null)
            {
                meshRenderer.sharedMaterial = sceneMaterial;
            }

            if (addMeshCollider)
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            if (meshDataAsset != null)
            {
                Undo.RecordObject(meshDataAsset, "Update Mesh Grid Data");
                meshDataAsset.Capture(gridSettings, heightmapSettings, heightProcessSettings);
                EditorUtility.SetDirty(meshDataAsset);

                var gridInstance = go.AddComponent<FPMeshGridInstance>();
                gridInstance.DataAsset = meshDataAsset;
                gridInstance.PreviewMaterial = sceneMaterial;
                gridInstance.AddMeshCollider = addMeshCollider;
                gridInstance.AutoRegenerateInEditor = true;
            }

            lastGeneratedObject = go;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        private void SaveMeshAsset()
        {
            Mesh mesh = ResolveMeshForSaving();
            string defaultName = string.IsNullOrWhiteSpace(mesh.name) ? "FP_GridSurface" : mesh.name;
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Grid Mesh",
                defaultName,
                "asset",
                "Choose where to save the generated mesh asset.");

            if (string.IsNullOrWhiteSpace(path))
            {
                if (!EditorUtility.IsPersistent(mesh))
                {
                    DestroyImmediate(mesh);
                }
                return;
            }

            Mesh originalMeshReference = mesh;
            string result = FP_Utility_Editor.CreateAssetAt(mesh, path);
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (savedMesh != null)
            {
                ReplaceSceneMeshReferences(originalMeshReference, savedMesh);
            }

            Debug.Log($"[Mesh Generator] Mesh saved to {result}");
        }

        private Mesh ResolveMeshForSaving()
        {
            Mesh liveMesh = TryGetLiveGeneratedMesh();
            if (liveMesh != null)
            {
                return liveMesh;
            }

            return BuildMesh(true);
        }

        private Mesh TryGetLiveGeneratedMesh()
        {
            if (lastGeneratedObject == null)
            {
                return null;
            }

            var meshFilter = lastGeneratedObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return null;
            }

            if (EditorUtility.IsPersistent(meshFilter.sharedMesh))
            {
                return null;
            }

            return meshFilter.sharedMesh;
        }

        private void ReplaceSceneMeshReferences(Mesh originalMesh, Mesh savedMesh)
        {
            if (originalMesh == null || savedMesh == null)
            {
                return;
            }

            var meshFilters = Resources.FindObjectsOfTypeAll<MeshFilter>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || EditorUtility.IsPersistent(meshFilter))
                {
                    continue;
                }

                if (meshFilter.sharedMesh != originalMesh)
                {
                    continue;
                }

                Undo.RecordObject(meshFilter, "Assign Saved Grid Mesh");
                meshFilter.sharedMesh = savedMesh;
                EditorUtility.SetDirty(meshFilter);
            }

            var meshColliders = Resources.FindObjectsOfTypeAll<MeshCollider>();
            for (int i = 0; i < meshColliders.Length; i++)
            {
                MeshCollider meshCollider = meshColliders[i];
                if (meshCollider == null || EditorUtility.IsPersistent(meshCollider))
                {
                    continue;
                }

                if (meshCollider.sharedMesh != originalMesh)
                {
                    continue;
                }

                Undo.RecordObject(meshCollider, "Assign Saved Grid Mesh");
                meshCollider.sharedMesh = savedMesh;
                EditorUtility.SetDirty(meshCollider);
            }

            if (lastGeneratedObject != null)
            {
                EditorUtility.SetDirty(lastGeneratedObject);
            }
        }

        private void RefreshLastGeneratedPreview()
        {
            if (lastGeneratedObject == null)
            {
                return;
            }

            var gridInstance = lastGeneratedObject.GetComponent<FPMeshGridInstance>();
            if (gridInstance != null && meshDataAsset != null)
            {
                gridInstance.PreviewMaterial = GetSceneSurfaceMaterial();
                gridInstance.AddMeshCollider = addMeshCollider;
                gridInstance.AutoRegenerateInEditor = autoUpdatePreviewObject;
                EditorUtility.SetDirty(gridInstance);
                return;
            }

            var meshFilter = lastGeneratedObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            Mesh previousMesh = meshFilter.sharedMesh;
            Mesh nextMesh = BuildMesh(true);
            meshFilter.sharedMesh = nextMesh;

            var meshRenderer = lastGeneratedObject.GetComponent<MeshRenderer>();
            Material sceneMaterial = GetSceneSurfaceMaterial();
            if (meshRenderer != null && sceneMaterial != null)
            {
                meshRenderer.sharedMaterial = sceneMaterial;
                EditorUtility.SetDirty(meshRenderer);
            }

            var meshCollider = lastGeneratedObject.GetComponent<MeshCollider>();
            if (addMeshCollider)
            {
                if (meshCollider == null)
                {
                    meshCollider = lastGeneratedObject.AddComponent<MeshCollider>();
                }

                meshCollider.sharedMesh = nextMesh;
                EditorUtility.SetDirty(meshCollider);
            }
            else if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
                EditorUtility.SetDirty(meshCollider);
            }

            if (previousMesh != null && !EditorUtility.IsPersistent(previousMesh))
            {
                DestroyImmediate(previousMesh);
            }

            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(lastGeneratedObject);
        }

        private FPMeshGridBuildSettings GetEffectiveGridSettings()
        {
            if (!effectiveGridDirty)
            {
                return effectiveGridSettings;
            }

            FPMeshHeightmapSettings safeHeightmapSettings = heightmapSettings.Sanitized();
            effectiveGridSettings = gridSettings.Sanitized();
            if (safeHeightmapSettings.UseSonarLogDepthData)
            {
                if (safeHeightmapSettings.MatchGridToSonarLogBounds
                    && sonarLogInspectionRaster != null
                    && !sonarLogInspectionDirty
                    && string.Equals(sonarLogInspectionPath, safeHeightmapSettings.SonarLogSourcePath, System.StringComparison.OrdinalIgnoreCase)
                    && string.Equals(sonarLogInspectionSettingsKey, GetSonarLogInspectionSettingsKey(safeHeightmapSettings), System.StringComparison.Ordinal)
                    && sonarLogInspectionRaster.TryGetRealSizeMeters(out float sonarWidth, out float sonarLength))
                {
                    effectiveGridSettings.Width = sonarWidth;
                    effectiveGridSettings.Length = sonarLength;
                    effectiveGridSettings = effectiveGridSettings.Sanitized();
                }
            }
            else if (safeHeightmapSettings.UseGeoTiffElevationData)
            {
                if (safeHeightmapSettings.MatchGridToGeoTiffScale
                    && geoTiffInspectionRaster != null
                    && string.Equals(geoTiffInspectionPath, safeHeightmapSettings.GeoTiffSourcePath, System.StringComparison.OrdinalIgnoreCase)
                    && geoTiffInspectionRaster.TryGetRealSizeMeters(
                        safeHeightmapSettings.GeoTiffCoordinateSystem,
                        safeHeightmapSettings.GeoTiffHorizontalUnitsToMeters,
                        out float geoTiffWidth,
                        out float geoTiffLength,
                        out _))
                {
                    effectiveGridSettings.Width = geoTiffWidth;
                    effectiveGridSettings.Length = geoTiffLength;
                    effectiveGridSettings = effectiveGridSettings.Sanitized();
                }
            }

            effectiveGridDirty = false;
            return effectiveGridSettings;
        }

        private bool IsSourceRealScaleRequested()
        {
            FPMeshHeightmapSettings safeHeightmapSettings = heightmapSettings.Sanitized();
            return (safeHeightmapSettings.UseGeoTiffElevationData
                    && safeHeightmapSettings.MatchGridToGeoTiffScale
                    && !string.IsNullOrWhiteSpace(safeHeightmapSettings.GeoTiffSourcePath))
                || (safeHeightmapSettings.UseSonarLogDepthData
                    && safeHeightmapSettings.MatchGridToSonarLogBounds
                    && !string.IsNullOrWhiteSpace(safeHeightmapSettings.SonarLogSourcePath));
        }

        private void SyncGridDimensionsToEffectiveBounds(FPMeshGridBuildSettings effectiveSettings)
        {
            if (!IsSourceRealScaleRequested())
            {
                return;
            }

            if (Mathf.Approximately(gridSettings.Width, effectiveSettings.Width)
                && Mathf.Approximately(gridSettings.Length, effectiveSettings.Length))
            {
                return;
            }

            gridSettings.Width = effectiveSettings.Width;
            gridSettings.Length = effectiveSettings.Length;
            previewDirty = true;
        }

        private bool ShouldAutoRebuildPreview()
        {
            return autoRebuildPreview && !IsDirectSourceModeActive();
        }

        private bool IsDirectSourceModeActive()
        {
            FPMeshHeightmapSettings safeHeightmapSettings = heightmapSettings.Sanitized();
            return safeHeightmapSettings.UseGeoTiffElevationData || safeHeightmapSettings.UseSonarLogDepthData;
        }

        private bool TryGetGeoTiffInspection(out FPMeshGeoTiffRaster raster, out string error)
        {
            FPMeshHeightmapSettings safeSettings = heightmapSettings.Sanitized();
            string sourcePath = safeSettings.GeoTiffSourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                raster = null;
                error = "No GeoTIFF source path was assigned.";
                return false;
            }

            if (!geoTiffInspectionDirty
                && string.Equals(geoTiffInspectionPath, sourcePath, System.StringComparison.OrdinalIgnoreCase))
            {
                raster = geoTiffInspectionRaster;
                error = geoTiffInspectionError;
                return raster != null;
            }

            geoTiffInspectionDirty = false;
            geoTiffInspectionPath = sourcePath;
            geoTiffInspectionRaster = null;
            geoTiffInspectionError = string.Empty;

            if (!FPMeshGeoTiffRaster.TryLoad(sourcePath, out geoTiffInspectionRaster, out geoTiffInspectionError))
            {
                raster = null;
                error = geoTiffInspectionError;
                return false;
            }

            raster = geoTiffInspectionRaster;
            error = string.Empty;
            return true;
        }

        private bool TryGetSonarLogInspection(out FPMeshSonarLogRaster raster, out string error)
        {
            FPMeshHeightmapSettings safeSettings = heightmapSettings.Sanitized();
            string sourcePath = safeSettings.SonarLogSourcePath;
            string settingsKey = GetSonarLogInspectionSettingsKey(safeSettings);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                raster = null;
                error = "No sonar log source path was assigned.";
                return false;
            }

            if (!sonarLogInspectionDirty
                && string.Equals(sonarLogInspectionPath, sourcePath, System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(sonarLogInspectionSettingsKey, settingsKey, System.StringComparison.Ordinal))
            {
                raster = sonarLogInspectionRaster;
                error = sonarLogInspectionError;
                return raster != null;
            }

            sonarLogInspectionDirty = false;
            sonarLogInspectionPath = sourcePath;
            sonarLogInspectionSettingsKey = settingsKey;
            sonarLogInspectionRaster = null;
            sonarLogInspectionError = string.Empty;

            if (!FPMeshSonarLogRaster.TryLoad(sourcePath, safeSettings, out sonarLogInspectionRaster, out sonarLogInspectionError))
            {
                raster = null;
                error = sonarLogInspectionError;
                effectiveGridDirty = true;
                return false;
            }

            raster = sonarLogInspectionRaster;
            error = string.Empty;
            effectiveGridDirty = true;
            return true;
        }

        private static string GetSonarLogInspectionSettingsKey(FPMeshHeightmapSettings settings)
        {
            return string.Join(
                "|",
                settings.SonarLogMeshMode,
                settings.SonarLogNavSource,
                settings.SonarLogHeadingSource,
                settings.SonarLogOverlapMode,
                settings.MatchGridToSonarLogBounds,
                settings.SonarLogSurveySpeedMetersPerSecond.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                settings.SonarLogPingRateHz.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                settings.SonarLogRangeMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                settings.SonarLogForwardStepMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                settings.SonarLogCellSizeMeters.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                settings.SonarLogTimeOffsetMs,
                settings.SonarLogRasterWidth,
                settings.SonarLogRasterLength);
        }

        private static float CalculateSonarLogForwardStepMeters(float surveySpeedMetersPerSecond, float pingRateHz)
        {
            float safeSpeed = Mathf.Max(0f, surveySpeedMetersPerSecond);
            float safePingRate = Mathf.Max(0.000001f, pingRateHz);
            return Mathf.Max(0.000001f, safeSpeed / safePingRate);
        }

        private static float EstimateOmniscan450SsPingRate(float rangeMeters)
        {
            float safeRange = Mathf.Max(0f, rangeMeters);
            if (safeRange <= 30f)
            {
                return 20f;
            }

            if (safeRange <= 50f)
            {
                return Mathf.Lerp(20f, 10f, (safeRange - 30f) / 20f);
            }

            if (safeRange <= 140f)
            {
                return Mathf.Lerp(10f, 5f, (safeRange - 50f) / 90f);
            }

            return 5f;
        }

        private static string FormatSampleFormat(ushort sampleFormat)
        {
            switch (sampleFormat)
            {
                case 1:
                    return "unsigned integer";
                case 2:
                    return "signed integer";
                case 3:
                    return "floating point";
                default:
                    return $"sample format {sampleFormat}";
            }
        }

        private static string FormatCompression(ushort compression)
        {
            switch (compression)
            {
                case 1:
                    return "None";
                case 5:
                    return "LZW";
                default:
                    return $"Compression {compression}";
            }
        }

        private static string FormatRasterLayout(FPMeshGeoTiffRaster raster)
        {
            if (raster == null)
            {
                return "Unknown";
            }

            return raster.IsTiled
                ? $"Tiled {raster.TileWidth} x {raster.TileLength}"
                : $"Strips, {raster.RowsPerStrip} rows/strip";
        }

        private static string FormatValueRange(FPMeshGeoTiffRaster raster)
        {
            if (raster == null || raster.ValidSampleCount <= 0)
            {
                return "No finite samples";
            }

            return $"{FormatFloat(raster.MinValue)} to {FormatFloat(raster.MaxValue)} ({raster.ValidSampleCount:n0} valid, {raster.NoDataSampleCount:n0} no-data)";
        }

        private static string FormatFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? "NaN"
                : value.ToString("0.###");
        }

        private static bool LooksLikeImageIntensityRaster(FPMeshGeoTiffRaster raster)
        {
            return raster != null
                && raster.SampleFormat == 1
                && raster.BitsPerSample <= 8
                && raster.ValidSampleCount > 0
                && raster.MinValue >= 0f
                && raster.MaxValue <= 255f
                && !raster.GdalScale.HasValue
                && !raster.GdalOffset.HasValue;
        }

        private string GetGeoTiffBrowseFolder()
        {
            if (!string.IsNullOrWhiteSpace(heightmapSettings.GeoTiffSourcePath))
            {
                string resolvedPath = ResolveEditorPath(heightmapSettings.GeoTiffSourcePath);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    string directory = Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    {
                        return directory;
                    }
                }
            }

            return Application.dataPath;
        }

        private string GetSonarLogBrowseFolder()
        {
            if (!string.IsNullOrWhiteSpace(heightmapSettings.SonarLogSourcePath))
            {
                string resolvedPath = ResolveEditorPath(heightmapSettings.SonarLogSourcePath);
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    string directory = Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    {
                        return directory;
                    }
                }
            }

            return Application.dataPath;
        }

        private void SyncGeoTiffSourcePathFromHeightmap(Texture2D previousHeightmap)
        {
            if (heightmapSettings.Heightmap == null)
            {
                return;
            }

            if (heightmapSettings.Heightmap == previousHeightmap && !string.IsNullOrWhiteSpace(heightmapSettings.GeoTiffSourcePath))
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(heightmapSettings.Heightmap);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                heightmapSettings.GeoTiffSourcePath = assetPath;
            }
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return absolutePath;
            }

            string normalizedAbsolute = Path.GetFullPath(absolutePath);
            string normalizedRoot = Path.GetFullPath(projectRoot);
            if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                normalizedRoot += Path.DirectorySeparatorChar;
            }

            if (!normalizedAbsolute.StartsWith(normalizedRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath;
            }

            return normalizedAbsolute.Substring(normalizedRoot.Length).Replace('\\', '/');
        }

        private static string ResolveEditorPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                return path;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return string.Empty;
            }

            string projectRelative = Path.Combine(projectRoot, path);
            return File.Exists(projectRelative) ? projectRelative : string.Empty;
        }
    }
}
