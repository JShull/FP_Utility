namespace FuzzPhyte.Utility.Editor
{
    using UnityEngine;
    using System;
    using System.IO;
    using UnityEditor;
    using System.Reflection;
    using System.Collections.Generic;

    public class FPAudioSegmentTool:EditorWindow
    {
        #region additional classes/structs
        [Serializable]
        private enum EditAction {NA=0, Mute=1, Cut=2 }
        private class WaveformCache
        {
            public int width;
            public float[] min;
            public float[] max;
        }
        [Serializable]
        private class EditRegion
        {
            public float start;
            public float end;
            public EditAction action;
        }
        #endregion
        private AudioClip sourceClip;

        // Timeline controls
        private float inTime = 0f;
        private float outTime = 0f;
        private float playhead = 0f;
        private bool clampToWholeSamples = true;
        private float waveFormHeightPixels = 100;
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

        //regions
        private List<EditRegion> regions = new List<EditRegion>();
        private EditAction newRegionAction = EditAction.Cut;
        private int fadeMs = 5; // small fade to avoid clicks on mutes/cuts
        private readonly Color kMuteFill = new Color(FP_Utility_Editor.WarningColor.r, FP_Utility_Editor.WarningColor.g, FP_Utility_Editor.WarningColor.b, 0.25f);
        private readonly Color kMuteOutline = new Color(FP_Utility_Editor.WarningColor.r, FP_Utility_Editor.WarningColor.g, FP_Utility_Editor.WarningColor.b, 0.9f);
        private readonly Color kCutFill = new Color(FP_Utility_Editor.OkayColor.r, FP_Utility_Editor.OkayColor.g, FP_Utility_Editor.OkayColor.b, 0.25f);
        private readonly Color kCutOutline = new Color(FP_Utility_Editor.OkayColor.r, FP_Utility_Editor.OkayColor.g, FP_Utility_Editor.OkayColor.b, 0.90f);
        private Color regionTypeColor = FP_Utility_Editor.OkayColor;
        
        private float quickRegionSeconds = 0.1f;
        private float regionStartSec = 0f;
        private float regionLengthSec = 0.10f; // default 100ms quick region
        private bool regionStartFollowsPlayhead = true; // convenience toggle

        [MenuItem("FuzzPhyte/Audio/Audio Segment Tool", priority = FuzzPhyte.Utility.FP_UtilityData.ORDER_SUBMENU_LVL6)]
        public static void ShowWindow()
        {
            var win = GetWindow<FPAudioSegmentTool>("FP Audio Segment Tool");
            win.minSize = new Vector2(520, 500);
        }
        private void OnEnable()
        {
            
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

            DrawPreviewButtons();
            EditorGUILayout.Space(10);
            // --- Waveform Thumbnail ---
            DrawWaveformUI(clipLen);
           

            EditorGUILayout.Space(10);
            FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);
            DrawRegionsUI(clipLen);
            FP_Utility_Editor.DrawUILine(regionTypeColor);
            FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);
            EditorGUILayout.Space(10);

            DrawExportUI();
            HandleSegmentAutoStop(); // keep preview segment bounded by outTime
        }

        #region Waveform UI
        private void DrawWaveformUI(float clipLen)
        {
            float height = waveFormHeightPixels;
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
                DrawRegionOverlays(r, clipLen);
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
            DrawGhostRegionOverlay(r, clipLen, regionStartSec, regionStartSec + regionLengthSec);
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

        #region Audio Region UI
        private void DrawGhostRegionOverlay(Rect r, float clipLen, float startSec, float endSec)
        {
            if (clipLen <= 0f) return;
            startSec = Mathf.Clamp(startSec, 0f, clipLen);
            endSec = Mathf.Clamp(endSec, 0f, clipLen);
            if (endSec <= startSec) return;

            float xStart = Mathf.Lerp(r.x, r.xMax, startSec / clipLen);
            float xEnd = Mathf.Lerp(r.x, r.xMax, endSec / clipLen);
            var rect = new Rect(xStart, r.y, Mathf.Max(1f, xEnd - xStart), r.height);

            // subtle ghost (different from your region colors)
            var fill = new Color(1f, 1f, 1f, 0.08f);
            var outline = new Color(1f, 1f, 1f, 0.35f);

            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(rect, fill, outline);
            Handles.EndGUI();
        }

        private void DrawRegionsUI(float clipLen)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Region Picker (independent of segment In/Out)", EditorStyles.boldLabel);
            // Choose the action for the *next* region you add
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUILayout.HorizontalScope())
            {
                float regionEndSec = 0;
                // LEFT
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    newRegionAction = (EditAction)EditorGUILayout.EnumPopup(
                new GUIContent("Action", "Type for the region you're about to add (Mute = time preserved, Cut = time compressed)"),
                newRegionAction
                );
                    if (newRegionAction == EditAction.Mute)
                    {
                        regionTypeColor = FP_Utility_Editor.WarningColor;
                    }
                    else
                    {
                        regionTypeColor = FP_Utility_Editor.OkayColor;
                    }
                    // Let the user choose if the region start should track the playhead
                    regionStartFollowsPlayhead = EditorGUILayout.ToggleLeft("Region Start follows Playhead", regionStartFollowsPlayhead);

                    if (regionStartFollowsPlayhead) regionStartSec = playhead;

                    // Start slider (single normal slider)
                    float maxStart = Mathf.Max(0f, sourceClip.length - 0.001f);
                    regionStartSec = EditorGUILayout.Slider("Region Start (sec)", regionStartSec, 0f, maxStart);

                    // Length slider (separate)
                    float maxLen = Mathf.Max(0.01f, sourceClip.length - regionStartSec);
                    regionLengthSec = EditorGUILayout.Slider("Region Length (sec)", regionLengthSec, 0.01f, maxLen);

                    // Computed end (read-only display)
                    regionEndSec = Mathf.Min(regionStartSec + regionLengthSec, sourceClip.length);
                    
                }
                GUILayout.Space(10);
                //RIGHT
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width*0.5f)))
                {
                    if (GUILayout.Button("Set Start = Playhead", GUILayout.Height(20)))
                        regionStartSec = Mathf.Clamp(playhead, 0f, Mathf.Max(0f, sourceClip.length - regionLengthSec));

                    if (GUILayout.Button("Add Region (Start+Length)", GUILayout.Height(20)))
                    {
                        regions.Add(new EditRegion
                        {
                            start = regionStartSec,
                            end = regionEndSec,
                            action = newRegionAction // uses your existing Mute/Cut selector
                        });
                        SortAndMergeRegions();
                        Repaint();
                    }
                    EditorGUILayout.LabelField($"Region: {regionStartSec:F3}s → {regionEndSec:F3}s  (len {regionLengthSec:F3}s)");
                }
            }
            FP_Utility_Editor.DrawUILine(regionTypeColor);
            EditorGUILayout.Space(2);
            fadeMs = EditorGUILayout.IntSlider(new GUIContent("Edge Fade (ms)", "Applies short linear fades to prevent clicks"),
                                               fadeMs, 0, 30);

            if (regions.Count == 0)
            {
                EditorGUILayout.HelpBox("No regions added. Use the button above to add one from your current In/Out selection.", MessageType.Info);
                return;
            }

            // List
            for (int i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    r.action = (EditAction)EditorGUILayout.EnumPopup(r.action, GUILayout.Width(70));
                    r.start = EditorGUILayout.FloatField(r.start);
                    r.end = EditorGUILayout.FloatField(r.end);

                    if (GUILayout.Button("↤ In", GUILayout.Width(40))) r.start = inTime;
                    if (GUILayout.Button("Out ↦", GUILayout.Width(50))) r.end = outTime;
                    if (GUILayout.Button("X", GUILayout.Width(24))) { regions.RemoveAt(i); i--; continue; }
                }
                r.start = Mathf.Clamp(r.start, 0, sourceClip.length);
                r.end = Mathf.Clamp(r.end, 0, sourceClip.length);
                if (r.end < r.start) r.end = r.start;
            }

            /*
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview with Mutes (time preserved)"))
                {
                    var tmp = BuildMutedClip(sourceClip, regions, fadeMs);
                    if (tmp != null) EditorPreview(tmp, 0, false, 1f);
                }

                if (GUILayout.Button("Preview After Cuts (time compressed)"))
                {
                    var tmp = BuildCutClip(sourceClip, regions, fadeMs);
                    if (tmp != null) EditorPreview(tmp, 0, false, 1f);
                }
            }

            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Muted Version (.wav)"))
                {
                    var muted = BuildMutedClip(sourceClip, regions, fadeMs);
                    if (muted != null) SaveClipToAssets(muted, exportFileName + "_MUTED");
                }
                if (GUILayout.Button("Save Cut Version (.wav)"))
                {
                    var cut = BuildCutClip(sourceClip, regions, fadeMs);
                    if (cut != null) SaveClipToAssets(cut, exportFileName + "_CUT");
                }
            }
            */
        }
        private void SortAndMergeRegions()
        {
            // clamp and sort
            for (int i = 0; i < regions.Count; i++)
            {
                regions[i].start = Mathf.Clamp(regions[i].start, 0f, sourceClip.length);
                regions[i].end = Mathf.Clamp(regions[i].end, 0f, sourceClip.length);
                if (regions[i].end < regions[i].start) regions[i].end = regions[i].start;
            }

            regions.Sort((a, b) => a.start.CompareTo(b.start));

            // simple merge of overlapping regions with same action
            for (int i = 1; i < regions.Count; i++)
            {
                var prev = regions[i - 1];
                var cur = regions[i];
                if (cur.start <= prev.end && cur.action == prev.action)
                {
                    prev.end = Mathf.Max(prev.end, cur.end);
                    regions.RemoveAt(i);
                    i--;
                }
            }
        }

        private static void ApplyLinearFade(float[] data, int start, int end, int channels, int fadeSamples, bool fadeIn)
        {
            fadeSamples = Mathf.Clamp(fadeSamples, 0, Mathf.Max(0, end - start));
            for (int n = 0; n < fadeSamples; n++)
            {
                float t = (n + 1) / (float)fadeSamples;
                float g = fadeIn ? t : (1f - t);
                int frame = fadeIn ? (start + n) : (end - fadeSamples + n);
                int baseIdx = frame * channels;
                for (int c = 0; c < channels; c++)
                    data[baseIdx + c] *= g;
            }
        }
        private AudioClip BuildMutedClip(AudioClip clip, List<EditRegion> edits, int fadeMs)
        {
            if (clip == null) return null;
            int ch = clip.channels;
            int hz = clip.frequency;

            float[] src = new float[clip.samples * ch];
            clip.GetData(src, 0);

            // Copy so we don't mutate src
            float[] dst = new float[src.Length];
            Array.Copy(src, dst, src.Length);

            int fade = Mathf.RoundToInt(hz * (fadeMs / 1000f));

            foreach (var e in edits)
            {
                if (e.action != EditAction.Mute) continue;

                int s = Mathf.RoundToInt(Mathf.Clamp01(e.start / clip.length) * clip.samples);
                int t = Mathf.RoundToInt(Mathf.Clamp01(e.end / clip.length) * clip.samples);
                if (t <= s) continue;

                // edge fades
                ApplyLinearFade(dst, s, t, ch, fade, false); // fade-out at region start
                ApplyLinearFade(dst, s, t, ch, fade, true); // fade-in at region end

                // zero body
                int bodyStart = (s + fade);
                int bodyEnd = (t - fade);
                if (bodyEnd > bodyStart)
                {
                    int baseIdx = bodyStart * ch;
                    Array.Clear(dst, baseIdx, (bodyEnd - bodyStart) * ch);
                }
            }

            var name = $"{clip.name}_MUTED";
            var outClip = AudioClip.Create(name, clip.samples, ch, hz, false);
            outClip.SetData(dst, 0);
            return outClip;
        }
        private AudioClip BuildCutClip(AudioClip clip, List<EditRegion> edits, int fadeMs)
        {
            if (clip == null) return null;

            int ch = clip.channels;
            int hz = clip.frequency;

            // Work with a sorted, merged view
            var cuts = new List<EditRegion>();
            foreach (var e in edits) if (e.action == EditAction.Cut) cuts.Add(new EditRegion { start = e.start, end = e.end, action = e.action });
            if (cuts.Count == 0) return clip; // nothing to cut → return source for preview

            cuts.Sort((a, b) => a.start.CompareTo(b.start));

            // Compute “keep” segments = everything not cut
            var keep = new List<(int s, int t)>();
            int lastSamp = 0;
            foreach (var c in cuts)
            {
                int cutS = Mathf.RoundToInt(Mathf.Clamp(c.start, 0, clip.length) * hz);
                int cutT = Mathf.RoundToInt(Mathf.Clamp(c.end, 0, clip.length) * hz);
                cutS = Mathf.Clamp(cutS, 0, clip.samples);
                cutT = Mathf.Clamp(cutT, 0, clip.samples);
                if (cutT < cutS) cutT = cutS;

                if (cutS > lastSamp) keep.Add((lastSamp, cutS));
                lastSamp = Math.Max(lastSamp, cutT);
            }
            if (lastSamp < clip.samples) keep.Add((lastSamp, clip.samples));

            // Total length after cuts
            int keptSamples = 0;
            foreach (var k in keep) keptSamples += (k.t - k.s);

            float[] src = new float[clip.samples * ch];
            clip.GetData(src, 0);
            float[] dst = new float[keptSamples * ch];

            // Stitch keep segments with small crossfades at seams
            int fade = Mathf.RoundToInt(hz * (fadeMs / 1000f));
            int writeSample = 0;

            for (int i = 0; i < keep.Count; i++)
            {
                var k = keep[i];
                int segLen = k.t - k.s;
                int dstOffsetFrames = writeSample;
                // copy segment
                Array.Copy(src, k.s * ch, dst, dstOffsetFrames * ch, segLen * ch);

                // crossfade into next segment (if any)
                if (i < keep.Count - 1 && fade > 0)
                {
                    int fadeLen = Mathf.Min(fade, segLen, keep[i + 1].t - keep[i + 1].s);
                    for (int n = 0; n < fadeLen; n++)
                    {
                        float a = 1f - (n / (float)fadeLen); // fade out tail
                        float b = (n / (float)fadeLen);      // fade in next head

                        int tailFrame = (dstOffsetFrames + segLen - fadeLen + n);
                        int nextSrcFrame = keep[i + 1].s + n;

                        for (int c = 0; c < ch; c++)
                        {
                            int tailIdx = tailFrame * ch + c;
                            int nextIdxSrc = nextSrcFrame * ch + c;

                            float tail = dst[tailIdx] * a;
                            float head = src[nextIdxSrc] * b;
                            dst[tailIdx] = tail + head; // overlap-add
                        }
                    }

                    // We advanced `writeSample` by segLen - fadeLen (since the next keep head was blended in)
                    writeSample += segLen - fadeLen;
                    // Also skip the head of the next keep when we copy it in the next loop iteration
                    keep[i + 1] = (keep[i + 1].s + fade, keep[i + 1].t);
                }
                else
                {
                    writeSample += segLen;
                }
            }

            var name = $"{clip.name}_CUT";
            var outClip = AudioClip.Create(name, keptSamples, ch, hz, false);
            outClip.SetData(dst, 0);
            return outClip;
        }
        private void DrawRegionOverlays(Rect r, float clipLen)
        {
            if (regions == null || regions.Count == 0 || clipLen <= 0f) return;

            Handles.BeginGUI();
            GUIStyle _regionLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) }
            };
            for (int i = 0; i < regions.Count; i++)
            {
                var reg = regions[i];
                // convert time → x
                float xStart = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(reg.start / clipLen));
                float xEnd = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(reg.end / clipLen));
                if (xEnd <= xStart) continue;

                var rect = new Rect(xStart, r.y, Mathf.Max(1f, xEnd - xStart), r.height);

                // choose colors by action
                Color fill, outline;
                if (reg.action == EditAction.Mute) { fill = kMuteFill; outline = kMuteOutline; }
                else { fill = kCutFill; outline = kCutOutline; }

                // block
                Handles.DrawSolidRectangleWithOutline(rect, fill, outline);

                // small label in the corner
                var labelRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, 16f);
                string text = $"{reg.action}  {reg.start:F2}–{reg.end:F2}s";

                GUI.Label(labelRect, text, _regionLabelStyle);

                // (optional) hover cursor
                if (rect.Contains(Event.current.mousePosition))
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }

            Handles.EndGUI();
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

            var seg = BuildProcessedSegmentClip(
                sourceClip,
                inTime,
                outTime,
                regions,   // your List<EditRegion>
                fadeMs     // your fade slider
            );

            if (seg == null)
            {
                Debug.LogError("Failed to build processed segment.");
                return;
            }

            byte[] wavBytes = FuzzPhyte.Utility.Audio.FP_AudioUtils.ConvertAudioClipToWAV(seg);

            var safeName = MakeSafeFilename(exportFileName);
            string outPath = $"{exportFolder}/{safeName}_in{inTime:F2}_out{outTime:F2}.wav";

            File.WriteAllBytes(outPath, wavBytes);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var imported = AssetDatabase.LoadAssetAtPath<AudioClip>(outPath);
            if (imported != null) Selection.activeObject = imported;
        }
        /// <summary>
        /// Build final segment using current in/out, then apply CUT (time-compress) and MUTE (time-preserve) regions.
        /// - Cuts are applied first *inside the in/out window* with crossfades at seams.
        /// - Mutes are then applied on the post-cut timeline with short edge fades.
        /// </summary>
        private AudioClip BuildProcessedSegmentClip(AudioClip clip,float inSec,float outSec,List<EditRegion> allRegions,int fadeMs)
        {
            if (clip == null) return null;

            int ch = clip.channels;
            int hz = clip.frequency;

            // 1) Clamp selection and map to samples
            float inClamped = Mathf.Clamp(inSec, 0f, clip.length);
            float outClamped = Mathf.Clamp(outSec <= 0f ? clip.length : outSec, 0f, clip.length);
            if (outClamped < inClamped) outClamped = inClamped;

            int selStartSamp = Mathf.RoundToInt(inClamped * hz);
            int selEndSamp = Mathf.RoundToInt(outClamped * hz);
            int selLenSamp = Mathf.Max(0, selEndSamp - selStartSamp);
            if (selLenSamp == 0) return null;

            // Pull selection into a temp buffer (source of truth for edits)
            float[] src = new float[clip.samples * ch];
            clip.GetData(src, 0);

            float[] seg = new float[selLenSamp * ch];
            Array.Copy(src, selStartSamp * ch, seg, 0, seg.Length);

            // Helpers to convert between seconds (global) and selection-local samples
            int SecToSelSamp(float sec) => Mathf.RoundToInt(Mathf.Clamp(sec - inClamped, 0f, outClamped - inClamped) * hz);

            // 2) Partition regions by action and intersect with the selection
            var cutRegs = new List<(int s, int t)>();               // selection-local sample spans
            var muteRegs = new List<(int s, int t)>();               // selection-local sample spans

            if (allRegions != null)
            {
                foreach (var r in allRegions)
                {
                    // intersect [r.start, r.end] with [inClamped, outClamped]
                    float s = Mathf.Max(r.start, inClamped);
                    float t = Mathf.Min(r.end, outClamped);
                    if (t <= s) continue;

                    int rs = SecToSelSamp(s);
                    int rt = SecToSelSamp(t);
                    if (rt <= rs) continue;

                    if (r.action == EditAction.Cut) cutRegs.Add((rs, rt));
                    else muteRegs.Add((rs, rt));
                }
            }

            // Sort & merge overlapping cuts (keeps behavior predictable)
            cutRegs.Sort((a, b) => a.s.CompareTo(b.s));
            for (int i = 1; i < cutRegs.Count; i++)
            {
                var prev = cutRegs[i - 1];
                var cur = cutRegs[i];
                if (cur.s <= prev.t) // overlap or touch
                {
                    cutRegs[i - 1] = (prev.s, Mathf.Max(prev.t, cur.t));
                    cutRegs.RemoveAt(i);
                    i--;
                }
            }

            // 3) Compute keep segments inside the selection after cuts
            var keep = new List<(int s, int t)>();
            int cursor = 0;
            foreach (var c in cutRegs)
            {
                if (c.s > cursor) keep.Add((cursor, c.s));
                cursor = Mathf.Max(cursor, c.t);
            }
            if (cursor < selLenSamp) keep.Add((cursor, selLenSamp));

            // Early out: no cuts → result timeline length equals selection length
            int dstLenSamp = 0;
            foreach (var k in keep) dstLenSamp += (k.t - k.s);

            float[] dst = new float[dstLenSamp * ch];

            // 4) Copy keep segments with crossfades at seams (to smooth the CUT joins)
            int fadeSamp = Mathf.RoundToInt(hz * (fadeMs / 1000f));
            int write = 0;

            for (int i = 0; i < keep.Count; i++)
            {
                var k = keep[i];
                int segLen = k.t - k.s;

                // Copy the raw segment
                Array.Copy(seg, k.s * ch, dst, write * ch, segLen * ch);

                // Crossfade into next keep (if exists)
                if (i < keep.Count - 1 && fadeSamp > 0)
                {
                    // Limit fade to available samples on both sides
                    int nextLen = keep[i + 1].t - keep[i + 1].s;
                    int f = Mathf.Min(fadeSamp, segLen, nextLen);

                    // Overlap-add: tail of current with head of *next source* (but we haven’t copied next yet)
                    for (int n = 0; n < f; n++)
                    {
                        float a = 1f - (n / (float)f); // fade out tail
                        float b = (n / (float)f);      // fade in head

                        int tailFrameInDst = (write + segLen - f + n);
                        int headFrameInSrc2 = keep[i + 1].s + n;

                        for (int cch = 0; cch < ch; cch++)
                        {
                            int tailIdx = tailFrameInDst * ch + cch;
                            int headIdx = headFrameInSrc2 * ch + cch;
                            float tail = dst[tailIdx] * a;
                            float head = seg[headIdx] * b;
                            dst[tailIdx] = tail + head;
                        }
                    }

                    // We “used” f samples of the next head; advance write by segLen - f
                    write += segLen - f;

                    // Shift the next keep's start forward by f so its head won't be copied again
                    keep[i + 1] = (keep[i + 1].s + f, keep[i + 1].t);
                }
                else
                {
                    write += segLen;
                }
            }

            // 5) Apply MUTE regions *on the post-cut timeline*.
            // We need to map each mute-reg (selection-local samples) into the compressed dst timeline.
            // Build a mapping: for a given selection sample X, what's its position in dst?
            //   dstPos(X) = sum over keep segments of clamp( min(max(X - keep.s, 0), keep.len) )
            int[] keepStarts = new int[keep.Count];
            int[] keepLens = new int[keep.Count];
            int[] dstBase = new int[keep.Count]; // where each keep begins in dst
            {
                int acc = 0;
                for (int i = 0; i < keep.Count; i++)
                {
                    keepStarts[i] = keep[i].s;
                    keepLens[i] = keep[i].t - keep[i].s;
                    dstBase[i] = acc;
                    acc += keepLens[i];
                }
            }

            int MapSelSampToDst(int xSel)
            {
                // Binary search could be used; keep is small, linear is fine.
                int acc = 0;
                for (int i = 0; i < keep.Count; i++)
                {
                    int ks = keepStarts[i];
                    int kl = keepLens[i];
                    if (xSel < ks) break;

                    int within = Mathf.Clamp(xSel - ks, 0, kl);
                    acc = dstBase[i] + within;
                }
                return Mathf.Clamp(acc, 0, dstLenSamp);
            }

            // Apply each mute region (converted to dst coordinates) with small edge fades
            foreach (var mr in muteRegs)
            {
                // Intersection of mute with selection is already applied; convert to dst positions
                int dstS = MapSelSampToDst(mr.s);
                int dstT = MapSelSampToDst(mr.t);
                if (dstT <= dstS) continue;

                int f = Mathf.Min(fadeSamp, (dstT - dstS) / 2);

                // Fade out into the mute
                for (int n = 0; n < f; n++)
                {
                    float g = 1f - (n + 1) / (float)f;  // 1 → 0
                    int frame = (dstS + n);
                    for (int cch = 0; cch < ch; cch++)
                    {
                        int idx = frame * ch + cch;
                        dst[idx] *= g;
                    }
                }

                // Zero body
                int bodyStart = dstS + f;
                int bodyEnd = dstT - f;
                if (bodyEnd > bodyStart)
                {
                    Array.Clear(dst, bodyStart * ch, (bodyEnd - bodyStart) * ch);
                }

                // Fade back in after the mute
                for (int n = 0; n < f; n++)
                {
                    float g = (n + 1) / (float)f; // 0 → 1
                    int frame = (dstT - f + n);
                    for (int cch = 0; cch < ch; cch++)
                    {
                        int idx = frame * ch + cch;
                        dst[idx] *= g;
                    }
                }
            }

            // 6) Bake to an AudioClip
            string name = $"{clip.name}_SEG";
            var outClip = AudioClip.Create(name, dstLenSamp, ch, hz, false);
            outClip.SetData(dst, 0);
            return outClip;
        }

        private void SaveClipToAssets(AudioClip clip, string baseName)
        {
            Directory.CreateDirectory(exportFolder);
            byte[] wavBytes = FuzzPhyte.Utility.Audio.FP_AudioUtils.ConvertAudioClipToWAV(clip);
            string safe = MakeSafeFilename(baseName);
            string path = $"{exportFolder}/{safe}.wav";
            File.WriteAllBytes(path, wavBytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var imported = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (imported != null) Selection.activeObject = imported;
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
