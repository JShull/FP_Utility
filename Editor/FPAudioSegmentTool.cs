namespace FuzzPhyte.Utility.Editor
{
    using UnityEngine;
    using System;
    using System.IO;
    using UnityEditor;
    using System.Reflection;

    public class FPAudioSegmentTool:EditorWindow
    {
        private AudioClip sourceClip;

        // Timeline controls
        private float inTime = 0f;
        private float outTime = 0f;
        private float playhead = 0f;
        private bool clampToWholeSamples = true;

        // Export
        private string exportFileName = "AudioSegment";
        private string exportFolder = "Assets/_FPUtility/AudioExports";

        // Waveform cache
        private WaveformCache waveform;
        private int lastWaveWidth;
        private AudioClip lastClip;
        private float waveformScale = 0.35f; // default narrower look

        // Playback tracking for segment preview
        private double segmentPlayStartDsp;   // EditorAudio DSP time when we started preview
        private float segmentDuration;       // seconds; for stopping at `outTime`
        private bool isPlayingSegment;
        private bool autoAdvancePlayhead = true; 

        [MenuItem("FuzzPhyte/Audio/Audio Segment Tool", priority = FuzzPhyte.Utility.FP_UtilityData.ORDER_SUBMENU_LVL3)]
        public static void ShowWindow()
        {
            var win = GetWindow<FPAudioSegmentTool>("FP Audio Segment Tool");
            win.minSize = new Vector2(520, 360);
        }

        private void OnDisable()
        {
            EditorStopAll();
            isPlayingSegment = false;
            EditorApplication.update -= OnEditorUpdate;
        }
        private void OnEditorUpdate()
        {
            if (!isPlayingSegment) return;

            double elapsed = EditorApplication.timeSinceStartup - segmentPlayStartDsp;

            // Keep the UI playhead in sync (optional)
            if (autoAdvancePlayhead)
            {
                playhead = Mathf.Clamp((float)(inTime + elapsed), 0f, sourceClip.length);
                Repaint();
            }

            // Add a tiny pad to avoid early cuts due to timing jitter
            if (elapsed >= (double)segmentDuration - 1e-3)
            {
                isPlayingSegment = false;
                EditorStopAll();
                EditorApplication.update -= OnEditorUpdate;
                Repaint();
            }
        }
        private void OnGUI()
        {
            sourceClip = (AudioClip)EditorGUILayout.ObjectField("Source Clip", sourceClip, typeof(AudioClip), false);

            if (sourceClip == null)
            {
                EditorGUILayout.HelpBox("Drop an AudioClip to begin.", MessageType.Info);
                return;
            }

            float clipLen = Mathf.Max(0f, sourceClip.length);
            if (outTime <= 0f || outTime > clipLen) outTime = clipLen;
            inTime = Mathf.Clamp(inTime, 0f, clipLen);
            outTime = Mathf.Clamp(outTime, 0f, clipLen);
            if (outTime < inTime) outTime = inTime;
            playhead = Mathf.Clamp(playhead, 0f, clipLen);

            EditorGUILayout.LabelField($"Clip Length: {clipLen:F3}s | Freq: {sourceClip.frequency} | Ch: {sourceClip.channels}");
            EditorGUILayout.MinMaxSlider(new GUIContent("Segment (In/Out)"), ref inTime, ref outTime, 0f, clipLen);

            using (new EditorGUILayout.HorizontalScope())
            {
                inTime = EditorGUILayout.FloatField("In (sec)", inTime);
                outTime = EditorGUILayout.FloatField("Out (sec)", outTime);
            }

            playhead = EditorGUILayout.Slider("Playhead", playhead, 0f, clipLen);
            clampToWholeSamples = EditorGUILayout.ToggleLeft("Clamp in/out to whole samples", clampToWholeSamples);

            waveformScale = EditorGUILayout.Slider("Waveform Thickness", waveformScale, 0.05f, 1f);

            // --- Waveform Thumbnail ---
            DrawWaveformUI(clipLen);

            EditorGUILayout.Space(6);
            DrawPreviewButtons();

            EditorGUILayout.Space(8);
            DrawExportUI();
            HandleSegmentAutoStop(); // keep preview segment bounded by outTime
        }

        #region Waveform UI
        private void DrawWaveformUI(float clipLen)
        {
            const float height = 96f;
            Rect r = GUILayoutUtility.GetRect(10, 10000, height, height);

            // Rebuild cache if clip or width changed
            int width = Mathf.Max(1, (int)r.width);
            if (waveform == null || lastClip != sourceClip || lastWaveWidth != width)
            {
                waveform = BuildWaveformCache(sourceClip, width, 1024);
                lastClip = sourceClip;
                lastWaveWidth = width;
            }

            // Background
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f));

            if (waveform != null && waveform.min != null && waveform.max != null)
            {
                // Shade the KEPT region (in/out)
                float xIn = Mathf.Lerp(r.x, r.xMax, inTime / clipLen);
                float xOut = Mathf.Lerp(r.x, r.xMax, outTime / clipLen);
                Rect kept = new Rect(xIn, r.y, Mathf.Max(1f, xOut - xIn), r.height);
                EditorGUI.DrawRect(kept, new Color(0.25f, 0.5f, 0.25f, 0.25f));

                // Draw min/max waveform as vertical lines
                Handles.BeginGUI();
                for (int x = 0; x < waveform.width && x + r.x < r.xMax; x++)
                {
                    float min = waveform.min[x];
                    float max = waveform.max[x];
                    // drawing
                    float scaledMin = Mathf.Clamp(min * waveformScale, -1f, 1f);
                    float scaledMax = Mathf.Clamp(max * waveformScale, -1f, 1f);

                    /// Now compute pixel coords using scaledMin/scaledMax (thick mode)
                    //float yMin = Mathf.Lerp(r.center.y, r.yMax - 2, (1f - scaledMin) * 0.5f);
                    //float yMax = Mathf.Lerp(r.center.y, r.y + 2, (1f + scaledMax) * 0.5f);
                    //float px = r.x + x;
                    //Handles.DrawLine(new Vector3(px, yMin), new Vector3(px, yMax));


                    // After (centered mapping with padding):
                    float pad = 2f;
                    float half = (r.height * 0.5f) - pad;

                    // scale the envelope toward the center line
                    float yTop = r.center.y - (max * half * waveformScale);
                    float yBottom = r.center.y - (min * half * waveformScale);

                    // clamp to rect just in case
                    yTop = Mathf.Clamp(yTop, r.y + pad, r.yMax - pad);
                    yBottom = Mathf.Clamp(yBottom, r.y + pad, r.yMax - pad);

                    // draw the vertical min/max line
                    float px = r.x + x;
                    Handles.DrawLine(new Vector3(px, yTop), new Vector3(px, yBottom));

                }
                // center line
                Handles.color = new Color(1f, 1f, 1f, 0.08f);
                Handles.DrawLine(new Vector3(r.x, r.center.y), new Vector3(r.xMax, r.center.y));

                // Playhead
                float xHead = Mathf.Lerp(r.x, r.xMax, (clipLen > 0f ? playhead / clipLen : 0f));
                Handles.DrawLine(new Vector3(xHead, r.y), new Vector3(xHead, r.yMax));
                Handles.EndGUI();
            }
            else
            {
                EditorGUI.LabelField(r, "Building waveform…");
            }

            // Mouse interaction: click to set playhead
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                float pct = Mathf.InverseLerp(r.x, r.xMax, Event.current.mousePosition.x);
                playhead = pct * clipLen;
                Repaint();
            }
        }

        private class WaveformCache
        {
            public int width;
            public float[] min;
            public float[] max;
        }

        /// <summary>
        /// Generates downsampled min/max envelopes for a clip. Reads data once, mixes channels.
        /// </summary>
        private WaveformCache BuildWaveformCache(AudioClip clip, int width, int chunkSamples = 16384)
        {
            if (clip == null || width <= 0) return null;

            int channels = clip.channels;
            int totalSamples = clip.samples; // per channel
            int totalFrames = totalSamples;  // "frames" == samples per channel
            int framesPerColumn = Mathf.Max(1, totalFrames / width);

            var min = new float[width];
            var max = new float[width];
            for (int i = 0; i < width; i++) { min[i] = 1f; max[i] = -1f; }

            // Stream in chunks to avoid huge allocations
            int framesRead = 0;
            int colIndex = 0;
            int framesAccum = 0;
            float curMin = 1f;
            float curMax = -1f;

            float[] buffer = new float[chunkSamples * channels];

            while (framesRead < totalFrames)
            {
                int toRead = Mathf.Min(chunkSamples, totalFrames - framesRead);
                clip.GetData(buffer, framesRead); // reads starting frame across all channels into interleaved buffer
                int interleavedCount = toRead * channels;

                for (int i = 0; i < interleavedCount; i += channels)
                {
                    // Mixdown to mono by averaging channels
                    float sample = 0f;
                    for (int c = 0; c < channels; c++)
                        sample += buffer[i + c];
                    sample /= channels;

                    if (sample < curMin) curMin = sample;
                    if (sample > curMax) curMax = sample;

                    framesAccum++;
                    if (framesAccum >= framesPerColumn)
                    {
                        if (colIndex < width)
                        {
                            min[colIndex] = curMin;
                            max[colIndex] = curMax;
                            colIndex++;
                        }
                        framesAccum = 0;
                        curMin = 1f;
                        curMax = -1f;
                    }
                }

                framesRead += toRead;
            }

            // Flush last column if needed
            if (colIndex < width)
            {
                min[colIndex] = Mathf.Min(min[colIndex], curMin);
                max[colIndex] = Mathf.Max(max[colIndex], curMax);
            }

            return new WaveformCache { width = width, min = min, max = max };
        }
        #endregion

        #region Preview & Export UI
        private void DrawPreviewButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set In = Playhead", GUILayout.Height(24)))
                    inTime = Mathf.Clamp(playhead, 0f, outTime);

                if (GUILayout.Button("Set Out = Playhead", GUILayout.Height(24)))
                    outTime = Mathf.Clamp(playhead, inTime, sourceClip.length);

                if (GUILayout.Button("Jump Playhead to In", GUILayout.Height(24)))
                    playhead = inTime;

                if (GUILayout.Button("Jump Playhead to Out", GUILayout.Height(24)))
                    playhead = outTime;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview Full Clip"))
                {
                    isPlayingSegment = false;
                    EditorPreview(sourceClip, 0, false,1);
                }

                if (GUILayout.Button("Play Segment (in→out)"))
                {
                    PlaySegmentFromSource(inTime, outTime);
                }

                if (GUILayout.Button("Stop Preview"))
                {
                    isPlayingSegment = false;
                    EditorStopAll();
                }
            }
        }

        private void DrawExportUI()
        {
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            exportFileName = EditorGUILayout.TextField("File Name (no ext)", exportFileName);
            exportFolder = EditorGUILayout.TextField("Folder", exportFolder);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create In-Memory Segment", GUILayout.Height(26)))
                {
                    var seg = CreateSegmentClip(sourceClip, inTime, outTime, sourceClip.frequency, sourceClip.channels);
                    if (seg != null) Selection.activeObject = seg;
                }

                if (GUILayout.Button("Save Segment as .wav in Assets", GUILayout.Height(26)))
                {
                    SaveSegmentToAssets();
                }
            }
        }
        #endregion

        #region Core segment build & save
        private void SaveSegmentToAssets()
        {
            if (sourceClip == null) return;
            Directory.CreateDirectory(exportFolder);

            var seg = CreateSegmentClip(sourceClip, inTime, outTime, sourceClip.frequency, sourceClip.channels);
            if (seg == null)
            {
                Debug.LogError("Failed to build segment.");
                return;
            }

            // Use your FP audio util to convert to WAV
            byte[] wavBytes = FuzzPhyte.Utility.Audio.FP_AudioUtils.ConvertAudioClipToWAV(seg);

            var safeName = MakeSafeFilename(exportFileName);
            string outPath = $"{exportFolder}/{safeName}_in{inTime:F2}_out{outTime:F2}.wav";
            File.WriteAllBytes(outPath, wavBytes);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var imported = AssetDatabase.LoadAssetAtPath<AudioClip>(outPath);
            if (imported != null)
            {
                Debug.Log($"Saved segment to {outPath}");
                Selection.activeObject = imported;
            }
            else
            {
                Debug.LogWarning($"Wrote WAV but failed to load as AudioClip. Check importer settings: {outPath}");
            }
        }

        private static string MakeSafeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "AudioSegment" : name.Trim();
        }

        /// <summary>
        /// Build a new AudioClip from a slice [inTime,outTime].
        /// </summary>
        private AudioClip CreateSegmentClip(AudioClip clip, float inSec, float outSec, int frequency, int channels)
        {
            if (clip == null) return null;

            float start = Mathf.Clamp(inSec, 0f, clip.length);
            float end = Mathf.Clamp(outSec <= 0f ? clip.length : outSec, 0f, clip.length);
            if (end < start) end = start;

            int startSample = Mathf.FloorToInt(start * frequency);
            int endSample = Mathf.FloorToInt(end * frequency);

            int sampleCount = Mathf.Max(0, endSample - startSample);
            if (sampleCount == 0) return null;

            int totalSrcSamples = clip.samples * channels;
            float[] src = new float[totalSrcSamples];
            clip.GetData(src, 0);

            // Copy interleaved data
            int copyLen = sampleCount * channels;
            float[] dst = new float[copyLen];
            int srcOffset = startSample * channels;
            Array.Copy(src, srcOffset, dst, 0, copyLen);

            var seg = AudioClip.Create($"{clip.name}_seg_{start:F3}-{end:F3}", sampleCount, channels, frequency, false);
            seg.SetData(dst, 0);
            return seg;
        }
        #endregion

        #region Segment preview bounded by in/out (no asset write)
        /// <summary>
        /// Plays only the in→out range. We build a temporary in-memory segment and preview it.
        /// </summary>
        private void PlaySegmentFromSource(float inSec, float outSec)
        {
            if (sourceClip == null) return;

            // clamp & compute
            float start = Mathf.Clamp(inSec, 0f, sourceClip.length);
            float end = Mathf.Clamp(outSec <= 0f ? sourceClip.length : outSec, 0f, sourceClip.length);
            if (end < start) end = start;
            segmentDuration = end - start;

            // convert to sample index for EditorPreview
            int startSample = Mathf.FloorToInt(start * sourceClip.frequency);

            // stop anything already playing
            EditorStopAll();
            isPlayingSegment = false;
            EditorApplication.update -= OnEditorUpdate;

            // start preview from the source clip at startSample
            EditorPreview(sourceClip, startSample, loop: false, volume: 1f);

            // track & auto-stop
            segmentPlayStartDsp = EditorApplication.timeSinceStartup;
            isPlayingSegment = true;
            if (autoAdvancePlayhead) playhead = start;

            EditorApplication.update += OnEditorUpdate;
        }

        private void HandleSegmentAutoStop()
        {
            if (!isPlayingSegment) return;

            double elapsed = EditorApplication.timeSinceStartup - segmentPlayStartDsp;
            if (elapsed >= segmentDuration)
            {
                isPlayingSegment = false;
                EditorStopAll();
                Repaint();
            }
        }
        #endregion

        #region Editor Preview via AudioUtil (robust, multi-version)
        private static Type audioUtilType;
        private static MethodInfo miPlayClip3;          // PlayClip(AudioClip, int startSample, bool loop)
        private static MethodInfo miPlayClip4;          // PlayClip(AudioClip, int startSample, bool loop, float volume)
        private static MethodInfo miPlayPreviewClip;    // PlayPreviewClip(...)
        private static MethodInfo miStopAllClips;       // StopAllClips()
        private static MethodInfo miStopAllPreview;     // StopAllPreviewClips()
        private static MethodInfo miSetPreviewVolume;   // SetPreviewVolume(float)

        private static void EnsureAudioUtil()
        {
            if (audioUtilType != null) return;

            var asm = typeof(AudioImporter).Assembly;
            audioUtilType = asm.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null) return;

            var methods = audioUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.Name == "PlayClip")
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(AudioClip))
                        miPlayClip3 = m;
                    else if (p.Length == 4 && p[0].ParameterType == typeof(AudioClip) && p[3].ParameterType == typeof(float))
                        miPlayClip4 = m;
                }
                else if (m.Name == "PlayPreviewClip")
                {
                    miPlayPreviewClip = m; // varies in params across versions; invoke dynamically
                }
                else if (m.Name == "StopAllClips")
                {
                    miStopAllClips = m;
                }
                else if (m.Name == "StopAllPreviewClips")
                {
                    miStopAllPreview = m;
                }
                else if (m.Name == "SetPreviewVolume")
                {
                    var p = m.GetParameters();
                    if (p.Length == 1 && p[0].ParameterType == typeof(float))
                        miSetPreviewVolume = m;
                }
            }
        }

        private static void EditorPreview(AudioClip clip, int startSample = 0, bool loop = false, float volume = 1f)
        {
            if (clip == null) return;
            EnsureAudioUtil();

            // Set preview volume if the API exists
            try { miSetPreviewVolume?.Invoke(null, new object[] { Mathf.Clamp01(volume) }); }
            catch { /* ignore */ }

            // Prefer 4-arg PlayClip
            if (miPlayClip4 != null)
            {
                miPlayClip4.Invoke(null, new object[] { clip, startSample, loop, Mathf.Clamp01(volume) });
                return;
            }

            // Fallback to 3-arg PlayClip
            if (miPlayClip3 != null)
            {
                miPlayClip3.Invoke(null, new object[] { clip, startSample, loop });
                return;
            }

            // Last resort: PlayPreviewClip with best-effort args
            if (miPlayPreviewClip != null)
            {
                var prms = miPlayPreviewClip.GetParameters();
                var args = new object[prms.Length];
                for (int i = 0; i < prms.Length; i++)
                {
                    if (prms[i].ParameterType == typeof(AudioClip)) args[i] = clip;
                    else if (prms[i].ParameterType == typeof(int)) args[i] = startSample;
                    else if (prms[i].ParameterType == typeof(bool)) args[i] = loop;
                    else if (prms[i].ParameterType == typeof(float)) args[i] = Mathf.Clamp01(volume);
                    else args[i] = prms[i].IsOptional ? Type.Missing : null;
                }
                miPlayPreviewClip.Invoke(null, args);
                return;
            }

            Debug.LogWarning("Audio preview not supported on this Unity version (AudioUtil methods not found).");
        }

        private static void EditorStopAll()
        {
            EnsureAudioUtil();
            if (miStopAllClips != null) { miStopAllClips.Invoke(null, null); return; }
            if (miStopAllPreview != null) { miStopAllPreview.Invoke(null, null); return; }
        }
        #endregion

    }
}
