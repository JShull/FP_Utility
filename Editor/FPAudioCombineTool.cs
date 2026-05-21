namespace FuzzPhyte.Utility.Editor
{
    using UnityEngine;
    using UnityEditor;
    using System;
    using System.IO;
    using System.Reflection;
    using System.Collections.Generic;

    public class FPAudioCombineTool : EditorWindow
    {
        [Serializable]
        private class CombineEntry
        {
            public AudioClip clip;
            public float inTime;
            public float outTime;
            public float timelineStart;
            public Color trackColor;
            public float gain = 1f;
            public bool gainInitialized;
            public bool locked;
            public bool muted;
            public bool expanded = true;
            public WaveformCache waveform;
            public int lastWaveWidth;
            public AudioClip lastClip;
            public float lastWaveIn;
            public float lastWaveOut;

            public float SegmentLength => clip == null ? 0f : Mathf.Max(0f, outTime - inTime);
            public float SegmentTimelineStart => timelineStart + inTime;
            public float SegmentTimelineEnd => timelineStart + outTime;
            public float SourceTimelineEnd => clip == null ? timelineStart : timelineStart + clip.length;
            public float TimelineEnd => SegmentTimelineEnd;
        }

        private class WaveformCache
        {
            public int width;
            public float[] min;
            public float[] max;
            public float peak;
        }

        private readonly List<CombineEntry> entries = new List<CombineEntry>();
        private Vector2 scroll;

        private float playhead;
        private bool autoAdvancePlayhead = true;
        private bool isPlayingCombined;
        private double combinedPlayStartTime;
        private float combinedPlayStartOffset;
        private int overviewDragIndex = -1;
        private float overviewDragOffset;
        private float overviewDragTimelineLength;

        private int outputFrequency = 44100;
        private int outputChannels = 2;
        private bool normalizeIfClipping = true;
        private float defaultGapSeconds = 2f;
        private float nudgeSeconds = 0.25f;
        private bool hasExportStartBookend;
        private float exportStartBookend;

        private string exportFileName = "AudioCombined";
        private string exportFolder = "Assets/_FPUtility/AudioExports";

        private static readonly Color kTrackBackground = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color kPlayheadColor = new Color(1f, 0.72f, 0.25f, 0.95f);
        private static readonly Color kExportStartColor = new Color(0.25f, 0.75f, 1f, 0.95f);

        [MenuItem("FuzzPhyte/Utility/Audio/Combine Tool", priority = FuzzPhyte.Utility.FP_UtilityData.MENU_UTILITY_AUDIO + 1)]
        public static void ShowWindow()
        {
            var win = GetWindow<FPAudioCombineTool>("FP Audio Combine Tool");
            win.minSize = new Vector2(620, 560);
            win.AddSelectedAudioClips(false);
        }

        private void OnDisable()
        {
            EditorStopAll();
            isPlayingCombined = false;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!isPlayingCombined)
            {
                return;
            }

            float totalLength = CalculateMixTimelineEnd();
            double elapsed = EditorApplication.timeSinceStartup - combinedPlayStartTime;

            if (autoAdvancePlayhead)
            {
                playhead = Mathf.Clamp(combinedPlayStartOffset + (float)elapsed, 0f, totalLength);
                Repaint();
            }

            if (elapsed >= Mathf.Max(0f, totalLength - combinedPlayStartOffset) - 1e-3f)
            {
                isPlayingCombined = false;
                EditorStopAll();
                EditorApplication.update -= OnEditorUpdate;
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            float timelineLength = Mathf.Max(0.01f, CalculateTimelineLength());
            playhead = Mathf.Clamp(playhead, 0f, timelineLength);
            if (hasExportStartBookend)
            {
                exportStartBookend = Mathf.Clamp(exportStartBookend, 0f, timelineLength);
            }

            EditorGUILayout.Space(4);
            DrawPlaybackUI(timelineLength);
            EditorGUILayout.Space(6);
            DrawTimelineOverview(timelineLength);
            EditorGUILayout.Space(8);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawEntryList(timelineLength);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            DrawAudioDropArea(timelineLength);
            EditorGUILayout.Space(8);
            DrawExportUI();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.LabelField("Audio Combine", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected Clip(s)", GUILayout.Height(24)))
                {
                    AddSelectedAudioClips(true);
                }

                if (GUILayout.Button("Add Empty Row", GUILayout.Height(24)))
                {
                    entries.Add(new CombineEntry { trackColor = GenerateTrackColor(entries.Count) });
                }

                if (GUILayout.Button("Auto Layout", GUILayout.Height(24)))
                {
                    AutoLayoutEntries();
                }

                if (GUILayout.Button("Clear", GUILayout.Height(24)))
                {
                    EditorStopAll();
                    isPlayingCombined = false;
                    entries.Clear();
                    playhead = 0f;
                    hasExportStartBookend = false;
                    exportStartBookend = 0f;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                defaultGapSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Gap (sec)", defaultGapSeconds));
                nudgeSeconds = Mathf.Max(0.001f, EditorGUILayout.FloatField("Nudge (sec)", nudgeSeconds));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                outputFrequency = Mathf.Max(8000, EditorGUILayout.IntField("Output Hz", outputFrequency));
                outputChannels = EditorGUILayout.IntPopup("Output Channels", outputChannels, new[] { "Mono", "Stereo" }, new[] { 1, 2 });
                normalizeIfClipping = EditorGUILayout.ToggleLeft("Normalize if mix clips", normalizeIfClipping, GUILayout.Width(160));
            }
        }

        private void DrawPlaybackUI(float timelineLength)
        {
            playhead = EditorGUILayout.Slider("Playhead", playhead, 0f, timelineLength);
            autoAdvancePlayhead = EditorGUILayout.ToggleLeft("Auto-advance playhead during preview", autoAdvancePlayhead);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play From Playhead", GUILayout.Height(26)))
                {
                    PlayCombinedFrom(playhead);
                }

                if (GUILayout.Button("Play All", GUILayout.Height(26)))
                {
                    playhead = 0f;
                    PlayCombinedFrom(0f);
                }

                if (GUILayout.Button("Stop Preview", GUILayout.Height(26)))
                {
                    isPlayingCombined = false;
                    EditorStopAll();
                    EditorApplication.update -= OnEditorUpdate;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Export Start = Playhead", GUILayout.Height(24)))
                {
                    exportStartBookend = Mathf.Clamp(playhead, 0f, timelineLength);
                    hasExportStartBookend = true;
                }

                GUI.enabled = hasExportStartBookend;
                if (GUILayout.Button("Remove Export Start", GUILayout.Height(24), GUILayout.Width(160)))
                {
                    hasExportStartBookend = false;
                    exportStartBookend = 0f;
                }
                GUI.enabled = true;

                string label = hasExportStartBookend ? $"Export starts at {exportStartBookend:F3}s" : "Export starts at 0.000s";
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(170));
            }
        }

        private void DrawTimelineOverview(float timelineLength)
        {
            Rect r = GUILayoutUtility.GetRect(10, 10000, 58, 58);
            EditorGUI.DrawRect(r, new Color(0.08f, 0.08f, 0.08f));

            if (entries.Count == 0)
            {
                EditorGUI.LabelField(r, "Add AudioClips to build a combined timeline.");
                return;
            }

            Handles.BeginGUI();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.clip == null || e.SegmentLength <= 0f)
                {
                    continue;
                }

                float y = Mathf.Lerp(r.y + 5f, r.yMax - 15f, entries.Count <= 1 ? 0.5f : i / (float)(entries.Count - 1));
                Rect sourceBlock = BuildSourceRect(r, e, timelineLength, y, 9f);
                Rect block = BuildSegmentRect(r, e, timelineLength, y, 9f);
                Color displayColor = e.muted ? Color.Lerp(e.trackColor, Color.gray, 0.75f) : e.trackColor;
                Handles.DrawSolidRectangleWithOutline(sourceBlock, WithAlpha(displayColor, e.muted ? 0.07f : 0.12f), WithAlpha(displayColor, e.muted ? 0.12f : 0.2f));
                Handles.DrawSolidRectangleWithOutline(block, WithAlpha(displayColor, e.muted ? 0.13f : 0.34f), WithAlpha(Color.Lerp(displayColor, Color.white, 0.25f), e.muted ? 0.45f : 0.9f));
                DrawGainBarHandles(block, e.trackColor, e.gain);
                DrawStateBadgesHandles(block, e.locked, e.muted);
            }

            DrawPlayheadLine(r, timelineLength);
            DrawExportStartLine(r, timelineLength);
            Handles.EndGUI();

            HandleOverviewInput(r, timelineLength);
        }

        private Rect BuildSourceRect(Rect r, CombineEntry entry, float timelineLength, float y, float height)
        {
            float xStart = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.timelineStart / timelineLength));
            float xEnd = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.SourceTimelineEnd / timelineLength));
            return new Rect(xStart, y, Mathf.Max(2f, xEnd - xStart), height);
        }

        private Rect BuildSegmentRect(Rect r, CombineEntry entry, float timelineLength, float y, float height)
        {
            float xStart = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.SegmentTimelineStart / timelineLength));
            float xEnd = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.SegmentTimelineEnd / timelineLength));
            return new Rect(xStart, y, Mathf.Max(2f, xEnd - xStart), height);
        }

        private void HandleOverviewInput(Rect r, float timelineLength)
        {
            Event evt = Event.current;
            if (evt == null)
            {
                return;
            }

            if (evt.type == EventType.MouseDown && r.Contains(evt.mousePosition))
            {
                float mouseTime = RectToTime(r, timelineLength, evt.mousePosition.x);
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var e = entries[i];
                    if (e.clip == null || e.SegmentLength <= 0f || e.locked)
                    {
                        continue;
                    }

                    float y = Mathf.Lerp(r.y + 5f, r.yMax - 15f, entries.Count <= 1 ? 0.5f : i / (float)(entries.Count - 1));
                    Rect block = BuildSegmentRect(r, e, timelineLength, y, 9f);
                    if (!block.Contains(evt.mousePosition))
                    {
                        continue;
                    }

                    overviewDragIndex = i;
                    overviewDragOffset = mouseTime - e.timelineStart;
                    overviewDragTimelineLength = timelineLength;
                    playhead = Mathf.Clamp(mouseTime, 0f, timelineLength);
                    evt.Use();
                    return;
                }

                SetPlayheadFromRect(r, timelineLength);
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && overviewDragIndex >= 0)
            {
                float dragLength = Mathf.Max(timelineLength, overviewDragTimelineLength);
                float mouseTime = RectToTime(r, dragLength, evt.mousePosition.x);
                entries[overviewDragIndex].timelineStart = Mathf.Max(0f, mouseTime - overviewDragOffset);
                playhead = Mathf.Clamp(playhead, 0f, CalculateTimelineLength());
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && overviewDragIndex >= 0)
            {
                overviewDragIndex = -1;
                evt.Use();
            }
        }

        private static float RectToTime(Rect r, float timelineLength, float x)
        {
            float pct = Mathf.InverseLerp(r.x, r.xMax, x);
            return Mathf.Clamp01(pct) * Mathf.Max(0f, timelineLength);
        }

        private static Color GenerateTrackColor(int index)
        {
            float hue = Mathf.Repeat((Mathf.Max(0, index) * 0.6180339f) + UnityEngine.Random.Range(0.02f, 0.18f), 1f);
            return Color.HSVToRGB(hue, 0.62f, 0.9f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void DrawEntryList(float timelineLength)
        {
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Add one or more AudioClips. Each row has its own in/out segment and timeline start, so gaps are just empty timeline space between rows.", MessageType.Info);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                DrawEntry(entries[i], i, timelineLength);
                EditorGUILayout.Space(4);
            }
        }

        private void DrawEntry(CombineEntry entry, int index, float timelineLength)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.expanded = EditorGUILayout.Foldout(entry.expanded, $"Clip {index + 1}", true);
                    entry.locked = GUILayout.Toggle(entry.locked, "Locked", EditorStyles.radioButton, GUILayout.Width(72));
                    entry.muted = GUILayout.Toggle(entry.muted, "Muted", EditorStyles.radioButton, GUILayout.Width(72));
                    GUILayout.FlexibleSpace();

                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = wasEnabled && !entry.locked && index > 0 && !entries[index - 1].locked;
                    if (GUILayout.Button("Up", GUILayout.Width(42)))
                    {
                        SwapEntries(index, index - 1);
                    }
                    GUI.enabled = wasEnabled && !entry.locked && index < entries.Count - 1 && !entries[index + 1].locked;
                    if (GUILayout.Button("Down", GUILayout.Width(52)))
                    {
                        SwapEntries(index, index + 1);
                    }
                    GUI.enabled = wasEnabled && !entry.locked;

                    if (GUILayout.Button("Remove", GUILayout.Width(68)))
                    {
                        entries.RemoveAt(index);
                        return;
                    }

                    GUI.enabled = wasEnabled;
                }

                if (!entry.expanded)
                {
                    DrawTrack(entry, index, timelineLength, 32f);
                    return;
                }

                EditorGUI.BeginDisabledGroup(entry.locked);
                AudioClip newClip = (AudioClip)EditorGUILayout.ObjectField("Clip", entry.clip, typeof(AudioClip), false);
                if (newClip != entry.clip)
                {
                    AssignClip(entry, newClip);
                }

                if (entry.clip == null)
                {
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.HelpBox("Drop an AudioClip into this row.", MessageType.Info);
                    return;
                }

                ClampEntry(entry);

                EditorGUILayout.LabelField($"Source Length: {entry.clip.length:F3}s | Freq: {entry.clip.frequency} | Ch: {entry.clip.channels}");
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.trackColor = EditorGUILayout.ColorField("Track Color", entry.trackColor);
                    if (GUILayout.Button("Random", GUILayout.Width(78)))
                    {
                        entry.trackColor = GenerateTrackColor(index + entries.Count + 1);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.gain = EditorGUILayout.Slider("Clip Gain", entry.gain, 0f, 1.25f);
                    if (GUILayout.Button("-3 dB", GUILayout.Width(58)))
                    {
                        entry.gain = Mathf.Clamp01(entry.gain * 0.7079458f);
                    }

                    if (GUILayout.Button("0 dB", GUILayout.Width(58)))
                    {
                        entry.gain = 1f;
                    }
                }

                EditorGUILayout.MinMaxSlider(new GUIContent("Segment (In/Out)"), ref entry.inTime, ref entry.outTime, 0f, entry.clip.length);

                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.inTime = EditorGUILayout.FloatField("In (sec)", entry.inTime);
                    entry.outTime = EditorGUILayout.FloatField("Out (sec)", entry.outTime);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.timelineStart = Mathf.Max(0f, EditorGUILayout.FloatField("Clip Start", entry.timelineStart));
                    EditorGUILayout.LabelField($"Seg Start: {entry.SegmentTimelineStart:F3}s", GUILayout.Width(135));
                    EditorGUILayout.LabelField($"Length: {entry.SegmentLength:F3}s", GUILayout.Width(120));
                    EditorGUILayout.LabelField($"End: {entry.SegmentTimelineEnd:F3}s", GUILayout.Width(110));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set Start = Playhead", GUILayout.Height(22)))
                    {
                        entry.timelineStart = Mathf.Max(0f, playhead - entry.inTime);
                    }

                    if (GUILayout.Button("Playhead = Start", GUILayout.Height(22)))
                    {
                        playhead = Mathf.Clamp(entry.SegmentTimelineStart, 0f, timelineLength);
                    }

                    GUI.enabled = index > 0;
                    if (GUILayout.Button("After Previous + Gap", GUILayout.Height(22)))
                    {
                        entry.timelineStart = Mathf.Max(0f, entries[index - 1].SegmentTimelineEnd + defaultGapSeconds - entry.inTime);
                    }
                    GUI.enabled = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"Nudge This + Later -{nudgeSeconds:F2}s", GUILayout.Height(22)))
                    {
                        ShiftEntriesFrom(index, -nudgeSeconds);
                    }

                    if (GUILayout.Button($"Nudge This + Later +{nudgeSeconds:F2}s", GUILayout.Height(22)))
                    {
                        ShiftEntriesFrom(index, nudgeSeconds);
                    }
                }

                EditorGUI.EndDisabledGroup();

                DrawTrack(entry, index, timelineLength, 92f);

                if (entry.waveform != null)
                {
                    float effectivePeak = entry.waveform.peak * entry.gain;
                    EditorGUILayout.LabelField($"Segment Peak: {entry.waveform.peak:F3} | After Gain: {effectivePeak:F3}", EditorStyles.miniLabel);
                }
            }
        }

        private void DrawTrack(CombineEntry entry, int index, float timelineLength, float height)
        {
            Rect r = GUILayoutUtility.GetRect(10, 10000, height, height);
            EditorGUI.DrawRect(r, kTrackBackground);

            if (entry.clip == null || entry.SegmentLength <= 0f || timelineLength <= 0f)
            {
                EditorGUI.LabelField(r, "No playable segment.");
                return;
            }

            float sourceXStart = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.timelineStart / timelineLength));
            float sourceXEnd = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.SourceTimelineEnd / timelineLength));
            Rect sourceRect = new Rect(sourceXStart, r.y, Mathf.Max(1f, sourceXEnd - sourceXStart), r.height);
            Color displayColor = entry.muted ? Color.Lerp(entry.trackColor, Color.gray, 0.75f) : entry.trackColor;
            EditorGUI.DrawRect(sourceRect, WithAlpha(displayColor, entry.muted ? 0.05f : 0.08f));

            float xStart = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.SegmentTimelineStart / timelineLength));
            float xEnd = Mathf.Lerp(r.x, r.xMax, Mathf.Clamp01(entry.SegmentTimelineEnd / timelineLength));
            Rect segRect = new Rect(xStart, r.y, Mathf.Max(1f, xEnd - xStart), r.height);
            EditorGUI.DrawRect(segRect, WithAlpha(displayColor, entry.muted ? 0.1f : 0.22f));
            DrawGainBar(segRect, displayColor, entry.gain);

            int width = Mathf.Max(1, Mathf.RoundToInt(segRect.width));
            if (entry.waveform == null ||
                entry.lastClip != entry.clip ||
                entry.lastWaveWidth != width ||
                !Mathf.Approximately(entry.lastWaveIn, entry.inTime) ||
                !Mathf.Approximately(entry.lastWaveOut, entry.outTime))
            {
                entry.waveform = BuildWaveformCache(entry.clip, entry.inTime, entry.outTime, width, 2048);
                entry.lastClip = entry.clip;
                entry.lastWaveWidth = width;
                entry.lastWaveIn = entry.inTime;
                entry.lastWaveOut = entry.outTime;
            }

            Handles.BeginGUI();
            Handles.color = Color.Lerp(displayColor, Color.white, entry.muted ? 0.2f : 0.45f);

            if (entry.waveform != null && entry.waveform.min != null && entry.waveform.max != null)
            {
                const float waveformScale = 1f;
                float pad = 2f;
                float half = (segRect.height * 0.5f) - pad;

                for (int x = 0; x < entry.waveform.width && segRect.x + x < segRect.xMax; x++)
                {
                    float min = Mathf.Clamp(entry.waveform.min[x] * entry.gain, -1f, 1f);
                    float max = Mathf.Clamp(entry.waveform.max[x] * entry.gain, -1f, 1f);
                    float yTop = segRect.center.y - (max * half * waveformScale);
                    float yBottom = segRect.center.y - (min * half * waveformScale);
                    yTop = Mathf.Clamp(yTop, segRect.y + pad, segRect.yMax - pad);
                    yBottom = Mathf.Clamp(yBottom, segRect.y + pad, segRect.yMax - pad);
                    float px = segRect.x + x;
                    Handles.DrawLine(new Vector3(px, yTop), new Vector3(px, yBottom));
                }
            }

            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            Handles.DrawLine(new Vector3(r.x, r.center.y), new Vector3(r.xMax, r.center.y));
            Handles.DrawSolidRectangleWithOutline(sourceRect, new Color(0f, 0f, 0f, 0f), WithAlpha(displayColor, entry.muted ? 0.12f : 0.22f));
            Handles.DrawSolidRectangleWithOutline(segRect, new Color(0f, 0f, 0f, 0f), WithAlpha(Color.Lerp(displayColor, Color.white, 0.25f), entry.muted ? 0.45f : 0.85f));
            DrawStateBadgesHandles(segRect, entry.locked, entry.muted);
            DrawPlayheadLine(r, timelineLength);
            DrawExportStartLine(r, timelineLength);
            Handles.EndGUI();

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
            };
            string state = entry.locked && entry.muted ? " [LOCKED, MUTED]" : entry.locked ? " [LOCKED]" : entry.muted ? " [MUTED]" : string.Empty;
            GUI.Label(new Rect(r.x + 6f, r.y + 3f, r.width - 12f, 18f), $"{index + 1}: {entry.clip.name}{state}", labelStyle);

            if (!entry.locked && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                SetPlayheadFromRect(r, timelineLength);
                Event.current.Use();
            }
        }

        private void DrawPlayheadLine(Rect r, float timelineLength)
        {
            float xHead = Mathf.Lerp(r.x, r.xMax, timelineLength > 0f ? Mathf.Clamp01(playhead / timelineLength) : 0f);
            Handles.color = kPlayheadColor;
            Handles.DrawLine(new Vector3(xHead, r.y), new Vector3(xHead, r.yMax));
        }

        private void DrawExportStartLine(Rect r, float timelineLength)
        {
            if (!hasExportStartBookend)
            {
                return;
            }

            float xHead = Mathf.Lerp(r.x, r.xMax, timelineLength > 0f ? Mathf.Clamp01(exportStartBookend / timelineLength) : 0f);
            Handles.color = kExportStartColor;
            Handles.DrawLine(new Vector3(xHead, r.y), new Vector3(xHead, r.yMax));
            Rect cap = new Rect(xHead - 4f, r.y, 8f, 5f);
            Handles.DrawSolidRectangleWithOutline(cap, kExportStartColor, new Color(0f, 0f, 0f, 0f));
        }

        private static void DrawGainBar(Rect segmentRect, Color trackColor, float gain)
        {
            float barWidth = Mathf.Min(6f, Mathf.Max(3f, segmentRect.width));
            Rect rail = new Rect(segmentRect.x, segmentRect.y, barWidth, segmentRect.height);
            EditorGUI.DrawRect(rail, new Color(0f, 0f, 0f, 0.38f));

            float normalizedGain = Mathf.Clamp01(gain);
            float fillHeight = Mathf.Max(1f, segmentRect.height * normalizedGain);
            Rect fill = new Rect(rail.x, rail.yMax - fillHeight, rail.width, fillHeight);
            EditorGUI.DrawRect(fill, Color.Lerp(trackColor, Color.white, 0.2f));

            if (gain > 1f)
            {
                Rect hot = new Rect(rail.x, rail.y, rail.width, 3f);
                EditorGUI.DrawRect(hot, new Color(1f, 0.5f, 0.2f, 0.95f));
            }
        }

        private static void DrawGainBarHandles(Rect segmentRect, Color trackColor, float gain)
        {
            float barWidth = Mathf.Min(5f, Mathf.Max(2f, segmentRect.width));
            Rect rail = new Rect(segmentRect.x, segmentRect.y, barWidth, segmentRect.height);
            Handles.DrawSolidRectangleWithOutline(rail, new Color(0f, 0f, 0f, 0.32f), new Color(0f, 0f, 0f, 0f));

            float normalizedGain = Mathf.Clamp01(gain);
            float fillHeight = Mathf.Max(1f, segmentRect.height * normalizedGain);
            Rect fill = new Rect(rail.x, rail.yMax - fillHeight, rail.width, fillHeight);
            Handles.DrawSolidRectangleWithOutline(fill, Color.Lerp(trackColor, Color.white, 0.2f), new Color(0f, 0f, 0f, 0f));

            if (gain > 1f)
            {
                Rect hot = new Rect(rail.x, rail.y, rail.width, 2f);
                Handles.DrawSolidRectangleWithOutline(hot, new Color(1f, 0.5f, 0.2f, 0.95f), new Color(0f, 0f, 0f, 0f));
            }
        }

        private static void DrawStateBadgesHandles(Rect segmentRect, bool locked, bool muted)
        {
            if (muted)
            {
                Rect muteOverlay = new Rect(segmentRect.x, segmentRect.y, segmentRect.width, segmentRect.height);
                Handles.DrawSolidRectangleWithOutline(muteOverlay, new Color(0f, 0f, 0f, 0.22f), new Color(0.55f, 0.55f, 0.55f, 0.5f));
            }

            if (locked)
            {
                Rect topLock = new Rect(segmentRect.x, segmentRect.y, segmentRect.width, 3f);
                Rect leftLock = new Rect(segmentRect.x, segmentRect.y, 3f, segmentRect.height);
                Color lockColor = new Color(1f, 0.78f, 0.25f, 0.95f);
                Handles.DrawSolidRectangleWithOutline(topLock, lockColor, new Color(0f, 0f, 0f, 0f));
                Handles.DrawSolidRectangleWithOutline(leftLock, lockColor, new Color(0f, 0f, 0f, 0f));
            }
        }

        private void DrawExportUI()
        {
            FP_Utility_Editor.DrawUILine(FP_Utility_Editor.OkayColor);
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
            exportFileName = EditorGUILayout.TextField("File Name (no ext)", exportFileName);
            exportFolder = EditorGUILayout.TextField("Folder", exportFolder);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create In-Memory Combined Clip", GUILayout.Height(26)))
                {
                    var combined = BuildCombinedClip("CombinedPreview");
                    if (combined != null)
                    {
                        Selection.activeObject = combined;
                    }
                }

                if (GUILayout.Button("Save Combined as .wav in Assets", GUILayout.Height(26)))
                {
                    SaveCombinedToAssets();
                }
            }
        }

        private void DrawAudioDropArea(float timelineLength)
        {
            Rect dropRect = GUILayoutUtility.GetRect(10, 10000, 46, 46);
            Event evt = Event.current;
            bool hovering = dropRect.Contains(evt.mousePosition);
            bool canDrop = hovering && DragContainsSupportedAudioClips();

            EditorGUI.DrawRect(dropRect, canDrop ? new Color(0.18f, 0.28f, 0.22f) : new Color(0.11f, 0.11f, 0.11f));
            Handles.BeginGUI();
            Handles.DrawSolidRectangleWithOutline(dropRect, new Color(0f, 0f, 0f, 0f), canDrop ? FP_Utility_Editor.OkayColor : new Color(0.35f, 0.35f, 0.35f, 0.9f));
            Handles.EndGUI();

            GUIStyle labelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            GUI.Label(dropRect, "Drop AudioClip(s) Here", labelStyle);

            if (!hovering || !canDrop)
            {
                return;
            }

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddDraggedAudioClips(DragAndDrop.objectReferences, timelineLength);
                evt.Use();
            }
        }

        private bool DragContainsSupportedAudioClips()
        {
            UnityEngine.Object[] dragged = DragAndDrop.objectReferences;
            if (dragged == null || dragged.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < dragged.Length; i++)
            {
                AudioClip clip = dragged[i] as AudioClip;
                if (clip != null && IsSupportedAudioAsset(clip))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddDraggedAudioClips(UnityEngine.Object[] draggedObjects, float timelineLength)
        {
            if (draggedObjects == null)
            {
                return;
            }

            float nextStart = timelineLength > 0.01f ? timelineLength + defaultGapSeconds : 0f;
            int added = 0;

            for (int i = 0; i < draggedObjects.Length; i++)
            {
                AudioClip clip = draggedObjects[i] as AudioClip;
                if (clip == null || !IsSupportedAudioAsset(clip))
                {
                    continue;
                }

                var entry = new CombineEntry { trackColor = GenerateTrackColor(entries.Count + added) };
                AssignClip(entry, clip);
                entry.timelineStart = Mathf.Max(0f, nextStart);
                entries.Add(entry);
                nextStart = entry.SegmentTimelineEnd + defaultGapSeconds;
                added++;
            }

            if (added > 0)
            {
                Repaint();
            }
        }

        private void AddSelectedAudioClips(bool appendToEnd)
        {
            UnityEngine.Object[] selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                if (entries.Count == 0)
                {
                    entries.Add(new CombineEntry());
                }
                return;
            }

            bool hasPlayableEntries = HasPlayableEntries();
            float currentLength = CalculateTimelineLength();
            float nextStart = appendToEnd && hasPlayableEntries ? currentLength + defaultGapSeconds : currentLength;
            int added = 0;

            foreach (UnityEngine.Object obj in selected)
            {
                AudioClip clip = obj as AudioClip;
                if (clip == null || !IsSupportedAudioAsset(clip))
                {
                    continue;
                }

                var entry = new CombineEntry { trackColor = GenerateTrackColor(entries.Count + added) };
                AssignClip(entry, clip);
                entry.timelineStart = Mathf.Max(0f, nextStart);
                entries.Add(entry);
                nextStart = entry.SegmentTimelineEnd + defaultGapSeconds;
                added++;
            }

            if (entries.Count == 0 && added == 0)
            {
                entries.Add(new CombineEntry());
            }
        }

        private static bool IsSupportedAudioAsset(AudioClip clip)
        {
            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            return extension == ".wav" || extension == ".mp3" || extension == ".ogg" || extension == ".aiff" || extension == ".aif";
        }

        private void AssignClip(CombineEntry entry, AudioClip clip)
        {
            bool hadOtherClips = HasOtherClips(entry);
            if (entry.trackColor.a <= 0f)
            {
                entry.trackColor = GenerateTrackColor(entries.IndexOf(entry));
            }

            if (!entry.gainInitialized)
            {
                entry.gain = 1f;
                entry.gainInitialized = true;
            }

            entry.clip = clip;
            entry.inTime = 0f;
            entry.outTime = clip == null ? 0f : clip.length;
            entry.waveform = null;
            entry.lastClip = null;
            entry.lastWaveWidth = 0;

            if (clip != null && !hadOtherClips)
            {
                outputFrequency = clip.frequency;
                outputChannels = Mathf.Clamp(clip.channels, 1, 2);
            }
        }

        private bool HasPlayableEntries()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].clip != null && entries[i].SegmentLength > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasOtherClips(CombineEntry ignoredEntry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!ReferenceEquals(entries[i], ignoredEntry) && entries[i].clip != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClampEntry(CombineEntry entry)
        {
            if (entry.trackColor.a <= 0f)
            {
                entry.trackColor = GenerateTrackColor(entries.IndexOf(entry));
            }

            if (!entry.gainInitialized)
            {
                entry.gain = 1f;
                entry.gainInitialized = true;
            }

            entry.gain = Mathf.Clamp(entry.gain, 0f, 1.25f);

            if (entry.clip == null)
            {
                entry.inTime = 0f;
                entry.outTime = 0f;
                entry.timelineStart = Mathf.Max(0f, entry.timelineStart);
                return;
            }

            float clipLen = Mathf.Max(0f, entry.clip.length);
            if (entry.outTime <= 0f || entry.outTime > clipLen)
            {
                entry.outTime = clipLen;
            }

            entry.inTime = Mathf.Clamp(entry.inTime, 0f, clipLen);
            entry.outTime = Mathf.Clamp(entry.outTime, 0f, clipLen);
            if (entry.outTime < entry.inTime)
            {
                entry.outTime = entry.inTime;
            }

            entry.timelineStart = Mathf.Max(0f, entry.timelineStart);
        }

        private void AutoLayoutEntries()
        {
            float cursor = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                ClampEntry(entries[i]);
                if (entries[i].locked)
                {
                    cursor = Mathf.Max(cursor, entries[i].SegmentTimelineEnd + defaultGapSeconds);
                    continue;
                }

                entries[i].timelineStart = Mathf.Max(0f, cursor - entries[i].inTime);
                cursor = entries[i].SegmentTimelineEnd + defaultGapSeconds;
            }

            playhead = Mathf.Clamp(playhead, 0f, CalculateTimelineLength());
        }

        private void ShiftEntriesFrom(int startIndex, float delta)
        {
            if (startIndex < 0 || startIndex >= entries.Count)
            {
                return;
            }

            float minStart = float.MaxValue;
            for (int i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].locked)
                {
                    continue;
                }

                minStart = Mathf.Min(minStart, entries[i].timelineStart);
            }

            if (minStart == float.MaxValue)
            {
                return;
            }

            if (delta < 0f)
            {
                delta = Mathf.Max(delta, -minStart);
            }

            for (int i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].locked)
                {
                    continue;
                }

                entries[i].timelineStart = Mathf.Max(0f, entries[i].timelineStart + delta);
            }

            playhead = Mathf.Clamp(playhead, 0f, CalculateTimelineLength());
        }

        private void SwapEntries(int a, int b)
        {
            if (a < 0 || b < 0 || a >= entries.Count || b >= entries.Count)
            {
                return;
            }

            CombineEntry tmp = entries[a];
            entries[a] = entries[b];
            entries[b] = tmp;
        }

        private float CalculateTimelineLength()
        {
            float length = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                ClampEntry(entries[i]);
                length = Mathf.Max(length, entries[i].TimelineEnd);
            }

            return length;
        }

        private float CalculateMixTimelineLength()
        {
            return Mathf.Max(0f, CalculateMixTimelineEnd() - GetExportStartTime());
        }

        private float CalculateMixTimelineEnd()
        {
            float length = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                ClampEntry(entries[i]);
                if (entries[i].muted)
                {
                    continue;
                }

                length = Mathf.Max(length, entries[i].TimelineEnd);
            }

            return length;
        }

        private float GetExportStartTime()
        {
            return hasExportStartBookend ? Mathf.Max(0f, exportStartBookend) : 0f;
        }

        private void SetPlayheadFromRect(Rect r, float timelineLength)
        {
            float pct = Mathf.InverseLerp(r.x, r.xMax, Event.current.mousePosition.x);
            playhead = Mathf.Clamp01(pct) * Mathf.Max(0f, timelineLength);
            Repaint();
        }

        private WaveformCache BuildWaveformCache(AudioClip clip, float inSec, float outSec, int width, int chunkFrames)
        {
            if (clip == null || width <= 0)
            {
                return null;
            }

            int channels = clip.channels;
            float startSec = Mathf.Clamp(inSec, 0f, clip.length);
            float endSec = Mathf.Clamp(outSec <= 0f ? clip.length : outSec, 0f, clip.length);
            if (endSec < startSec)
            {
                endSec = startSec;
            }

            int startFrame = Mathf.Clamp(Mathf.FloorToInt(startSec * clip.frequency), 0, clip.samples);
            int endFrame = Mathf.Clamp(Mathf.CeilToInt(endSec * clip.frequency), 0, clip.samples);
            int totalFrames = Mathf.Max(0, endFrame - startFrame);

            var min = new float[width];
            var max = new float[width];

            if (totalFrames == 0)
            {
                return new WaveformCache { width = width, min = min, max = max, peak = 0f };
            }

            int framesPerColumn = Mathf.Max(1, Mathf.CeilToInt(totalFrames / (float)width));
            int bufferFrames = Mathf.Max(1, Mathf.Min(chunkFrames, framesPerColumn));
            float[] buffer = new float[bufferFrames * channels];
            float peak = 0f;

            for (int x = 0; x < width; x++)
            {
                int colStart = startFrame + x * framesPerColumn;
                int colEnd = Mathf.Min(endFrame, colStart + framesPerColumn);
                if (colStart >= colEnd)
                {
                    min[x] = 0f;
                    max[x] = 0f;
                    continue;
                }

                float curMin = 1f;
                float curMax = -1f;
                int cursor = colStart;

                while (cursor < colEnd)
                {
                    int toRead = Mathf.Min(bufferFrames, colEnd - cursor);
                    clip.GetData(buffer, cursor);
                    int interleavedCount = toRead * channels;

                    for (int i = 0; i < interleavedCount; i += channels)
                    {
                        float sample = 0f;
                        for (int c = 0; c < channels; c++)
                        {
                            sample += buffer[i + c];
                        }

                        sample /= channels;
                        curMin = Mathf.Min(curMin, sample);
                        curMax = Mathf.Max(curMax, sample);
                        peak = Mathf.Max(peak, Mathf.Abs(sample));
                    }

                    cursor += toRead;
                }

                min[x] = curMin;
                max[x] = curMax;
            }

            return new WaveformCache { width = width, min = min, max = max, peak = peak };
        }

        private AudioClip BuildCombinedClip(string clipName)
        {
            float totalLength = CalculateMixTimelineLength();
            if (totalLength <= 0f)
            {
                Debug.LogWarning("No unmuted audio segments to combine.");
                return null;
            }

            float exportStart = GetExportStartTime();
            int hz = Mathf.Max(8000, outputFrequency);
            int ch = Mathf.Clamp(outputChannels, 1, 2);
            int totalFrames = Mathf.Max(1, Mathf.CeilToInt(totalLength * hz));
            float[] dst = new float[totalFrames * ch];

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.clip == null || entry.SegmentLength <= 0f || entry.muted)
                {
                    continue;
                }

                MixEntryIntoBuffer(entry, dst, totalFrames, hz, ch, exportStart);
            }

            float peak = 0f;
            for (int i = 0; i < dst.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(dst[i]));
            }

            if (peak > 1f && normalizeIfClipping)
            {
                float gain = 1f / peak;
                for (int i = 0; i < dst.Length; i++)
                {
                    dst[i] *= gain;
                }
            }
            else
            {
                for (int i = 0; i < dst.Length; i++)
                {
                    dst[i] = Mathf.Clamp(dst[i], -1f, 1f);
                }
            }

            var outClip = AudioClip.Create(clipName, totalFrames, ch, hz, false);
            outClip.SetData(dst, 0);
            return outClip;
        }

        private void MixEntryIntoBuffer(CombineEntry entry, float[] dst, int totalFrames, int hz, int ch, float exportStartTime)
        {
            AudioClip clip = entry.clip;
            int srcCh = clip.channels;
            int srcFrames = clip.samples;
            float[] src = new float[srcFrames * srcCh];
            clip.GetData(src, 0);

            float srcStartSec = Mathf.Clamp(entry.inTime, 0f, clip.length);
            float srcEndSec = Mathf.Clamp(entry.outTime <= 0f ? clip.length : entry.outTime, 0f, clip.length);
            if (srcEndSec <= srcStartSec)
            {
                return;
            }

            float absoluteStart = entry.SegmentTimelineStart;
            float absoluteEnd = entry.SegmentTimelineEnd;
            float mixStart = Mathf.Max(absoluteStart, exportStartTime);
            float mixEnd = Mathf.Min(absoluteEnd, exportStartTime + (totalFrames / (float)hz));
            if (mixEnd <= mixStart)
            {
                return;
            }

            int dstStart = Mathf.Clamp(Mathf.RoundToInt((mixStart - exportStartTime) * hz), 0, totalFrames);
            int dstEnd = Mathf.Clamp(Mathf.CeilToInt((mixEnd - exportStartTime) * hz), 0, totalFrames);

            for (int frame = dstStart; frame < dstEnd; frame++)
            {
                float absoluteTime = exportStartTime + (frame / (float)hz);
                float segmentTime = absoluteTime - absoluteStart;
                float srcFrame = (srcStartSec + segmentTime) * clip.frequency;

                for (int outCh = 0; outCh < ch; outCh++)
                {
                    float sample = SampleSource(src, srcFrames, srcCh, srcFrame, outCh, ch) * entry.gain;
                    int dstIndex = frame * ch + outCh;
                    dst[dstIndex] += sample;
                }
            }
        }

        private static float SampleSource(float[] src, int srcFrames, int srcChannels, float frameFloat, int outChannel, int outChannels)
        {
            if (srcFrames <= 0 || srcChannels <= 0)
            {
                return 0f;
            }

            int f0 = Mathf.Clamp(Mathf.FloorToInt(frameFloat), 0, srcFrames - 1);
            int f1 = Mathf.Clamp(f0 + 1, 0, srcFrames - 1);
            float t = Mathf.Clamp01(frameFloat - f0);

            float a = ReadMappedChannel(src, f0, srcChannels, outChannel, outChannels);
            float b = ReadMappedChannel(src, f1, srcChannels, outChannel, outChannels);
            return Mathf.Lerp(a, b, t);
        }

        private static float ReadMappedChannel(float[] src, int frame, int srcChannels, int outChannel, int outChannels)
        {
            if (srcChannels == outChannels && outChannel < srcChannels)
            {
                return src[frame * srcChannels + outChannel];
            }

            if (srcChannels == 1)
            {
                return src[frame * srcChannels];
            }

            if (outChannels > 1 && outChannel < srcChannels)
            {
                return src[frame * srcChannels + outChannel];
            }

            float sum = 0f;
            int baseIndex = frame * srcChannels;
            for (int c = 0; c < srcChannels; c++)
            {
                sum += src[baseIndex + c];
            }

            return sum / srcChannels;
        }

        private void PlayCombinedFrom(float startSec)
        {
            var combined = BuildCombinedClip("CombinedPreview");
            if (combined == null)
            {
                return;
            }

            float exportStart = GetExportStartTime();
            float mixEnd = CalculateMixTimelineEnd();
            float start = Mathf.Clamp(Mathf.Max(startSec, exportStart), exportStart, mixEnd);
            int startSample = Mathf.Clamp(Mathf.FloorToInt((start - exportStart) * combined.frequency), 0, combined.samples);

            EditorStopAll();
            isPlayingCombined = false;
            EditorApplication.update -= OnEditorUpdate;

            EditorPreview(combined, startSample, false, 1f);
            combinedPlayStartTime = EditorApplication.timeSinceStartup;
            combinedPlayStartOffset = start;
            isPlayingCombined = true;
            playhead = start;
            EditorApplication.update += OnEditorUpdate;
        }

        private void SaveCombinedToAssets()
        {
            Directory.CreateDirectory(exportFolder);

            var combined = BuildCombinedClip(MakeSafeFilename(exportFileName));
            if (combined == null)
            {
                return;
            }

            byte[] wavBytes = FuzzPhyte.Utility.Audio.FP_AudioUtils.ConvertAudioClipToWAV(combined);
            string safeName = MakeSafeFilename(exportFileName);
            string outPath = $"{exportFolder}/{safeName}.wav";

            File.WriteAllBytes(outPath, wavBytes);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var imported = AssetDatabase.LoadAssetAtPath<AudioClip>(outPath);
            if (imported != null)
            {
                Selection.activeObject = imported;
            }
        }

        private static string MakeSafeFilename(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? "AudioCombined" : name.Trim();
        }

        #region Editor Preview via AudioUtil
        private static Type audioUtilType;
        private static MethodInfo miPlayClip3;
        private static MethodInfo miPlayClip4;
        private static MethodInfo miPlayPreviewClip;
        private static MethodInfo miStopAllClips;
        private static MethodInfo miStopAllPreview;
        private static MethodInfo miSetPreviewVolume;

        private static void EnsureAudioUtil()
        {
            if (audioUtilType != null)
            {
                return;
            }

            var asm = typeof(AudioImporter).Assembly;
            audioUtilType = asm.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null)
            {
                return;
            }

            var methods = audioUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var m in methods)
            {
                if (m.Name == "PlayClip")
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 && p[0].ParameterType == typeof(AudioClip))
                    {
                        miPlayClip3 = m;
                    }
                    else if (p.Length == 4 && p[0].ParameterType == typeof(AudioClip) && p[3].ParameterType == typeof(float))
                    {
                        miPlayClip4 = m;
                    }
                }
                else if (m.Name == "PlayPreviewClip")
                {
                    miPlayPreviewClip = m;
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
                    {
                        miSetPreviewVolume = m;
                    }
                }
            }
        }

        private static void EditorPreview(AudioClip clip, int startSample = 0, bool loop = false, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioUtil();

            try
            {
                miSetPreviewVolume?.Invoke(null, new object[] { Mathf.Clamp01(volume) });
            }
            catch
            {
                // Ignore Unity-version preview volume differences.
            }

            if (miPlayClip4 != null)
            {
                miPlayClip4.Invoke(null, new object[] { clip, startSample, loop, Mathf.Clamp01(volume) });
                return;
            }

            if (miPlayClip3 != null)
            {
                miPlayClip3.Invoke(null, new object[] { clip, startSample, loop });
                return;
            }

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
            if (miStopAllClips != null)
            {
                miStopAllClips.Invoke(null, null);
                return;
            }

            if (miStopAllPreview != null)
            {
                miStopAllPreview.Invoke(null, null);
            }
        }
        #endregion
    }
}
