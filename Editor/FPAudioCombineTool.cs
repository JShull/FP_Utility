// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using UnityEngine;
    using UnityEditor;
    using System;
    using System.IO;
    using System.Reflection;
    using System.Collections.Generic;
    using FuzzPhyte.Utility.Audio;

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
            public bool fadeInEnabled;
            public float fadeInDuration;
            public float fadeInPower = 1f;
            public bool fadeOutEnabled;
            public float fadeOutDuration;
            public float fadeOutPower = 1f;
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
            public bool unavailable;
            public string unavailableReason;
        }

        private readonly List<CombineEntry> entries = new List<CombineEntry>();
        private Vector2 parameterScroll;
        private Vector2 viewerScroll;
        private Vector2 settingsScroll;
        private int selectedEntryIndex = -1;
        private int listDragIndex = -1;

        private float playhead;
        private bool autoAdvancePlayhead = true;
        private bool isPlayingCombined;
        private AudioClip activeCombinedPreviewClip;
        private double combinedPlayStartTime;
        private float combinedPlayStartOffset;
        private int overviewDragIndex = -1;
        private float overviewDragOffset;
        private float overviewDragTimelineLength;
        private bool overviewPlayheadDragging;
        private int trackDragIndex = -1;
        private float trackDragOffset;
        private float trackDragTimelineLength;
        private int fadeDragIndex = -1;
        private bool fadeDragIsOut;
        private int fadeCurveDragIndex = -1;
        private bool fadeCurveDragIsOut;

        private int outputFrequency = 44100;
        private int outputChannels = 2;
        private bool normalizeIfClipping = true;
        private float defaultGapSeconds = 2f;
        private float nudgeSeconds = 0.25f;
        private FPAudioCombineData activeMixData;
        private string mixDataFileName = "AudioCombineData";
        private string mixDataFolder = "Assets/_FPUtility/AudioCombineData";
        private bool hasExportStartBookend;
        private float exportStartBookend;
        private bool hasExportEndBookend;
        private float exportEndBookend;

        private string exportFileName = "AudioCombined";
        private string exportFolder = "Assets/_FPUtility/AudioExports";
        private const string PreviewAssetFolder = "Assets/_FPUtility/AudioPreview";
        private const string PreviewAssetPath = PreviewAssetFolder + "/__FPAudioCombinePreview.wav";

        private static readonly Color kTrackBackground = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color kPlayheadColor = new Color(1f, 0.72f, 0.25f, 0.95f);
        private static readonly Color kExportStartColor = new Color(0.25f, 0.75f, 1f, 0.95f);
        private const float WorkspacePadding = 4f;
        private const float PanelGap = 8f;
        private const float ParameterPanelWidth = 352f;
        private const float HeaderHeight = 250f;
        private const float FooterHeight = 164f;
        private const float ViewerTrackHeight = 252f;
        private const float StackRowGap = 6f;
        private const float ViewerScrollbarGutter = 16f;

        [MenuItem("FuzzPhyte/Utility/Audio/Combine Tool", priority = FuzzPhyte.Utility.FP_UtilityData.MENU_UTILITY_AUDIO + 1)]
        public static void ShowWindow()
        {
            var win = GetWindow<FPAudioCombineTool>("FP Audio Combine Tool");
            win.minSize = new Vector2(820, 560);
            win.AddSelectedAudioClips(false);
        }

        private void OnDisable()
        {
            EditorStopAll();
            activeCombinedPreviewClip = null;
            isPlayingCombined = false;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!isPlayingCombined)
            {
                return;
            }

            float totalLength = GetExportEndTime();
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
                activeCombinedPreviewClip = null;
                EditorApplication.update -= OnEditorUpdate;
                Repaint();
            }
        }

        private void OnGUI()
        {
            GUILayout.Space(4f);
            float timelineLength = Mathf.Max(0.01f, CalculateTimelineLength());
            playhead = Mathf.Clamp(playhead, 0f, timelineLength);
            if (hasExportStartBookend)
            {
                exportStartBookend = Mathf.Clamp(exportStartBookend, 0f, timelineLength);
            }
            if (hasExportEndBookend)
            {
                exportEndBookend = Mathf.Clamp(exportEndBookend, 0f, timelineLength);
            }
            EnsureValidExportBookends();

            if (entries.Count == 0)
            {
                selectedEntryIndex = -1;
            }
            else if (selectedEntryIndex < 0 || selectedEntryIndex >= entries.Count)
            {
                selectedEntryIndex = 0;
            }

            if (Event.current.type == EventType.MouseUp)
            {
                listDragIndex = -1;
                trackDragIndex = -1;
                fadeDragIndex = -1;
                fadeCurveDragIndex = -1;
                overviewPlayheadDragging = false;
            }

            DrawWorkspace(timelineLength);
        }

        private void DrawWorkspace(float timelineLength)
        {
            Rect previousRect = GUILayoutUtility.GetLastRect();
            float workspaceTop = previousRect.yMax + 4f;
            Rect workspaceRect = new Rect(
                WorkspacePadding,
                workspaceTop,
                Mathf.Max(100f, position.width - (WorkspacePadding * 2f)),
                Mathf.Max(100f, position.height - workspaceTop - WorkspacePadding));

            float leftWidth = Mathf.Clamp(ParameterPanelWidth, 300f, Mathf.Max(300f, workspaceRect.width - 360f - PanelGap));
            Rect parameterRect = new Rect(workspaceRect.x, workspaceRect.y, leftWidth, workspaceRect.height);
            Rect viewerRect = new Rect(parameterRect.xMax + PanelGap, workspaceRect.y, Mathf.Max(100f, workspaceRect.xMax - parameterRect.xMax - PanelGap), workspaceRect.height);

            DrawParameterPanelContainer(parameterRect, timelineLength);
            DrawViewerPanelContainer(viewerRect, timelineLength);
        }

        private void DrawParameterPanelContainer(Rect rect, float timelineLength)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            float footerHeight = Mathf.Min(FooterHeight, Mathf.Max(90f, innerRect.height * 0.34f));
            float headerHeight = Mathf.Min(HeaderHeight, Mathf.Max(118f, innerRect.height * 0.34f));
            Rect headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, headerHeight);
            Rect footerRect = new Rect(innerRect.x, innerRect.yMax - footerHeight, innerRect.width, footerHeight);
            Rect stackRect = new Rect(innerRect.x, headerRect.yMax + PanelGap, innerRect.width, Mathf.Max(60f, footerRect.y - headerRect.yMax - (PanelGap * 2f)));

            GUI.BeginGroup(headerRect);
            Rect settingsViewRect = new Rect(0f, 0f, Mathf.Max(10f, headerRect.width - 16f), 372f);
            settingsScroll = GUI.BeginScrollView(new Rect(0f, 0f, headerRect.width, headerRect.height), settingsScroll, settingsViewRect);
            GUILayout.BeginArea(settingsViewRect);
            EditorGUILayout.LabelField("Combine Settings", EditorStyles.boldLabel);
            DrawToolbar();
            GUILayout.EndArea();
            GUI.EndScrollView();
            GUI.EndGroup();

            DrawParameterStack(stackRect, timelineLength);

            GUILayout.BeginArea(footerRect);
            DrawAudioDropArea(timelineLength);
            EditorGUILayout.Space(4);
            DrawExportUI();
            GUILayout.EndArea();
        }

        private void DrawViewerPanelContainer(Rect rect, float timelineLength)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            Rect innerRect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);
            float footerHeight = Mathf.Min(FooterHeight, Mathf.Max(90f, innerRect.height * 0.34f));
            float headerHeight = Mathf.Min(HeaderHeight, Mathf.Max(118f, innerRect.height * 0.34f));
            Rect headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, headerHeight);
            Rect stackRect = new Rect(innerRect.x, headerRect.yMax + PanelGap, innerRect.width, Mathf.Max(60f, innerRect.yMax - footerHeight - headerRect.yMax - (PanelGap * 2f)));

            GUILayout.BeginArea(headerRect);
            EditorGUILayout.LabelField("Timeline Viewer", EditorStyles.boldLabel);
            DrawPlaybackUI(timelineLength);
            EditorGUILayout.Space(4);
            DrawTimelineRuler(timelineLength);
            DrawTimelineOverview(timelineLength);
            GUILayout.EndArea();

            DrawViewerStack(stackRect, timelineLength);
        }

        private void DrawParameterStack(Rect stackRect, float timelineLength)
        {
            EditorGUI.DrawRect(stackRect, new Color(0.095f, 0.095f, 0.095f));
            float rowHeight = ViewerTrackHeight;
            float viewHeight = Mathf.Max(stackRect.height, entries.Count * (rowHeight + StackRowGap) + StackRowGap);
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(10f, stackRect.width - 16f), viewHeight);

            parameterScroll = GUI.BeginScrollView(stackRect, parameterScroll, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));

            if (entries.Count == 0)
            {
                Rect emptyRect = GUILayoutUtility.GetRect(viewRect.width, 54f);
                GUI.Label(emptyRect, "Add AudioClips to build a stack.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Rect rowRect = GUILayoutUtility.GetRect(viewRect.width, rowHeight);
                    DrawClipParameterRow(rowRect, entries[i], i, timelineLength);
                    GUILayout.Space(StackRowGap);
                }
            }

            GUILayout.EndArea();
            GUI.EndScrollView();
        }

        private void DrawViewerStack(Rect stackRect, float timelineLength)
        {
            EditorGUI.DrawRect(stackRect, new Color(0.075f, 0.075f, 0.075f));
            float rowHeight = ViewerTrackHeight;
            float viewHeight = Mathf.Max(stackRect.height, entries.Count * (rowHeight + StackRowGap) + StackRowGap);
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(10f, stackRect.width - 16f), viewHeight);

            viewerScroll = parameterScroll;
            viewerScroll = GUI.BeginScrollView(stackRect, viewerScroll, viewRect);
            GUILayout.BeginArea(new Rect(0f, 0f, viewRect.width, viewRect.height));

            if (entries.Count == 0)
            {
                Rect emptyRect = GUILayoutUtility.GetRect(viewRect.width, 54f);
                GUI.Label(emptyRect, "Drop AudioClips in the parameter panel to preview the combined timeline.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    DrawTrack(entries[i], i, timelineLength, rowHeight);
                    GUILayout.Space(StackRowGap);
                }
            }

            GUILayout.EndArea();
            GUI.EndScrollView();
            parameterScroll.y = viewerScroll.y;
        }

        private void DrawClipParameterRow(Rect rowRect, CombineEntry entry, int index, float timelineLength)
        {
            bool selected = selectedEntryIndex == index;
            Color displayColor = entry.muted ? Color.Lerp(entry.trackColor, Color.gray, 0.75f) : entry.trackColor;
            EditorGUI.DrawRect(rowRect, selected ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.13f, 0.13f, 0.13f));
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 5f, rowRect.height), displayColor);

            Rect handleRect = new Rect(rowRect.x + 8f, rowRect.y + 8f, 16f, rowRect.height - 16f);
            GUI.Label(handleRect, "=", EditorStyles.centeredGreyMiniLabel);
            HandleClipReorder(rowRect, handleRect, index);

            Rect contentRect = new Rect(rowRect.x + 28f, rowRect.y + 5f, rowRect.width - 34f, rowRect.height - 10f);

            float y = contentRect.y;
            const float lineHeight = 18f;
            const float lineGap = 4f;
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 44f;

            Rect headerRect = new Rect(contentRect.x, y, contentRect.width, lineHeight);
            Rect removeRect = new Rect(headerRect.xMax - 24f, y, 24f, lineHeight);
            Rect downRect = new Rect(removeRect.x - 36f, y, 34f, lineHeight);
            Rect upRect = new Rect(downRect.x - 36f, y, 34f, lineHeight);
            Rect muteRect = new Rect(upRect.x - 54f, y, 52f, lineHeight);
            Rect lockRect = new Rect(muteRect.x - 54f, y, 52f, lineHeight);
            Rect clipButtonRect = new Rect(contentRect.x, y, Mathf.Max(54f, lockRect.x - contentRect.x - 4f), lineHeight);

            if (GUI.Button(clipButtonRect, $"Clip {index + 1}", EditorStyles.miniButtonLeft))
            {
                selectedEntryIndex = index;
            }

            entry.locked = GUI.Toggle(lockRect, entry.locked, "Lock", EditorStyles.radioButton);
            entry.muted = GUI.Toggle(muteRect, entry.muted, "Mute", EditorStyles.radioButton);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && !entry.locked && index > 0 && !entries[index - 1].locked;
            if (GUI.Button(upRect, "Up", EditorStyles.miniButtonLeft))
            {
                SwapEntries(index, index - 1);
                selectedEntryIndex = index - 1;
                EditorGUIUtility.labelWidth = originalLabelWidth;
                GUI.enabled = wasEnabled;
                return;
            }

            GUI.enabled = wasEnabled && !entry.locked && index < entries.Count - 1 && !entries[index + 1].locked;
            if (GUI.Button(downRect, "Dn", EditorStyles.miniButtonMid))
            {
                SwapEntries(index, index + 1);
                selectedEntryIndex = index + 1;
                EditorGUIUtility.labelWidth = originalLabelWidth;
                GUI.enabled = wasEnabled;
                return;
            }

            GUI.enabled = wasEnabled && !entry.locked;
            if (GUI.Button(removeRect, "X", EditorStyles.miniButtonRight))
            {
                entries.RemoveAt(index);
                selectedEntryIndex = Mathf.Clamp(index, 0, entries.Count - 1);
                EditorGUIUtility.labelWidth = originalLabelWidth;
                GUI.enabled = wasEnabled;
                return;
            }

            GUI.enabled = wasEnabled;
            y += lineHeight + lineGap;

            EditorGUI.BeginDisabledGroup(entry.locked);
            Rect clipRect = new Rect(contentRect.x, y, contentRect.width, lineHeight);
            AudioClip newClip = (AudioClip)EditorGUI.ObjectField(clipRect, "Clip", entry.clip, typeof(AudioClip), false);
            if (newClip != entry.clip)
            {
                AssignClip(entry, newClip);
            }

            if (entry.clip == null)
            {
                EditorGUI.EndDisabledGroup();
                EditorGUIUtility.labelWidth = originalLabelWidth;
                GUI.Label(new Rect(contentRect.x, y + lineHeight + lineGap, contentRect.width, lineHeight), "Drop an AudioClip into this row.", EditorStyles.miniLabel);
                return;
            }

            ClampEntry(entry);
            y += lineHeight + lineGap;

            Rect colorRect = new Rect(contentRect.x, y, 42f, lineHeight);
            Rect gainRect = new Rect(colorRect.xMax + 4f, y, contentRect.width - colorRect.width - 52f, lineHeight);
            Rect zeroDbRect = new Rect(contentRect.xMax - 42f, y, 42f, lineHeight);
            entry.trackColor = EditorGUI.ColorField(colorRect, GUIContent.none, entry.trackColor);
            entry.gain = EditorGUI.Slider(gainRect, "Gain", entry.gain, 0f, 1.25f);
            if (GUI.Button(zeroDbRect, "0 dB", EditorStyles.miniButton))
            {
                entry.gain = 1f;
            }
            y += lineHeight + lineGap;

            Rect segmentRect = new Rect(contentRect.x, y, contentRect.width, lineHeight);
            EditorGUI.MinMaxSlider(segmentRect, "Segment", ref entry.inTime, ref entry.outTime, 0f, entry.clip.length);
            y += lineHeight + lineGap;

            float thirdWidth = (contentRect.width - 8f) / 3f;
            Rect inRect = new Rect(contentRect.x, y, thirdWidth, lineHeight);
            Rect outRect = new Rect(inRect.xMax + 4f, y, thirdWidth, lineHeight);
            Rect startRect = new Rect(outRect.xMax + 4f, y, thirdWidth, lineHeight);
            EditorGUIUtility.labelWidth = 28f;
            entry.inTime = EditorGUI.FloatField(inRect, "In", entry.inTime);
            entry.outTime = EditorGUI.FloatField(outRect, "Out", entry.outTime);
            EditorGUIUtility.labelWidth = 36f;
            entry.timelineStart = Mathf.Max(0f, EditorGUI.FloatField(startRect, "Start", entry.timelineStart));
            y += lineHeight + lineGap;

            Rect moveRect = new Rect(contentRect.x, y, contentRect.width, lineHeight);
            EditorGUIUtility.labelWidth = 44f;
            float segmentStart = entry.SegmentTimelineStart;
            float moveMax = Mathf.Max(segmentStart, timelineLength - entry.SegmentLength, timelineLength + defaultGapSeconds);
            segmentStart = EditorGUI.Slider(moveRect, "Move", segmentStart, 0f, moveMax);
            entry.timelineStart = Mathf.Max(0f, segmentStart - entry.inTime);
            y += lineHeight + lineGap;

            Rect fadeToggleRect = new Rect(contentRect.x, y, contentRect.width, lineHeight);
            float toggleWidth = (fadeToggleRect.width - 4f) * 0.5f;
            entry.fadeInEnabled = GUI.Toggle(new Rect(fadeToggleRect.x, y, toggleWidth, lineHeight), entry.fadeInEnabled, "+ Fade In");
            entry.fadeOutEnabled = GUI.Toggle(new Rect(fadeToggleRect.x + toggleWidth + 4f, y, toggleWidth, lineHeight), entry.fadeOutEnabled, "+ Fade Out");
            y += lineHeight + lineGap;

            Rect fadeFieldRect = new Rect(contentRect.x, y, contentRect.width, lineHeight);
            float fadeFieldWidth = (fadeFieldRect.width - 4f) * 0.5f;
            EditorGUIUtility.labelWidth = 38f;
            entry.fadeInDuration = Mathf.Clamp(EditorGUI.FloatField(new Rect(fadeFieldRect.x, y, fadeFieldWidth, lineHeight), "In", entry.fadeInDuration), 0f, entry.SegmentLength);
            entry.fadeOutDuration = Mathf.Clamp(EditorGUI.FloatField(new Rect(fadeFieldRect.x + fadeFieldWidth + 4f, y, fadeFieldWidth, lineHeight), "Out", entry.fadeOutDuration), 0f, entry.SegmentLength);
            y += lineHeight + lineGap;

            Rect fadePowerRect = new Rect(contentRect.x, y, contentRect.width, lineHeight);
            EditorGUIUtility.labelWidth = 44f;
            entry.fadeInPower = ClampFadePower(EditorGUI.FloatField(new Rect(fadePowerRect.x, y, fadeFieldWidth, lineHeight), "In C", entry.fadeInPower));
            entry.fadeOutPower = ClampFadePower(EditorGUI.FloatField(new Rect(fadePowerRect.x + fadeFieldWidth + 4f, y, fadeFieldWidth, lineHeight), "Out C", entry.fadeOutPower));
            y += lineHeight + lineGap;

            Rect startButtonRect = new Rect(contentRect.x, y, (contentRect.width - 4f) * 0.5f, lineHeight);
            Rect afterButtonRect = new Rect(startButtonRect.xMax + 4f, y, startButtonRect.width, lineHeight);
            if (GUI.Button(startButtonRect, "Start = Playhead", EditorStyles.miniButtonLeft))
            {
                entry.timelineStart = Mathf.Max(0f, playhead - entry.inTime);
            }

            if (GUI.Button(afterButtonRect, "After Prev", EditorStyles.miniButtonRight))
            {
                if (index > 0)
                {
                    entry.timelineStart = Mathf.Max(0f, entries[index - 1].SegmentTimelineEnd + defaultGapSeconds - entry.inTime);
                }
            }
            y += lineHeight + lineGap;

            EditorGUI.EndDisabledGroup();
            EditorGUIUtility.labelWidth = originalLabelWidth;

            string detail = $"{entry.SegmentLength:F3}s | Start {entry.SegmentTimelineStart:F3}s | End {entry.SegmentTimelineEnd:F3}s";
            GUI.Label(new Rect(contentRect.x, y, contentRect.width, lineHeight), detail, EditorStyles.miniLabel);
        }

        private void HandleClipReorder(Rect rowRect, Rect handleRect, int index)
        {
            Event evt = Event.current;
            if (evt == null)
            {
                return;
            }

            if (evt.type == EventType.MouseDown && rowRect.Contains(evt.mousePosition))
            {
                selectedEntryIndex = index;
                if (handleRect.Contains(evt.mousePosition) && !entries[index].locked)
                {
                    listDragIndex = index;
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseDrag && listDragIndex >= 0 && rowRect.Contains(evt.mousePosition) && listDragIndex != index)
            {
                if (!entries[listDragIndex].locked && !entries[index].locked)
                {
                    SwapEntries(listDragIndex, index);
                    selectedEntryIndex = index;
                    listDragIndex = index;
                    Repaint();
                    evt.Use();
                }
            }
        }

        private void DrawTimelineRuler(float timelineLength)
        {
            Rect fullRect = GUILayoutUtility.GetRect(10f, 10000f, 22f, 22f);
            EditorGUI.DrawRect(fullRect, new Color(0.10f, 0.10f, 0.10f));
            Rect rulerRect = ReserveViewerScrollbarGutter(fullRect);

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.18f);
            int majorTicks = Mathf.Clamp(Mathf.CeilToInt(timelineLength), 1, 16);
            for (int i = 0; i <= majorTicks; i++)
            {
                float time = timelineLength * (i / (float)majorTicks);
                float x = Mathf.Lerp(rulerRect.x, rulerRect.xMax, i / (float)majorTicks);
                Handles.DrawLine(new Vector3(x, rulerRect.yMax - 10f), new Vector3(x, rulerRect.yMax));
                GUI.Label(new Rect(x + 3f, rulerRect.y + 1f, 54f, 16f), $"{time:F1}", EditorStyles.miniLabel);
            }

            Handles.EndGUI();
        }

        private void DrawToolbar()
        {
            DrawMixDataUI();

            using (new EditorGUILayout.HorizontalScope())
            {
                defaultGapSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Gap (sec)", defaultGapSeconds));
                nudgeSeconds = Mathf.Max(0.001f, EditorGUILayout.FloatField("Nudge (sec)", nudgeSeconds));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                outputFrequency = Mathf.Max(8000, EditorGUILayout.IntField("Output Hz", outputFrequency));
                outputChannels = EditorGUILayout.IntPopup("Output Channels", outputChannels, new[] { "Mono", "Stereo" }, new[] { 1, 2 });
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                normalizeIfClipping = EditorGUILayout.ToggleLeft("Normalize if mix clips", normalizeIfClipping);
            }

            DrawStackActionsPanel();
        }

        private void DrawMixDataUI()
        {
            EditorGUI.BeginChangeCheck();
            activeMixData = (FPAudioCombineData)EditorGUILayout.ObjectField("Mix Data", activeMixData, typeof(FPAudioCombineData), false);
            if (EditorGUI.EndChangeCheck() && activeMixData != null)
            {
                CacheMixAssetPathFields(activeMixData);
                LoadMixData(activeMixData);
            }

            GUIStyle wrappedTextArea = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            EditorGUILayout.LabelField("Name", EditorStyles.miniLabel);
            mixDataFileName = EditorGUILayout.TextArea(mixDataFileName, wrappedTextArea, GUILayout.MinHeight(38f), GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Folder", EditorStyles.miniLabel);
            mixDataFolder = EditorGUILayout.TextArea(mixDataFolder, wrappedTextArea, GUILayout.MinHeight(38f), GUILayout.ExpandWidth(true));

            using (new EditorGUILayout.HorizontalScope())
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && activeMixData != null;
                if (GUILayout.Button("Load", GUILayout.Height(22f)))
                {
                    LoadMixData(activeMixData);
                }

                GUI.enabled = wasEnabled;
                if (GUILayout.Button("Save", GUILayout.Height(22f)))
                {
                    SaveMixData();
                }
            }
        }

        private void DrawStackActionsPanel()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUIStyle warningLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = FP_Utility_Editor.WarningColor }
                };
                EditorGUILayout.LabelField("Stack", warningLabel);

                DrawStackActionButtons();
            }
        }

        private void DrawStackActionButtons()
        {
            const float buttonHeight = 24f;
            const float gap = 4f;
            Rect rowRect = GUILayoutUtility.GetRect(1f, 10000f, buttonHeight, buttonHeight);
            float buttonWidth = Mathf.Floor((rowRect.width - (gap * 2f)) / 3f);
            Rect addRect = new Rect(rowRect.x, rowRect.y, buttonWidth, buttonHeight);
            Rect autoRect = new Rect(addRect.xMax + gap, rowRect.y, buttonWidth, buttonHeight);
            Rect clearRect = new Rect(autoRect.xMax + gap, rowRect.y, rowRect.xMax - autoRect.xMax - gap, buttonHeight);

            Color oldBackground = GUI.backgroundColor;

            GUI.backgroundColor = FP_Utility_Editor.OkayColor;
            if (GUI.Button(addRect, "+ Add"))
            {
                entries.Add(new CombineEntry { trackColor = GenerateTrackColor(entries.Count) });
            }

            GUI.backgroundColor = FP_Utility_Editor.TextActiveColor;
            if (GUI.Button(autoRect, "Auto"))
            {
                AutoLayoutEntries();
            }

            GUI.backgroundColor = FP_Utility_Editor.WarningColor;
            if (GUI.Button(clearRect, "x Clear"))
            {
                ClearStackWithConfirmation();
            }

            GUI.backgroundColor = oldBackground;
        }

        private void ClearStackWithConfirmation()
        {
            ClearStackConfirmWindow.Show(this);
        }

        private void ClearStack()
        {
            EditorStopAll();
            isPlayingCombined = false;
            activeCombinedPreviewClip = null;
            entries.Clear();
            selectedEntryIndex = -1;
            playhead = 0f;
            hasExportStartBookend = false;
            exportStartBookend = 0f;
            hasExportEndBookend = false;
            exportEndBookend = 0f;
            Repaint();
        }

        private sealed class ClearStackConfirmWindow : EditorWindow
        {
            private FPAudioCombineTool owner;

            public static void Show(FPAudioCombineTool owner)
            {
                if (owner == null)
                {
                    return;
                }

                var window = CreateInstance<ClearStackConfirmWindow>();
                window.owner = owner;
                window.titleContent = new GUIContent("Clear Stack");

                Vector2 size = new Vector2(420f, 148f);
                Rect parent = owner.position;
                window.position = new Rect(
                    parent.x + ((parent.width - size.x) * 0.5f),
                    parent.y + ((parent.height - size.y) * 0.5f),
                    size.x,
                    size.y);
                window.minSize = size;
                window.maxSize = size;
                window.ShowUtility();
                window.Focus();
            }

            private void OnGUI()
            {
                if (owner == null)
                {
                    Close();
                    return;
                }

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    Close();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("Clear Audio Combine Stack", EditorStyles.boldLabel);
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("Clear all clips and export bookends from the current audio combine stack? This does not delete saved mix data assets, but unsaved window changes will be lost.", MessageType.Warning);

                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Cancel", GUILayout.Height(26f)))
                    {
                        Close();
                    }

                    Color oldBackground = GUI.backgroundColor;
                    GUI.backgroundColor = FP_Utility_Editor.WarningColor;
                    if (GUILayout.Button("Clear Stack", GUILayout.Height(26f)))
                    {
                        owner.ClearStack();
                        Close();
                    }
                    GUI.backgroundColor = oldBackground;
                }
            }
        }

        private void DrawPlaybackUI(float timelineLength)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play From Playhead", GUILayout.Height(26)))
                {
                    PlayCombinedFrom(playhead);
                }

                if (GUILayout.Button("Play From Beginning", GUILayout.Height(26)))
                {
                    playhead = 0f;
                    PlayCombinedFrom(0f);
                }

                if (GUILayout.Button("Stop", GUILayout.Height(26)))
                {
                    isPlayingCombined = false;
                    activeCombinedPreviewClip = null;
                    EditorStopAll();
                    EditorApplication.update -= OnEditorUpdate;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Export Start {", GUILayout.Height(24)))
                {
                    SetExportStartBookend(playhead, timelineLength);
                }

                if (GUILayout.Button("Set Export End }", GUILayout.Height(24)))
                {
                    SetExportEndBookend(playhead, timelineLength);
                }
            }

            playhead = EditorGUILayout.Slider("Playhead", playhead, 0f, timelineLength);
            autoAdvancePlayhead = EditorGUILayout.ToggleLeft("Auto-advance playhead during preview", autoAdvancePlayhead);
        }

        private void DrawTimelineOverview(float timelineLength)
        {
            Rect fullRect = GUILayoutUtility.GetRect(10, 10000, 58, 58);
            EditorGUI.DrawRect(fullRect, new Color(0.08f, 0.08f, 0.08f));
            Rect r = ReserveViewerScrollbarGutter(fullRect);

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

            DrawPlayheadLine(r, timelineLength, true);
            DrawExportStartLine(r, timelineLength);
            DrawExportEndLine(r, timelineLength);
            Handles.EndGUI();

            HandleOverviewInput(r, timelineLength);
        }

        private static Rect ReserveViewerScrollbarGutter(Rect rect)
        {
            rect.width = Mathf.Max(1f, rect.width - ViewerScrollbarGutter);
            return rect;
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

            Rect playheadGrabRect = BuildPlayheadGrabRect(r, timelineLength);
            EditorGUIUtility.AddCursorRect(playheadGrabRect, MouseCursor.SlideArrow);

            if (evt.type == EventType.MouseDown && r.Contains(evt.mousePosition))
            {
                float mouseTime = RectToTime(r, timelineLength, evt.mousePosition.x);
                if (playheadGrabRect.Contains(evt.mousePosition))
                {
                    overviewPlayheadDragging = true;
                    playhead = Mathf.Clamp(mouseTime, 0f, timelineLength);
                    Repaint();
                    evt.Use();
                    return;
                }

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
            else if (evt.type == EventType.MouseDrag && overviewPlayheadDragging)
            {
                SetPlayheadFromRect(r, timelineLength);
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && overviewPlayheadDragging)
            {
                overviewPlayheadDragging = false;
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

        private Rect BuildPlayheadGrabRect(Rect r, float timelineLength)
        {
            float xHead = Mathf.Lerp(r.x, r.xMax, timelineLength > 0f ? Mathf.Clamp01(playhead / timelineLength) : 0f);
            return new Rect(xHead - 7f, r.y, 14f, r.height);
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
                    if (entry.waveform.unavailable)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("Segment Peak: unavailable", EditorStyles.miniLabel);
                            if (GUILayout.Button("Repair Import Settings", GUILayout.Width(170)))
                            {
                                RepairClipImportForSampleData(entry);
                            }
                        }
                    }
                    else
                    {
                        float effectivePeak = entry.waveform.peak * entry.gain;
                        EditorGUILayout.LabelField($"Segment Peak: {entry.waveform.peak:F3} | After Gain: {effectivePeak:F3}", EditorStyles.miniLabel);
                    }
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
            DrawFadeOverlays(segRect, entry, displayColor);

            int width = Mathf.Max(1, Mathf.RoundToInt(segRect.width));
            bool unavailableWaveform = entry.waveform != null && entry.waveform.unavailable;
            if (entry.waveform == null ||
                entry.lastClip != entry.clip ||
                (!unavailableWaveform && entry.lastWaveWidth != width) ||
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

            if (entry.waveform != null && entry.waveform.unavailable)
            {
                GUIStyle unavailableStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                GUI.Label(segRect, entry.waveform.unavailableReason, unavailableStyle);
            }
            else if (entry.waveform != null && entry.waveform.min != null && entry.waveform.max != null)
            {
                const float waveformScale = 1f;
                float pad = 2f;
                float half = (segRect.height * 0.5f) - pad;

                for (int x = 0; x < entry.waveform.width && segRect.x + x < segRect.xMax; x++)
                {
                    float segmentTime = entry.waveform.width <= 1 ? 0f : entry.SegmentLength * (x / (float)(entry.waveform.width - 1));
                    float fadeGain = CalculateFadeGain(entry, segmentTime);
                    float min = Mathf.Clamp(entry.waveform.min[x] * entry.gain * fadeGain, -1f, 1f);
                    float max = Mathf.Clamp(entry.waveform.max[x] * entry.gain * fadeGain, -1f, 1f);
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
            DrawFadeHandles(segRect, entry, displayColor);
            DrawPlayheadLine(r, timelineLength);
            DrawExportStartLine(r, timelineLength);
            DrawExportEndLine(r, timelineLength);
            Handles.EndGUI();

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = new Color(1f, 1f, 1f, 0.9f) }
            };
            string state = entry.locked && entry.muted ? " [LOCKED, MUTED]" : entry.locked ? " [LOCKED]" : entry.muted ? " [MUTED]" : string.Empty;
            GUI.Label(new Rect(r.x + 6f, r.y + 3f, r.width - 12f, 18f), $"{index + 1}: {entry.clip.name}{state}", labelStyle);

            HandleTrackInput(r, segRect, entry, index, timelineLength);
        }

        private static void DrawFadeOverlays(Rect segmentRect, CombineEntry entry, Color trackColor)
        {
            if (entry.SegmentLength <= 0f)
            {
                return;
            }

            if (entry.fadeInEnabled && entry.fadeInDuration > 0f)
            {
                float width = Mathf.Clamp(segmentRect.width * (entry.fadeInDuration / entry.SegmentLength), 1f, segmentRect.width);
                Rect fadeRect = new Rect(segmentRect.x, segmentRect.y, width, segmentRect.height);
                EditorGUI.DrawRect(fadeRect, new Color(0f, 0f, 0f, 0.24f));
                EditorGUI.DrawRect(new Rect(fadeRect.xMax - 3f, fadeRect.y, 3f, fadeRect.height), Color.Lerp(trackColor, Color.white, 0.45f));
            }

            if (entry.fadeOutEnabled && entry.fadeOutDuration > 0f)
            {
                float width = Mathf.Clamp(segmentRect.width * (entry.fadeOutDuration / entry.SegmentLength), 1f, segmentRect.width);
                Rect fadeRect = new Rect(segmentRect.xMax - width, segmentRect.y, width, segmentRect.height);
                EditorGUI.DrawRect(fadeRect, new Color(0f, 0f, 0f, 0.24f));
                EditorGUI.DrawRect(new Rect(fadeRect.x, fadeRect.y, 3f, fadeRect.height), Color.Lerp(trackColor, Color.white, 0.45f));
            }
        }

        private static void DrawFadeHandles(Rect segmentRect, CombineEntry entry, Color trackColor)
        {
            if (entry.SegmentLength <= 0f)
            {
                return;
            }

            Color handleColor = Color.Lerp(trackColor, Color.white, 0.55f);
            if (entry.fadeInEnabled)
            {
                float width = Mathf.Clamp(segmentRect.width * (entry.fadeInDuration / entry.SegmentLength), 0f, segmentRect.width);
                float x = segmentRect.x + width;
                Handles.DrawSolidRectangleWithOutline(new Rect(x - 2f, segmentRect.y, 4f, segmentRect.height), handleColor, new Color(0f, 0f, 0f, 0.35f));
                Rect fadeRect = new Rect(segmentRect.x, segmentRect.y, width, segmentRect.height);
                DrawFadeCurve(fadeRect, entry.fadeInPower, false, handleColor);
                DrawFadeCurveHandle(fadeRect, entry.fadeInPower, false, handleColor);
            }

            if (entry.fadeOutEnabled)
            {
                float width = Mathf.Clamp(segmentRect.width * (entry.fadeOutDuration / entry.SegmentLength), 0f, segmentRect.width);
                float x = segmentRect.xMax - width;
                Handles.DrawSolidRectangleWithOutline(new Rect(x - 2f, segmentRect.y, 4f, segmentRect.height), handleColor, new Color(0f, 0f, 0f, 0.35f));
                Rect fadeRect = new Rect(segmentRect.xMax - width, segmentRect.y, width, segmentRect.height);
                DrawFadeCurve(fadeRect, entry.fadeOutPower, true, handleColor);
                DrawFadeCurveHandle(fadeRect, entry.fadeOutPower, true, handleColor);
            }
        }

        private static void DrawFadeCurve(Rect fadeRect, float power, bool fadeOut, Color curveColor)
        {
            if (fadeRect.width < 4f)
            {
                return;
            }

            power = ClampFadePower(power);
            Handles.color = curveColor;
            Vector3 previous = Vector3.zero;
            const int steps = 18;
            float pad = 6f;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float gain = fadeOut ? Mathf.Pow(1f - t, power) : Mathf.Pow(t, power);
                float x = Mathf.Lerp(fadeRect.x, fadeRect.xMax, t);
                float y = Mathf.Lerp(fadeRect.yMax - pad, fadeRect.y + pad, gain);
                Vector3 point = new Vector3(x, y);
                if (i > 0)
                {
                    Handles.DrawLine(previous, point);
                }

                previous = point;
            }
        }

        private static void DrawFadeCurveHandle(Rect fadeRect, float power, bool fadeOut, Color curveColor)
        {
            Rect handleRect = BuildFadeCurveHandleRect(fadeRect, power, fadeOut);
            if (handleRect.width <= 0f)
            {
                return;
            }

            Handles.DrawSolidRectangleWithOutline(handleRect, Color.Lerp(curveColor, Color.white, 0.2f), new Color(0f, 0f, 0f, 0.65f));
        }

        private static Rect BuildFadeCurveHandleRect(Rect fadeRect, float power, bool fadeOut)
        {
            if (fadeRect.width < 12f || fadeRect.height < 12f)
            {
                return Rect.zero;
            }

            power = ClampFadePower(power);
            const float t = 0.5f;
            const float size = 10f;
            const float pad = 6f;
            float gain = fadeOut ? Mathf.Pow(1f - t, power) : Mathf.Pow(t, power);
            float x = Mathf.Lerp(fadeRect.x, fadeRect.xMax, t);
            float y = Mathf.Lerp(fadeRect.yMax - pad, fadeRect.y + pad, gain);
            return new Rect(x - (size * 0.5f), y - (size * 0.5f), size, size);
        }

        private void HandleTrackInput(Rect trackRect, Rect segmentRect, CombineEntry entry, int index, float timelineLength)
        {
            Event evt = Event.current;
            if (evt == null)
            {
                return;
            }

            if (!entry.locked)
            {
                EditorGUIUtility.AddCursorRect(segmentRect, MouseCursor.MoveArrow);
                Rect fadeInHandle = BuildFadeInHandleRect(segmentRect, entry);
                Rect fadeOutHandle = BuildFadeOutHandleRect(segmentRect, entry);
                Rect fadeInCurveHandle = BuildFadeCurveHandleRect(BuildFadeInRect(segmentRect, entry), entry.fadeInPower, false);
                Rect fadeOutCurveHandle = BuildFadeCurveHandleRect(BuildFadeOutRect(segmentRect, entry), entry.fadeOutPower, true);
                if (entry.fadeInEnabled)
                {
                    EditorGUIUtility.AddCursorRect(fadeInHandle, MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(fadeInCurveHandle, MouseCursor.ResizeVertical);
                }

                if (entry.fadeOutEnabled)
                {
                    EditorGUIUtility.AddCursorRect(fadeOutHandle, MouseCursor.ResizeHorizontal);
                    EditorGUIUtility.AddCursorRect(fadeOutCurveHandle, MouseCursor.ResizeVertical);
                }
            }

            if (evt.type == EventType.MouseDown && trackRect.Contains(evt.mousePosition))
            {
                selectedEntryIndex = index;
                float mouseTime = RectToTime(trackRect, timelineLength, evt.mousePosition.x);

                if (!entry.locked && entry.fadeInEnabled && BuildFadeCurveHandleRect(BuildFadeInRect(segmentRect, entry), entry.fadeInPower, false).Contains(evt.mousePosition))
                {
                    fadeCurveDragIndex = index;
                    fadeCurveDragIsOut = false;
                }
                else if (!entry.locked && entry.fadeOutEnabled && BuildFadeCurveHandleRect(BuildFadeOutRect(segmentRect, entry), entry.fadeOutPower, true).Contains(evt.mousePosition))
                {
                    fadeCurveDragIndex = index;
                    fadeCurveDragIsOut = true;
                }
                else if (!entry.locked && entry.fadeInEnabled && BuildFadeInHandleRect(segmentRect, entry).Contains(evt.mousePosition))
                {
                    fadeDragIndex = index;
                    fadeDragIsOut = false;
                }
                else if (!entry.locked && entry.fadeOutEnabled && BuildFadeOutHandleRect(segmentRect, entry).Contains(evt.mousePosition))
                {
                    fadeDragIndex = index;
                    fadeDragIsOut = true;
                }
                else if (!entry.locked && segmentRect.Contains(evt.mousePosition))
                {
                    trackDragIndex = index;
                    trackDragOffset = mouseTime - entry.timelineStart;
                    trackDragTimelineLength = timelineLength;
                }
                else
                {
                    playhead = Mathf.Clamp(mouseTime, 0f, timelineLength);
                }

                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && fadeDragIndex == index)
            {
                float mouseTime = RectToTime(trackRect, timelineLength, evt.mousePosition.x);
                if (fadeDragIsOut)
                {
                    entry.fadeOutDuration = Mathf.Clamp(entry.SegmentTimelineEnd - mouseTime, 0f, entry.SegmentLength);
                }
                else
                {
                    entry.fadeInDuration = Mathf.Clamp(mouseTime - entry.SegmentTimelineStart, 0f, entry.SegmentLength);
                }

                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && fadeDragIndex == index)
            {
                fadeDragIndex = -1;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && fadeCurveDragIndex == index)
            {
                if (fadeCurveDragIsOut)
                {
                    entry.fadeOutPower = PointerYToFadePower(trackRect, evt.mousePosition.y);
                }
                else
                {
                    entry.fadeInPower = PointerYToFadePower(trackRect, evt.mousePosition.y);
                }

                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && fadeCurveDragIndex == index)
            {
                fadeCurveDragIndex = -1;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && trackDragIndex == index)
            {
                float dragLength = Mathf.Max(timelineLength, trackDragTimelineLength);
                float mouseTime = RectToTime(trackRect, dragLength, evt.mousePosition.x);
                entry.timelineStart = Mathf.Max(0f, mouseTime - trackDragOffset);
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && trackDragIndex == index)
            {
                trackDragIndex = -1;
                evt.Use();
            }
        }

        private static Rect BuildFadeInHandleRect(Rect segmentRect, CombineEntry entry)
        {
            float x = segmentRect.x;
            if (entry.SegmentLength > 0f)
            {
                x += segmentRect.width * Mathf.Clamp01(entry.fadeInDuration / entry.SegmentLength);
            }

            return new Rect(x - 6f, segmentRect.y, 12f, segmentRect.height);
        }

        private static Rect BuildFadeInRect(Rect segmentRect, CombineEntry entry)
        {
            if (entry.SegmentLength <= 0f)
            {
                return Rect.zero;
            }

            float width = Mathf.Clamp(segmentRect.width * (entry.fadeInDuration / entry.SegmentLength), 0f, segmentRect.width);
            return new Rect(segmentRect.x, segmentRect.y, width, segmentRect.height);
        }

        private static Rect BuildFadeOutHandleRect(Rect segmentRect, CombineEntry entry)
        {
            float x = segmentRect.xMax;
            if (entry.SegmentLength > 0f)
            {
                x -= segmentRect.width * Mathf.Clamp01(entry.fadeOutDuration / entry.SegmentLength);
            }

            return new Rect(x - 6f, segmentRect.y, 12f, segmentRect.height);
        }

        private static Rect BuildFadeOutRect(Rect segmentRect, CombineEntry entry)
        {
            if (entry.SegmentLength <= 0f)
            {
                return Rect.zero;
            }

            float width = Mathf.Clamp(segmentRect.width * (entry.fadeOutDuration / entry.SegmentLength), 0f, segmentRect.width);
            return new Rect(segmentRect.xMax - width, segmentRect.y, width, segmentRect.height);
        }

        private static float PointerYToFadePower(Rect trackRect, float mouseY)
        {
            const float pad = 6f;
            float usableHeight = Mathf.Max(1f, trackRect.height - (pad * 2f));
            float gainAtMidpoint = Mathf.Clamp01((trackRect.yMax - pad - mouseY) / usableHeight);
            gainAtMidpoint = Mathf.Clamp(gainAtMidpoint, Mathf.Pow(0.5f, 4f), Mathf.Pow(0.5f, 0.25f));
            return ClampFadePower(Mathf.Log(gainAtMidpoint) / Mathf.Log(0.5f));
        }

        private void DrawPlayheadLine(Rect r, float timelineLength, bool drawHandle = false)
        {
            float xHead = Mathf.Lerp(r.x, r.xMax, timelineLength > 0f ? Mathf.Clamp01(playhead / timelineLength) : 0f);
            if (drawHandle)
            {
                Rect lineRect = new Rect(xHead - 1.5f, r.y, 3f, r.height);
                Rect capRect = new Rect(xHead - 6f, r.y, 12f, 7f);
                Handles.DrawSolidRectangleWithOutline(lineRect, kPlayheadColor, new Color(0f, 0f, 0f, 0f));
                Handles.DrawSolidRectangleWithOutline(capRect, Color.Lerp(kPlayheadColor, Color.white, 0.18f), new Color(0f, 0f, 0f, 0.35f));
                return;
            }

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

        private void DrawExportEndLine(Rect r, float timelineLength)
        {
            if (!hasExportEndBookend)
            {
                return;
            }

            float xHead = Mathf.Lerp(r.x, r.xMax, timelineLength > 0f ? Mathf.Clamp01(exportEndBookend / timelineLength) : 0f);
            Handles.color = kExportStartColor;
            Handles.DrawLine(new Vector3(xHead, r.y), new Vector3(xHead, r.yMax));
            Rect cap = new Rect(xHead - 4f, r.yMax - 5f, 8f, 5f);
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

        private void RepairClipImportForSampleData(CombineEntry entry)
        {
            if (entry == null || entry.clip == null)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(entry.clip);
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("Audio Importer Not Found", $"Could not find an AudioImporter for:\n{assetPath}", "OK");
                return;
            }

            string platformName = GetActiveBuildAudioPlatformName();
            if (!EditorUtility.DisplayDialog(
                    "Repair Audio Import Settings",
                    $"Reimport '{entry.clip.name}' with readable PCM sample settings for default and {platformName}?\n\nThis sets Load Type to Decompress On Load, Compression Format to PCM, Preload Audio Data on, and Load In Background off.",
                    "Repair",
                    "Cancel"))
            {
                return;
            }

            Undo.RecordObject(importer, "Repair Audio Import Settings");

            AudioImporterSampleSettings defaultSettings = importer.defaultSampleSettings;
            ApplyReadableAudioSettings(ref defaultSettings);
            importer.defaultSampleSettings = defaultSettings;

            AudioImporterSampleSettings platformSettings = importer.ContainsSampleSettingsOverride(platformName)
                ? importer.GetOverrideSampleSettings(platformName)
                : defaultSettings;
            ApplyReadableAudioSettings(ref platformSettings);

            if (!importer.SetOverrideSampleSettings(platformName, platformSettings))
            {
                Debug.LogWarning($"Could not set audio override for {platformName} at path: {assetPath}");
            }

            importer.loadInBackground = false;
            importer.SaveAndReimport();
            AssetDatabase.Refresh();

            entry.waveform = null;
            entry.lastClip = null;
            entry.lastWaveWidth = 0;
            Repaint();
        }

        private static void ApplyReadableAudioSettings(ref AudioImporterSampleSettings settings)
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            settings.quality = 1f;
        }

        private static string GetActiveBuildAudioPlatformName()
        {
            string activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            switch (activeBuildTarget)
            {
                case "Android":
                    return "Android";
                case "iOS":
                    return "iOS";
                case "WebGL":
                    return "WebGL";
                case "StandaloneWindows":
                case "StandaloneWindows64":
                case "StandaloneOSX":
                case "StandaloneLinux64":
                    return "Standalone";
                default:
                    return activeBuildTarget;
            }
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
                entry.fadeInDuration = 0f;
                entry.fadeOutDuration = 0f;
                entry.fadeInPower = ClampFadePower(entry.fadeInPower);
                entry.fadeOutPower = ClampFadePower(entry.fadeOutPower);
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

            entry.fadeInDuration = Mathf.Clamp(entry.fadeInDuration, 0f, entry.SegmentLength);
            entry.fadeOutDuration = Mathf.Clamp(entry.fadeOutDuration, 0f, entry.SegmentLength);
            entry.fadeInPower = ClampFadePower(entry.fadeInPower);
            entry.fadeOutPower = ClampFadePower(entry.fadeOutPower);
            entry.timelineStart = Mathf.Max(0f, entry.timelineStart);
        }

        private static float ClampFadePower(float power)
        {
            return Mathf.Clamp(power <= 0f ? 1f : power, 0.25f, 4f);
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

        private void LoadMixData(FPAudioCombineData data)
        {
            if (data == null)
            {
                return;
            }

            EditorStopAll();
            isPlayingCombined = false;
            activeCombinedPreviewClip = null;
            EditorApplication.update -= OnEditorUpdate;

            activeMixData = data;
            CacheMixAssetPathFields(data);

            outputFrequency = Mathf.Max(8000, data.OutputFrequency);
            outputChannels = Mathf.Clamp(data.OutputChannels, 1, 2);
            normalizeIfClipping = data.NormalizeIfClipping;
            defaultGapSeconds = Mathf.Max(0f, data.DefaultGapSeconds);
            nudgeSeconds = Mathf.Max(0.001f, data.NudgeSeconds);
            exportFileName = string.IsNullOrWhiteSpace(data.ExportFileName) ? exportFileName : data.ExportFileName;
            exportFolder = string.IsNullOrWhiteSpace(data.ExportFolder) ? exportFolder : data.ExportFolder;
            hasExportStartBookend = data.HasExportStartBookend;
            exportStartBookend = Mathf.Max(0f, data.ExportStartBookend);
            hasExportEndBookend = data.HasExportEndBookend;
            exportEndBookend = Mathf.Max(0f, data.ExportEndBookend);

            entries.Clear();
            if (data.Clips != null)
            {
                for (int i = 0; i < data.Clips.Count; i++)
                {
                    FPAudioCombineClipData clipData = data.Clips[i];
                    var entry = new CombineEntry
                    {
                        clip = clipData.Clip,
                        inTime = Mathf.Max(0f, clipData.InTime),
                        outTime = Mathf.Max(0f, clipData.OutTime),
                        timelineStart = Mathf.Max(0f, clipData.TimelineStart),
                        trackColor = clipData.TrackColor,
                        gain = Mathf.Max(0f, clipData.Gain),
                        gainInitialized = true,
                        fadeInEnabled = clipData.FadeInEnabled,
                        fadeInDuration = Mathf.Max(0f, clipData.FadeInDuration),
                        fadeInPower = ClampFadePower(clipData.FadeInPower),
                        fadeOutEnabled = clipData.FadeOutEnabled,
                        fadeOutDuration = Mathf.Max(0f, clipData.FadeOutDuration),
                        fadeOutPower = ClampFadePower(clipData.FadeOutPower),
                        locked = clipData.Locked,
                        muted = clipData.Muted,
                        expanded = true
                    };

                    if (entry.trackColor.a <= 0f)
                    {
                        entry.trackColor = GenerateTrackColor(i);
                    }

                    ClampEntry(entry);
                    entries.Add(entry);
                }
            }

            selectedEntryIndex = entries.Count > 0 ? 0 : -1;
            playhead = Mathf.Clamp(playhead, 0f, Mathf.Max(0.01f, CalculateTimelineLength()));
            EnsureValidExportBookends();
            Repaint();
        }

        private void SaveMixData()
        {
            FPAudioCombineData data = activeMixData;
            if (data == null)
            {
                data = CreateOrLoadMixDataAsset();
                if (data == null)
                {
                    return;
                }
            }

            Undo.RecordObject(data, "Save Audio Combine Data");
            WriteWindowStateToMixData(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            activeMixData = data;
            CacheMixAssetPathFields(data);
            Selection.activeObject = data;
        }

        private FPAudioCombineData CreateOrLoadMixDataAsset()
        {
            string safeName = MakeSafeFilename(string.IsNullOrWhiteSpace(mixDataFileName) ? "AudioCombineData" : mixDataFileName);
            string folder = string.IsNullOrWhiteSpace(mixDataFolder) ? "Assets/_FPUtility/AudioCombineData" : mixDataFolder.Replace("\\", "/");
            if (!folder.StartsWith("Assets", StringComparison.Ordinal))
            {
                Debug.LogWarning($"Audio combine data folder must be inside Assets. Using default folder instead of: {folder}");
                folder = "Assets/_FPUtility/AudioCombineData";
            }

            EnsureAssetFolder(folder);
            string path = $"{folder}/{safeName}.asset";
            FPAudioCombineData existing = AssetDatabase.LoadAssetAtPath<FPAudioCombineData>(path);
            if (existing != null)
            {
                return existing;
            }

            FPAudioCombineData created = CreateInstance<FPAudioCombineData>();
            if (string.IsNullOrEmpty(created.UniqueID))
            {
                created.UniqueID = Guid.NewGuid().ToString();
            }

            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private void WriteWindowStateToMixData(FPAudioCombineData data)
        {
            if (data == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(data.UniqueID))
            {
                data.UniqueID = Guid.NewGuid().ToString();
            }

            data.OutputFrequency = Mathf.Max(8000, outputFrequency);
            data.OutputChannels = Mathf.Clamp(outputChannels, 1, 2);
            data.NormalizeIfClipping = normalizeIfClipping;
            data.DefaultGapSeconds = Mathf.Max(0f, defaultGapSeconds);
            data.NudgeSeconds = Mathf.Max(0.001f, nudgeSeconds);
            data.ExportFileName = exportFileName;
            data.ExportFolder = exportFolder;
            data.HasExportStartBookend = hasExportStartBookend;
            data.ExportStartBookend = Mathf.Max(0f, exportStartBookend);
            data.HasExportEndBookend = hasExportEndBookend;
            data.ExportEndBookend = Mathf.Max(0f, exportEndBookend);

            if (data.Clips == null)
            {
                data.Clips = new List<FPAudioCombineClipData>();
            }
            else
            {
                data.Clips.Clear();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CombineEntry entry = entries[i];
                ClampEntry(entry);
                data.Clips.Add(new FPAudioCombineClipData
                {
                    Clip = entry.clip,
                    InTime = entry.inTime,
                    OutTime = entry.outTime,
                    TimelineStart = entry.timelineStart,
                    TrackColor = entry.trackColor,
                    Gain = entry.gain,
                    FadeInEnabled = entry.fadeInEnabled,
                    FadeInDuration = entry.fadeInDuration,
                    FadeInPower = ClampFadePower(entry.fadeInPower),
                    FadeOutEnabled = entry.fadeOutEnabled,
                    FadeOutDuration = entry.fadeOutDuration,
                    FadeOutPower = ClampFadePower(entry.fadeOutPower),
                    Locked = entry.locked,
                    Muted = entry.muted
                });
            }
        }

        private void CacheMixAssetPathFields(FPAudioCombineData data)
        {
            if (data == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            mixDataFileName = Path.GetFileNameWithoutExtension(path);
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                mixDataFolder = folder.Replace("\\", "/");
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = folder.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            string leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                return;
            }

            EnsureAssetFolder(parent);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
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
            return Mathf.Max(0f, GetExportEndTime() - GetExportStartTime());
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

        private float GetExportEndTime()
        {
            return hasExportEndBookend ? Mathf.Max(GetExportStartTime(), exportEndBookend) : CalculateMixTimelineEnd();
        }

        private void SetExportStartBookend(float time, float timelineLength)
        {
            exportStartBookend = Mathf.Clamp(time, 0f, timelineLength);
            hasExportStartBookend = true;
            EnsureValidExportBookends();
        }

        private void SetExportEndBookend(float time, float timelineLength)
        {
            exportEndBookend = Mathf.Clamp(time, 0f, timelineLength);
            hasExportEndBookend = true;
            EnsureValidExportBookends();
        }

        private void EnsureValidExportBookends()
        {
            if (!hasExportStartBookend || !hasExportEndBookend)
            {
                return;
            }

            if (exportStartBookend > exportEndBookend)
            {
                if (Mathf.Abs(playhead - exportStartBookend) <= Mathf.Abs(playhead - exportEndBookend))
                {
                    exportEndBookend = exportStartBookend;
                }
                else
                {
                    exportStartBookend = exportEndBookend;
                }
            }
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
                    if (!clip.GetData(buffer, cursor))
                    {
                        return BuildUnavailableWaveformCache(width, $"Waveform unavailable: {clip.name}");
                    }

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

        private static WaveformCache BuildUnavailableWaveformCache(int width, string reason)
        {
            int safeWidth = Mathf.Max(1, width);
            return new WaveformCache
            {
                width = safeWidth,
                min = new float[safeWidth],
                max = new float[safeWidth],
                peak = 0f,
                unavailable = true,
                unavailableReason = reason
            };
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

            if (peak <= 1e-5f)
            {
                Debug.LogWarning("FP Audio Combine Tool built a silent combined preview/export. Check that clips are unmuted, readable, inside the export bookend range, and have gain above 0.");
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
            if (!clip.GetData(src, 0))
            {
                Debug.LogWarning($"FP Audio Combine Tool skipped '{clip.name}' because Unity could not read its sample data. Reimport the clip as decompressed PCM data before combining it.");
                return;
            }

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
                float fadeGain = CalculateFadeGain(entry, segmentTime);

                for (int outCh = 0; outCh < ch; outCh++)
                {
                    float sample = SampleSource(src, srcFrames, srcCh, srcFrame, outCh, ch) * entry.gain * fadeGain;
                    int dstIndex = frame * ch + outCh;
                    dst[dstIndex] += sample;
                }
            }
        }

        private static float CalculateFadeGain(CombineEntry entry, float segmentTime)
        {
            float fadeGain = 1f;
            if (entry.fadeInEnabled && entry.fadeInDuration > 1e-5f)
            {
                fadeGain *= Mathf.Pow(Mathf.Clamp01(segmentTime / entry.fadeInDuration), ClampFadePower(entry.fadeInPower));
            }

            if (entry.fadeOutEnabled && entry.fadeOutDuration > 1e-5f)
            {
                float timeToEnd = entry.SegmentLength - segmentTime;
                fadeGain *= Mathf.Pow(Mathf.Clamp01(timeToEnd / entry.fadeOutDuration), ClampFadePower(entry.fadeOutPower));
            }

            return fadeGain;
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
            float mixEnd = GetExportEndTime();
            float start = Mathf.Clamp(Mathf.Max(startSec, exportStart), exportStart, mixEnd);
            int startSample = Mathf.Clamp(Mathf.FloorToInt((start - exportStart) * combined.frequency), 0, combined.samples);

            EditorStopAll();
            isPlayingCombined = false;
            activeCombinedPreviewClip = null;
            EditorApplication.update -= OnEditorUpdate;

            activeCombinedPreviewClip = combined;
            AudioClip previewClip = BuildImportedPreviewClip(activeCombinedPreviewClip);
            if (previewClip != null)
            {
                activeCombinedPreviewClip = previewClip;
                startSample = Mathf.Clamp(Mathf.FloorToInt((start - exportStart) * activeCombinedPreviewClip.frequency), 0, activeCombinedPreviewClip.samples);
            }

            EditorPreview(activeCombinedPreviewClip, startSample, false, 1f);
            combinedPlayStartTime = EditorApplication.timeSinceStartup;
            combinedPlayStartOffset = start;
            isPlayingCombined = true;
            playhead = start;
            EditorApplication.update += OnEditorUpdate;
        }

        private AudioClip BuildImportedPreviewClip(AudioClip combined)
        {
            if (combined == null)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(PreviewAssetFolder);
                byte[] wavBytes = FuzzPhyte.Utility.Audio.FP_AudioUtils.ConvertAudioClipToWAV(combined);
                File.WriteAllBytes(PreviewAssetPath, wavBytes);
                AssetDatabase.ImportAsset(PreviewAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                ConfigurePreviewImporter(PreviewAssetPath);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(PreviewAssetPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"FP Audio Combine Tool could not create an imported preview clip, falling back to in-memory preview. {ex.Message}");
                return null;
            }
        }

        private static void ConfigurePreviewImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.loadInBackground)
            {
                importer.loadInBackground = false;
                changed = true;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            AudioClipLoadType desiredLoadType = AudioClipLoadType.DecompressOnLoad;
            AudioCompressionFormat desiredFormat = AudioCompressionFormat.PCM;
            if (settings.loadType != desiredLoadType || settings.compressionFormat != desiredFormat || !settings.preloadAudioData)
            {
                settings.loadType = desiredLoadType;
                settings.compressionFormat = desiredFormat;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
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

            if (TryPlayPreviewClip(clip, startSample, loop, volume))
            {
                return;
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

            Debug.LogWarning("Audio preview not supported on this Unity version (AudioUtil methods not found).");
        }

        private static bool TryPlayPreviewClip(AudioClip clip, int startSample, bool loop, float volume)
        {
            if (miPlayPreviewClip == null)
            {
                return false;
            }

            try
            {
                var prms = miPlayPreviewClip.GetParameters();
                var args = new object[prms.Length];
                for (int i = 0; i < prms.Length; i++)
                {
                    Type parameterType = prms[i].ParameterType;
                    if (parameterType == typeof(AudioClip))
                    {
                        args[i] = clip;
                    }
                    else if (parameterType == typeof(int))
                    {
                        args[i] = startSample;
                    }
                    else if (parameterType == typeof(bool))
                    {
                        args[i] = loop;
                    }
                    else if (parameterType == typeof(float))
                    {
                        args[i] = Mathf.Clamp01(volume);
                    }
                    else
                    {
                        args[i] = prms[i].IsOptional ? Type.Missing : null;
                    }
                }

                miPlayPreviewClip.Invoke(null, args);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AudioUtil.PlayPreviewClip failed; trying alternate preview path. {ex.Message}");
                return false;
            }
        }

        private static void EditorStopAll()
        {
            EnsureAudioUtil();
            if (miStopAllClips != null)
            {
                miStopAllClips.Invoke(null, null);
            }

            if (miStopAllPreview != null)
            {
                miStopAllPreview.Invoke(null, null);
            }
        }
        #endregion
    }
}
