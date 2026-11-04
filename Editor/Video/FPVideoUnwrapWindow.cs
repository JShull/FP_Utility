// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor.Video
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;

    public sealed class FPVideoUnwrapWindow : EditorWindow
    {
        [SerializeField] private string sourcePath = "";
        [SerializeField] private string exportPath = "";
        [SerializeField] private FPVideoUnwrapSettings settings = new FPVideoUnwrapSettings();
        [SerializeField] private double playhead, exportIn, exportOut, viewStart, viewEnd;
        [SerializeField] private bool includeAudio = true, lensExpanded, sourceVisible = true, colorExpanded;
        [SerializeField] private bool fastAudioTrim = true;
        [SerializeField] private float previewSplit = .5f;
        [SerializeField] private int previewMaxWidth = 2048;
        [SerializeField] private bool gpuPreview;
        [SerializeField] private FPVideoCalibrationView calibrationView;
        [SerializeField] private float overlayMix = .5f, differenceGain = 4, imageZoom = 1;
        [SerializeField] private Vector2 imagePan;
        [SerializeField] private bool calibrationGrid;
        private Vector2 parameterScroll;
        private FPVideoNativeSource source;
        private FPVideoUnwrapPreview preview;
        private FPVideoUnwrapRenderer renderer;
        private FPVideoUnwrapExport export;
        private bool playing, dirty = true, loadPending, reloadRange;
        private double playStartTime, playStartOffset;
        private double nextExportRepaint;
        private string error, notice = "Choose an external LRV or MP4 file to begin.";
        private int timelineDrag; // 1 playhead, 2 In, 3 Out, 4 pan
        private double dragStart, dragViewStart, dragViewEnd;
        private Rect timelineRect;
        private bool Busy => export != null && !export.Finished;
        private double Duration => source?.Info.Duration ?? 0;
        private static readonly Color PlayheadColor = new Color(1, .72f, .25f, .95f);
        private static readonly Color InColor = new Color(.25f, .75f, 1, .95f);

        [MenuItem("FuzzPhyte/Utility/Video/FP Video Unwrap", priority = FP_UtilityData.MENU_UTILITY_VIDEO + 2)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPVideoUnwrapWindow>("FP Video Unwrap"); window.minSize = new Vector2(960, 620);
        }
        private void OnEnable()
        {
            minSize = new Vector2(960, 620); settings ??= new FPVideoUnwrapSettings();
            if (settings.videoBitrateMbps == 0) settings.videoBitrateMbps = 40;
            if (previewMaxWidth != 1024 && previewMaxWidth != 2048 && previewMaxWidth != 4096 && previewMaxWidth != 8192) previewMaxWidth = 2048;
            loadPending = !string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath);
            EditorApplication.update += Tick; Undo.undoRedoPerformed += UndoChanged;
        }
        private void OnDisable()
        {
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= UndoChanged;
            export?.Dispose(); export = null; ReleaseSource();
        }
        private void UndoChanged() { dirty = true; playing = false; Repaint(); }
        private void ReleaseSource()
        {
            playing = false; preview?.Dispose(); preview = null; renderer?.Dispose(); renderer = null; source?.Dispose(); source = null;
        }
        private void LoadSource()
        {
            ReleaseSource(); error = null;
            try
            {
                if (!FPVideoNativeDecoder.Available) throw new NotSupportedException("Native Editor decoder unavailable in this Unity version.");
                source = new FPVideoNativeSource(sourcePath);
                if (reloadRange || exportOut <= 0 || exportOut > Duration) { exportIn = playhead = viewStart = 0; exportOut = viewEnd = Duration; }
                reloadRange = false; dirty = true; notice = "Preparing native source…";
            }
            catch (Exception e) { error = e.Message; ReleaseSource(); }
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (loadPending && !Busy) { loadPending = false; LoadSource(); }
                if (Busy)
                {
                    export.Tick();
                    if (export.Finished)
                    {
                        if (!export.Succeeded) error = export.Error;
                        else { notice = "Saved " + export.OutputPath + " • SHA-256 " + export.Hash; AssetDatabase.Refresh(); }
                    }
                    if (export.Finished || now >= nextExportRepaint)
                    { if (!export.Finished) notice = export.Status; nextExportRepaint = now + .1; Repaint(); }
                    return;
                }
                if (source == null) return;
                if (!source.Ready.IsCompleted) { notice = $"Staging temporary MP4 copy… {source.Progress:P0}"; Repaint(); return; }
                source.Ready.GetAwaiter().GetResult();
                if (preview == null) { preview = new FPVideoUnwrapPreview(source,previewMaxWidth,gpuPreview); renderer = new FPVideoUnwrapRenderer(); notice = "Ready • native Editor decode"; }
                bool wasPlaying = playing;
                if (playing && preview.UsesGpu && preview.Texture == null) { playStartOffset=playhead;playStartTime=now; }
                if (playing) { playhead = Math.Min(exportOut, playStartOffset + now - playStartTime); if (playhead >= exportOut) playing = false; }
                bool changed = preview.Request(Math.Min(playhead, Math.Max(0, Duration - 1 / source.Info.FrameRate)), now, timelineDrag != 0, wasPlaying);
                if (changed) dirty = true;
                if (changed && preview.UsesGpu && wasPlaying)
                { playhead=Math.Min(exportOut,preview.PresentedSeconds);playStartOffset=playhead;playStartTime=now; }
                bool rendered = dirty && preview.Texture != null;
                if (rendered)
                {
                    settings.Validate(); int width = Math.Min(Math.Min(4096, (int)(1024 * imageZoom)), settings.width); int height = Math.Max(1, (int)Math.Round((double)width * settings.height / settings.width));
                    renderer.Render(preview.Texture, settings, width, height,
                        settings.sourceProjection == FPVideoSourceProjection.DualFisheye ? calibrationView : FPVideoCalibrationView.Composite, overlayMix, differenceGain,
                        preview.PresentedSeconds,1 / source.Info.FrameRate); dirty = false;
                }
                if (changed || rendered || playing || dirty || timelineDrag != 0) Repaint();
            }
            catch (Exception e) { error = e.Message; playing = false; loadPending = false; Repaint(); }
        }
        private void OnGUI()
        {
            HandleShortcuts();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(352), GUILayout.ExpandHeight(true)))
                {
                    parameterScroll = EditorGUILayout.BeginScrollView(parameterScroll);
                    DrawParameters(); EditorGUILayout.EndScrollView(); DrawActions();
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
                {
                    DrawTransport(); DrawCalibrationToolbar(); DrawPreviews(); DrawTimeline(); DrawStatus();
                }
            }
            HandleDrop(new Rect(0, 0, position.width, position.height));
        }
        private void DrawParameters()
        {
            GUILayout.Label("Source", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Busy))
            {
                sourcePath = EditorGUILayout.TextField("File", sourcePath);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Browse…"))
                    {
                        string selected = EditorUtility.OpenFilePanel("Open 360 video", Path.GetDirectoryName(sourcePath) ?? "", "");
                        if (!string.IsNullOrEmpty(selected)) { sourcePath = selected; reloadRange = loadPending = true; }
                    }
                    if (GUILayout.Button("Load / Reload")) { reloadRange = loadPending = true; }
                }
            }
            if (source != null)
            {
                EditorGUILayout.LabelField(Path.GetFileName(source.OriginalPath), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField($"{source.Info.Width} × {source.Info.Height} • {source.Info.FrameRate:F3} fps • {Duration:F2} s", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(source.Info.HasAudio ? $"Audio: {source.Info.AudioChannels} ch / {source.Info.AudioSampleRate} Hz" : "No audio track", EditorStyles.miniLabel);
                if (source.SecondDecodePath != null) EditorGUILayout.LabelField($"Two native lens tracks • {source.Info.Width/2} × {source.Info.Height} each",EditorStyles.miniLabel);
            }
            EditorGUI.BeginChangeCheck();
            int nextPreviewWidth=EditorGUILayout.IntPopup(new GUIContent("Preview detail","Decode a smaller preview to reduce CPU conversion and GPU upload cost. Full source restores native pixels. Export always decodes full resolution."),
                previewMaxWidth,new[]{new GUIContent("1024 — fastest"),new GUIContent("2048 — balanced"),new GUIContent("4096 — detailed"),new GUIContent("Full source")},new[]{1024,2048,4096,8192});
            bool nextGpu=EditorGUILayout.Toggle(new GUIContent("GPU texture preview","Experimental Unity VideoPlayer backend. Frames stay in render textures; no managed pixel upload/readback. Native hardware-decoder selection depends on Unity, OS and driver. Export retains the exact Editor decoder."),gpuPreview);
            if (EditorGUI.EndChangeCheck())
            {
                previewMaxWidth=nextPreviewWidth;gpuPreview=nextGpu;playing=false;preview?.Dispose();preview=null;renderer?.Dispose();renderer=null;dirty=true;
            }
            if (gpuPreview) EditorGUILayout.HelpBox("Experimental native GPU textures with matched lens frames and asynchronous seeking. Queues Editor player-loop updates. Export uses the existing full-resolution decoder. Turn this off to return to CPU preview.",MessageType.Info);
            EditorGUILayout.Space(6); GUILayout.Label("Projection", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(Busy))
            {
                var serialized = new SerializedObject(this); serialized.Update();
                var data = serialized.FindProperty("settings");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(data.FindPropertyRelative("sourceProjection"), new GUIContent("Source layout"));
                EditorGUILayout.PropertyField(data.FindPropertyRelative("projection"), new GUIContent("Output view"));
                EditorGUILayout.PropertyField(data.FindPropertyRelative("width")); EditorGUILayout.PropertyField(data.FindPropertyRelative("height"));
                EditorGUILayout.PropertyField(data.FindPropertyRelative("videoBitrateMbps"), new GUIContent("Export bitrate (Mbps)"));
                if ((FPVideoProjection)data.FindPropertyRelative("projection").enumValueIndex == FPVideoProjection.Perspective)
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("horizontalFov"), new GUIContent("Horizontal FOV"));
                if ((FPVideoProjection)data.FindPropertyRelative("projection").enumValueIndex == FPVideoProjection.Cylindrical)
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("verticalFov"), new GUIContent("Vertical FOV"));
                EditorGUILayout.PropertyField(data.FindPropertyRelative("rotation"), new GUIContent("View rotation"));
                EditorGUILayout.PropertyField(data.FindPropertyRelative("flipX")); EditorGUILayout.PropertyField(data.FindPropertyRelative("flipY"));
                colorExpanded = EditorGUILayout.Foldout(colorExpanded,"Color tuning",true);
                if (colorExpanded)
                {
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("colorTuning"),new GUIContent("Apply color tuning"));
                    using (new EditorGUI.DisabledScope(!data.FindPropertyRelative("colorTuning").boolValue))
                    {
                        EditorGUILayout.Slider(data.FindPropertyRelative("colorExposure"),-4,4,new GUIContent("Exposure (stops)"));
                        EditorGUILayout.Slider(data.FindPropertyRelative("colorContrast"),0,2,new GUIContent("Contrast"));
                        EditorGUILayout.Slider(data.FindPropertyRelative("colorSaturation"),0,2,new GUIContent("Saturation"));
                        EditorGUILayout.Slider(data.FindPropertyRelative("colorTemperature"),-1,1,new GUIContent("Temperature","Relative cool/warm RGB balance, not measured Kelvin."));
                        EditorGUILayout.Slider(data.FindPropertyRelative("colorTint"),-1,1,new GUIContent("Tint","Relative green/magenta RGB balance."));
                        var curve=data.FindPropertyRelative("colorCurve");
                        curve.enumValueIndex=EditorGUILayout.Popup("Tone curve",curve.enumValueIndex,
                            new[]{"Neutral","Soft film","Flat Log113 (creative)","Custom gamma","Vivid","Shadow detail","Vivid + shadow detail"});
                        if (curve.enumValueIndex != 0)
                        {
                            EditorGUILayout.Slider(data.FindPropertyRelative("colorCurveStrength"),0,1,new GUIContent("Curve strength"));
                            if (curve.enumValueIndex == (int)FPVideoToneCurve.CustomGamma)
                                EditorGUILayout.Slider(data.FindPropertyRelative("colorGamma"),.5f,2.5f,new GUIContent("Midtone gamma","1 is neutral; above 1 lifts midtones, below 1 darkens them."));
                            EditorGUILayout.HelpBox("Creative output look only. Flat Log113 uses the documented log-base-113 equation; it does not decode Protune/CineStyle or add captured dynamic range.",MessageType.Info);
                            if (curve.enumValueIndex >= (int)FPVideoToneCurve.ShadowDetail)
                                EditorGUILayout.HelpBox("Brightens existing shadow detail while largely retaining highlights and black. It can reveal noise; clipped shadow information cannot be recovered.",MessageType.Info);
                        }
                        if (GUILayout.Button("Reset color tuning"))
                        {
                            data.FindPropertyRelative("colorExposure").floatValue = 0;
                            data.FindPropertyRelative("colorTemperature").floatValue = 0;
                            data.FindPropertyRelative("colorTint").floatValue = 0;
                            data.FindPropertyRelative("colorContrast").floatValue = 1;
                            data.FindPropertyRelative("colorSaturation").floatValue = 1;
                            curve.enumValueIndex = 0;
                            data.FindPropertyRelative("colorCurveStrength").floatValue = 1;
                            data.FindPropertyRelative("colorGamma").floatValue = 1;
                            GUI.changed = true;
                        }
                    }
                    EditorGUILayout.HelpBox("Applied to Composite preview and export after stitching. Source and calibration diagnostics stay ungraded. SDR output; no automatic camera Log/HDR conversion. Lens calibration files preserve your current color tuning.",MessageType.Info);
                }
                lensExpanded = EditorGUILayout.Foldout(lensExpanded, "Lens calibration", true);
                if (lensExpanded && settings.sourceProjection == FPVideoSourceProjection.DualFisheye)
                {
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("calibrationName"), new GUIContent("Calibration name"));
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("lensFov"), new GUIContent("A / shared full FOV"));
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("blendDegrees"), new GUIContent("Legacy edge feather (°)"));
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("leftCenter"), new GUIContent("Lens A center")); EditorGUILayout.PropertyField(data.FindPropertyRelative("rightCenter"), new GUIContent("Lens B center"));
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("lensRadius"), new GUIContent("A / shared radius"));
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("leftRotation"), new GUIContent("Lens A rotation")); EditorGUILayout.PropertyField(data.FindPropertyRelative("rightRotation"), new GUIContent("Lens B rotation"));
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("independentRightLens"), new GUIContent("Independent lens B"));
                    if (data.FindPropertyRelative("independentRightLens").boolValue)
                    {
                        EditorGUILayout.PropertyField(data.FindPropertyRelative("rightLensFov"), new GUIContent("Lens B full FOV"));
                        EditorGUILayout.PropertyField(data.FindPropertyRelative("rightLensRadius"), new GUIContent("Lens B radius"));
                    }
                    EditorGUILayout.Space(4); GUILayout.Label("Seam placement", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(data.FindPropertyRelative("useDirectionalSeam"), new GUIContent("Directional seam"));
                    if (data.FindPropertyRelative("useDirectionalSeam").boolValue)
                    {
                        EditorGUILayout.Slider(data.FindPropertyRelative("seamYaw"), -180, 180, new GUIContent("Seam yaw (°)"));
                        EditorGUILayout.Slider(data.FindPropertyRelative("seamWidth"), 0, 60, new GUIContent("Blend width (°)"));
                    }
                    EditorGUILayout.HelpBox("Freeze a frame. Compare Lens A/B, then use Overlap Overlay or Difference to align stationary features. Directional seam controls mixing, not geometry. Close objects can retain parallax after alignment.", MessageType.Info);
                    EditorGUILayout.Space(4); GUILayout.Label("GPU seam flow (experimental)", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(!FPVideoFarneback.Supported && !data.FindPropertyRelative("opticalFlow").boolValue))
                        EditorGUILayout.PropertyField(data.FindPropertyRelative("opticalFlow"), new GUIContent("Farnebäck correction"));
                    if (data.FindPropertyRelative("opticalFlow").boolValue)
                    {
                        data.FindPropertyRelative("useDirectionalSeam").boolValue = true;
                        var resolution = data.FindPropertyRelative("flowResolution");
                        resolution.intValue = EditorGUILayout.IntPopup("Flow resolution", resolution.intValue, new[] {"128", "256", "512", "1024"}, new[] {128,256,512,1024});
                        EditorGUILayout.IntSlider(data.FindPropertyRelative("flowIterations"),1,8,new GUIContent("Iterations"));
                        EditorGUILayout.Slider(data.FindPropertyRelative("flowMaxWarpDegrees"),.1f,10,new GUIContent("Max warp (°)"));
                        EditorGUILayout.Slider(data.FindPropertyRelative("flowStrength"),0,1,new GUIContent("Warp strength"));
                        EditorGUILayout.Slider(data.FindPropertyRelative("flowMinConfidence"),.05f,.95f,new GUIContent("Min confidence"));
                        EditorGUILayout.PropertyField(data.FindPropertyRelative("flowTemporalSmoothing"),new GUIContent("Temporal smoothing"));
                        if (data.FindPropertyRelative("flowTemporalSmoothing").boolValue)
                        {
                            EditorGUILayout.Slider(data.FindPropertyRelative("flowSmoothingSeconds"),.01f,.5f,new GUIContent("Response time (s)","Higher values steady the seam but increase lag. Start at 0.08 seconds."));
                            EditorGUILayout.Slider(data.FindPropertyRelative("flowTemporalRejectDegrees"),.1f,5,new GUIContent("Motion reset (°)","Reject history when vectors disagree by this angle. Lower values follow motion sooner."));
                        }
                        EditorGUILayout.HelpBox("Reference A stays fixed; uncertain matches retain the ordinary seam blend. Temporal smoothing filters reliable vectors on consecutive frames, resets on seeks and calibration changes, and rejects changing imagery. It can lag moving edges. Inspect a short moving export before a full clip.",MessageType.Warning);
                    }
                    if (!FPVideoFarneback.Supported) EditorGUILayout.HelpBox("This graphics device does not support float32 compute flow. Ordinary stitching remains available.",MessageType.Info);
                }
                if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); dirty = true; error = null; if (settings.sourceProjection != FPVideoSourceProjection.DualFisheye) { calibrationView = FPVideoCalibrationView.Composite; settings.opticalFlow = false; } }
                else serialized.ApplyModifiedProperties();
                if (lensExpanded && settings.sourceProjection == FPVideoSourceProjection.DualFisheye)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Save calibration…")) SaveCalibration();
                        if (GUILayout.Button("Load calibration…")) LoadCalibration();
                    }
                }
                EditorGUILayout.Space(6); GUILayout.Label("Export range", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                double nextIn = EditorGUILayout.DoubleField("In (seconds)", exportIn), nextOut = EditorGUILayout.DoubleField("Out (exclusive)", exportOut);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(this, "Change video export range"); exportIn = nextIn; exportOut = nextOut; error = null; }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("In at playhead")) SetBookend(true);
                    if (GUILayout.Button("Out at playhead")) SetBookend(false);
                }
                if (GUILayout.Button("Full source range")) { Undo.RecordObject(this, "Reset video range"); exportIn = 0; exportOut = Duration; }
                includeAudio = EditorGUILayout.Toggle("Preserve audio", includeAudio);
                using (new EditorGUI.DisabledScope(!includeAudio))
                {
                    EditorGUI.BeginChangeCheck();
                    bool nextFastAudio = EditorGUILayout.Toggle(new GUIContent("Fast audio trim", "Seek near In and decode five seconds of audio preroll on qualified Windows Unity 6000.6 / 48 kHz sources. Disable to decode audio from the recording start."), fastAudioTrim);
                    if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(this, "Change audio trimming"); fastAudioTrim = nextFastAudio; }
                }
                EditorGUILayout.LabelField($"Video budget ~{settings.videoBitrateMbps * Math.Max(0, exportOut - exportIn) / 8000:F2} GB (actual varies)", EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox("Geometric lens calibration is required. GPU seam flow is experimental and opt-in; gyro stabilization and spherical MP4 tags are not applied.", MessageType.Info);
            if (source != null && !source.Info.ConstantFrameRate) EditorGUILayout.HelpBox("Variable frame timing detected. Export is disabled.", MessageType.Warning);
        }
        private string ValidationError()
        {
            try
            {
                settings.Validate();
                if (settings.opticalFlow && !FPVideoFarneback.Supported) return "Disable seam flow: this graphics device does not support its compute/float32 requirements.";
                if (preview == null || source == null) return "Load a source and wait for its first decoded frame.";
                FPVideoUnwrapExport.FrameRange(source.Info, exportIn, exportOut); return null;
            }
            catch (Exception e) { return e.Message; }
        }
        private void DrawActions()
        {
            string validation = ValidationError();
            if (validation != null) EditorGUILayout.HelpBox(validation, MessageType.Warning);
            using (new EditorGUI.DisabledScope(Busy || validation != null))
            {
                EditorGUI.BeginChangeCheck();
                string nextPath = EditorGUILayout.TextField(new GUIContent("Output MP4", "Enter a full output path to export directly. Leave empty to use the Save dialog."), exportPath);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(this, "Change video output path"); exportPath = nextPath; }
                Color previous = GUI.backgroundColor; GUI.backgroundColor = FP_Utility_Editor.OkayColor;
                if (GUILayout.Button("Export selected range…", GUILayout.Height(30))) StartExport(false);
                GUI.backgroundColor = previous;
                if (GUILayout.Button("Export 2-second test at playhead…")) StartExport(true);
            }
            if (Busy && GUILayout.Button("Cancel export")) { export.Dispose(); export = null; notice = "Export cancelled; partial files removed."; }
            using (new EditorGUI.DisabledScope(export == null || !export.Succeeded))
                if (GUILayout.Button("Reveal completed output")) EditorUtility.RevealInFinder(export.OutputPath);
        }
        private void StartExport(bool test)
        {
            string path = exportPath.Trim();
            if (string.IsNullOrEmpty(path))
                path = EditorUtility.SaveFilePanel("Export unwrapped MP4", Path.GetDirectoryName(source.OriginalPath), Path.GetFileNameWithoutExtension(source.OriginalPath) + "_" + settings.projection + (test ? "_test" : ""), "mp4");
            if (string.IsNullOrEmpty(path)) { notice = "No output selected. Enter a full Output MP4 path if the Save dialog is unavailable."; return; }
            playing = false; preview?.Pause(); error = null;
            try
            {
                if (!Path.IsPathRooted(path) || !string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Output MP4 must be a full path ending in .mp4.");
                export?.Dispose();
                double start = test ? Math.Min(playhead, Duration - 1 / source.Info.FrameRate) : exportIn;
                export = new FPVideoUnwrapExport(source, settings, start, test ? Math.Min(Duration, start + 2) : exportOut, path, includeAudio, fastAudioTrim);
            }
            catch (Exception e) { error = e.Message; export = null; }
        }
        private void DrawTransport()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            using (new EditorGUI.DisabledScope(preview == null || Busy))
            {
                if (GUILayout.Button("|◀", EditorStyles.toolbarButton, GUILayout.Width(28))) Seek(exportIn);
                if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(26))) Step(-1);
                if (GUILayout.Button(playing ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(46))) TogglePlay();
                if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(26))) Step(1);
                EditorGUI.BeginChangeCheck(); double value = EditorGUILayout.DoubleField(playhead, GUILayout.Width(90)); if (EditorGUI.EndChangeCheck()) Seek(value);
                GUILayout.Label("s", GUILayout.Width(12)); GUILayout.FlexibleSpace();
                sourceVisible = GUILayout.Toggle(sourceVisible, "Source", EditorStyles.toolbarButton);
                if (GUILayout.Button("Calibration", EditorStyles.toolbarButton, GUILayout.Width(72))) lensExpanded = !lensExpanded;
                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(30))) { viewStart = 0; viewEnd = Duration; }
            }
        }
        private void DrawPreviews()
        {
            Rect area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (sourceVisible)
            {
                float split = area.width * previewSplit;
                DrawImage(new Rect(area.x, area.y, split - 3, area.height), "Source • dual fisheye / panorama", preview?.Texture);
                DrawImage(new Rect(area.x + split + 3, area.y, area.width - split - 3, area.height), PreviewLabel, renderer?.Output, true);
                Rect divider = new Rect(area.x + split - 3, area.y, 6, area.height); int id = GUIUtility.GetControlID(0x46505644, FocusType.Passive, divider);
                EditorGUIUtility.AddCursorRect(divider, MouseCursor.ResizeHorizontal); var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && divider.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
                if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id) { previewSplit = Mathf.Clamp((e.mousePosition.x - area.x) / area.width, .2f, .8f); Repaint(); e.Use(); }
                if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
            }
            else DrawImage(area, PreviewLabel, renderer?.Output, true);
        }
        private string PreviewLabel => settings.projection + " • " + ObjectNames.NicifyVariableName(calibrationView.ToString()) + " (preview)";
        private void DrawImage(Rect panel, string label, Texture texture, bool calibrationSurface = false)
        {
            GUI.Box(panel, GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(panel.x + 6, panel.y + 4, panel.width - 12, 20), label, EditorStyles.miniBoldLabel);
            Rect image = new Rect(panel.x + 5, panel.y + 26, panel.width - 10, Mathf.Max(1, panel.height - 32));
            Rect fitted = image;
            if (texture != null)
            {
                float scale = Mathf.Min(image.width / texture.width, image.height / texture.height);
                fitted = new Rect(image.center.x - texture.width * scale * .5f, image.center.y - texture.height * scale * .5f, texture.width * scale, texture.height * scale);
                if (calibrationSurface) HandleImageNavigation(fitted);
            }
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(image, FP_Utility_Editor.UnityEditorDarkGrey);
            if (texture == null) { GUI.Label(image, "Waiting for decoded frame", EditorStyles.centeredGreyMiniLabel); return; }
            if (!calibrationSurface) GUI.DrawTexture(fitted, texture, ScaleMode.StretchToFill, false);
            else
            {
                GUI.DrawTextureWithTexCoords(fitted, texture, CalibrationUv(imageZoom, imagePan), false);
                if (calibrationGrid)
                    for (int i = 1; i < 4; i++)
                    {
                        Color color = new Color(1, 1, 1, i == 2 ? .65f : .25f);
                        EditorGUI.DrawRect(new Rect(fitted.x + fitted.width * i / 4, fitted.y, 1, fitted.height), color);
                        EditorGUI.DrawRect(new Rect(fitted.x, fitted.y + fitted.height * i / 4, fitted.width, 1), color);
                    }
            }
        }
        internal static Rect CalibrationUv(float zoom, Vector2 pan)
        {
            float span = 1 / Mathf.Clamp(zoom, 1, 8), half = span * .5f;
            return new Rect(Mathf.Clamp(.5f + pan.x, half, 1 - half) - half, Mathf.Clamp(.5f + pan.y, half, 1 - half) - half, span, span);
        }
        private void DrawCalibrationToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            using (new EditorGUI.DisabledScope(Busy))
            {
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(settings.sourceProjection != FPVideoSourceProjection.DualFisheye))
                    calibrationView = (FPVideoCalibrationView)EditorGUILayout.EnumPopup(calibrationView, EditorStyles.toolbarPopup, GUILayout.Width(150));
                if (calibrationView == FPVideoCalibrationView.OverlapOverlay)
                { GUILayout.Label("A mix", GUILayout.Width(36)); overlayMix = EditorGUILayout.Slider(overlayMix, 0, 1, GUILayout.MaxWidth(160)); }
                else if (calibrationView == FPVideoCalibrationView.Difference)
                { GUILayout.Label("Gain", GUILayout.Width(30)); differenceGain = EditorGUILayout.Slider(differenceGain, 1, 16, GUILayout.MaxWidth(160)); }
                if (EditorGUI.EndChangeCheck()) { dirty = true; playing = false; lensExpanded = true; }
                GUILayout.FlexibleSpace(); calibrationGrid = GUILayout.Toggle(calibrationGrid, "Grid", EditorStyles.toolbarButton, GUILayout.Width(36));
                GUILayout.Label($"{imageZoom:F1}×", GUILayout.Width(34));
                if (GUILayout.Button("Reset zoom", EditorStyles.toolbarButton, GUILayout.Width(76))) { imageZoom = 1; imagePan = Vector2.zero; dirty = true; }
            }
        }
        private void HandleImageNavigation(Rect rect)
        {
            if (Busy) return;
            var e = Event.current; int id = GUIUtility.GetControlID(0x46505649, FocusType.Passive, rect);
            if (rect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
            {
                Rect before = CalibrationUv(imageZoom, imagePan);
                var pointer = new Vector2((e.mousePosition.x - rect.x) / rect.width, 1 - (e.mousePosition.y - rect.y) / rect.height);
                Vector2 anchor = before.position + Vector2.Scale(pointer, before.size);
                imageZoom = Mathf.Clamp(imageZoom * Mathf.Exp(-e.delta.y * .12f), 1, 8);
                imagePan = anchor - (pointer - Vector2.one * .5f) / imageZoom - Vector2.one * .5f;
                Rect after = CalibrationUv(imageZoom, imagePan); imagePan = after.center - Vector2.one * .5f;
                dirty = true; Repaint(); e.Use();
            }
            if (e.type == EventType.MouseDown && e.button == 2 && rect.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
            if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                imagePan += new Vector2(-e.delta.x / rect.width, e.delta.y / rect.height) / imageZoom;
                imagePan = CalibrationUv(imageZoom, imagePan).center - Vector2.one * .5f; Repaint(); e.Use();
            }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
        }
        private void SaveCalibration()
        {
            try
            {
                var calibration = FPVideoLensCalibration.Capture(settings);
                string path = EditorUtility.SaveFilePanel("Save lens calibration", Path.GetDirectoryName(Application.dataPath), "VideoLensCalibration", "json");
                if (string.IsNullOrEmpty(path)) return;
                File.WriteAllText(path, JsonUtility.ToJson(calibration, true)); notice = "Calibration saved: " + path;
            }
            catch (Exception e) { error = e.Message; }
        }
        private void LoadCalibration()
        {
            try
            {
                string path = EditorUtility.OpenFilePanel("Load lens calibration", Path.GetDirectoryName(Application.dataPath), "json");
                if (string.IsNullOrEmpty(path)) return;
                var calibration = FPVideoLensCalibration.Parse(File.ReadAllText(path));
                var candidate = settings.Snapshot(); calibration.Apply(candidate);
                Undo.RecordObject(this, "Load video lens calibration"); settings = candidate;
                playing = false; dirty = true; error = null; notice = "Loaded calibration: " + settings.calibrationName;
            }
            catch (Exception e) { error = e.Message; }
        }
        private void DrawTimeline()
        {
            Rect full = GUILayoutUtility.GetRect(100, 82, GUILayout.ExpandWidth(true)); timelineRect = full;
            double span = Math.Max(.001, viewEnd - viewStart);
            Rect ruler = new Rect(full.x, full.y, full.width, 25), lane = new Rect(full.x, ruler.yMax, full.width, 40);
            FPTimelineViewUtility.DrawRuler(ruler, viewStart, viewEnd);
            EditorGUI.DrawRect(lane, FP_Utility_Editor.UnityEditorDarkGrey);
            float a = FPTimelineViewUtility.TimeToX(lane, exportIn - viewStart, span), b = FPTimelineViewUtility.TimeToX(lane, exportOut - viewStart, span);
            EditorGUI.DrawRect(new Rect(a, lane.y + 5, Mathf.Max(0, b - a), lane.height - 10), new Color(.15f, .5f, .7f, .3f));
            FPTimelineViewUtility.DrawPlayhead(lane, exportIn - viewStart, span, InColor, true);
            FPTimelineViewUtility.DrawPlayhead(lane, exportOut - viewStart, span, FP_Utility_Editor.WarningColor, true);
            FPTimelineViewUtility.DrawPlayhead(new Rect(full.x, full.y, full.width, 65), playhead - viewStart, span, PlayheadColor, true);
            GUI.Label(new Rect(full.x, lane.yMax, full.width, 17), "Drag: scrub • I/O: range • Space: play • ←/→: frame • Wheel: zoom • Middle drag: pan", EditorStyles.miniLabel);
            HandleTimeline(full, lane, span);
        }
        private void HandleTimeline(Rect full, Rect lane, double span)
        {
            var e = Event.current; int id = GUIUtility.GetControlID(0x46505654, FocusType.Passive, full);
            if (Busy || source == null) return;
            if (e.type == EventType.ScrollWheel && full.Contains(e.mousePosition))
            {
                double anchor = viewStart + FPTimelineViewUtility.XToTime(full, e.mousePosition.x, span), ratio = (anchor - viewStart) / span;
                double next = Math.Max(1 / source.Info.FrameRate, Math.Min(Duration, span * Math.Exp(e.delta.y * .12)));
                viewStart = Math.Max(0, Math.Min(Duration - next, anchor - ratio * next)); viewEnd = viewStart + next; Repaint(); e.Use();
            }
            if (e.type == EventType.MouseDown && full.Contains(e.mousePosition) && (e.button == 0 || e.button == 2))
            {
                GUIUtility.hotControl = id; playing = false;
                float xIn = FPTimelineViewUtility.TimeToX(lane, exportIn - viewStart, span), xOut = FPTimelineViewUtility.TimeToX(lane, exportOut - viewStart, span);
                timelineDrag = e.button == 2 ? 4 : lane.Contains(e.mousePosition) && Math.Abs(e.mousePosition.x - xIn) < 7 ? 2 : lane.Contains(e.mousePosition) && Math.Abs(e.mousePosition.x - xOut) < 7 ? 3 : 1;
                Undo.RecordObject(this, timelineDrag == 1 ? "Scrub video" : "Change video timeline");
                dragStart = e.mousePosition.x; dragViewStart = viewStart; dragViewEnd = viewEnd;
                if (timelineDrag == 1) Seek(viewStart + FPTimelineViewUtility.XToTime(full, e.mousePosition.x, span)); e.Use();
            }
            if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                double time = viewStart + FPTimelineViewUtility.XToTime(full, e.mousePosition.x, span);
                if (timelineDrag == 1) Seek(time);
                else if (timelineDrag == 2) exportIn = Math.Min(time, exportOut - 1 / source.Info.FrameRate);
                else if (timelineDrag == 3) exportOut = Math.Max(time, exportIn + 1 / source.Info.FrameRate);
                else if (timelineDrag == 4) { viewStart = Math.Max(0, Math.Min(Duration - (dragViewEnd - dragViewStart), dragViewStart - (e.mousePosition.x - dragStart) / full.width * (dragViewEnd - dragViewStart))); viewEnd = viewStart + dragViewEnd - dragViewStart; }
                error = null; Repaint(); e.Use();
            }
            if ((e.type == EventType.MouseUp || e.type == EventType.Ignore) && GUIUtility.hotControl == id) { timelineDrag = 0; GUIUtility.hotControl = 0; dirty = true; Repaint(); if (e.type == EventType.MouseUp) e.Use(); }
        }
        private void Seek(double seconds) { playing = false; preview?.Pause(); renderer?.ResetTemporal(); dirty = true; playhead = Math.Max(0, Math.Min(Duration, double.IsNaN(seconds) ? 0 : seconds)); Repaint(); }
        private void Step(int frames) { if (source != null) Seek(playhead + frames / source.Info.FrameRate); }
        private void TogglePlay()
        {
            if (playing) { playing = false; preview?.Pause(); return; }
            if (playhead >= exportOut || playhead < exportIn) playhead = exportIn;
            playStartOffset = playhead; playStartTime = EditorApplication.timeSinceStartup; playing = true;
        }
        private void SetBookend(bool start)
        {
            Undo.RecordObject(this, "Set video export bookend"); if (start) exportIn = playhead; else exportOut = playhead; error = null;
        }
        private void HandleShortcuts()
        {
            var e = Event.current; if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField || preview == null || Busy || e.control || e.command || e.alt) return;
            if (e.keyCode == KeyCode.Space) TogglePlay(); else if (e.keyCode == KeyCode.LeftArrow) Step(e.shift ? -10 : -1);
            else if (e.keyCode == KeyCode.RightArrow) Step(e.shift ? 10 : 1); else if (e.keyCode == KeyCode.I) SetBookend(true);
            else if (e.keyCode == KeyCode.O) SetBookend(false); else return; e.Use(); Repaint();
        }
        private void DrawStatus()
        {
            if (Busy) { Rect rect = GUILayoutUtility.GetRect(100, 20, GUILayout.ExpandWidth(true)); EditorGUI.ProgressBar(rect, export.Progress, export.Status); }
            if (calibrationView >= FPVideoCalibrationView.FlowVectors && !settings.opticalFlow)
                EditorGUILayout.HelpBox("Enable Farnebäck correction to inspect flow diagnostics. Warped Difference uses magenta for rejected overlap matches; inspect Flow Confidence alongside it.",MessageType.Info);
            GUILayout.Label(preview == null ? notice : $"Requested {playhead:F3} s • Displayed {preview.PresentedSeconds:F3} s • {preview.BackendStatus} • {(preview.UsesGpu ? "Submit" : "Decode")} {preview.LastDecodeMilliseconds:F1} ms • {preview.Texture?.width} × {preview.Texture?.height} preview • Preview audio muted", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.HelpBox(error ?? notice, error == null ? MessageType.Info : MessageType.Error);
        }
        private void HandleDrop(Rect rect)
        {
            var e = Event.current; if (Busy || !rect.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
            if (DragAndDrop.paths.Length != 1) return; string path = DragAndDrop.paths[0];
            string ext = Path.GetExtension(path).ToLowerInvariant(); if (ext != ".lrv" && ext != ".insv" && ext != ".mp4" && ext != ".mov") return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform) { DragAndDrop.AcceptDrag(); sourcePath = Path.GetFullPath(path); loadPending = reloadRange = true; }
            e.Use();
        }
    }
}
