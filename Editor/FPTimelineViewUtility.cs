// Copyright (c) 2026 John B. Shull. See LICENSE.md for GPLv3-or-later / commercial terms.
// Shared by the audio workspace and Motion Studio without expanding the public Utility API.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("com.fuzzphyte.motion.studio.editor")]
namespace FuzzPhyte.Utility.Editor
{
    using System;
    using UnityEditor;
    using UnityEngine;

    internal static class FPTimelineViewUtility
    {
        internal static float TimeToX(Rect rect, double time, double duration) =>
            rect.x + rect.width * (float)(duration > 0 ? Math.Max(0, Math.Min(1, time / duration)) : 0);

        internal static double XToTime(Rect rect, float x, double duration) =>
            Mathf.InverseLerp(rect.x, rect.xMax, x) * Math.Max(0, duration);

        internal static void DrawRuler(Rect rect, double duration) {
            EditorGUI.DrawRect(rect, new Color(.1f, .1f, .1f));
            int ticks = Mathf.Clamp(Mathf.CeilToInt((float)duration), 1, 16);
            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = new Color(1, 1, 1, .18f);
            for (int i = 0; i <= ticks; i++) {
                double time = duration * i / ticks;
                float x = TimeToX(rect, time, duration);
                Handles.DrawLine(new Vector3(x, rect.yMax - 10), new Vector3(x, rect.yMax));
                GUI.Label(new Rect(Mathf.Min(x + 3, rect.xMax - 54), rect.y + 1, 54, 16), $"{time:F1}", EditorStyles.miniLabel);
            }
            Handles.color = previous;
            Handles.EndGUI();
        }

        internal static void DrawPlayhead(Rect rect, double time, double duration, Color color, bool handle = false) {
            float x = TimeToX(rect, time, duration);
            EditorGUI.DrawRect(new Rect(x - (handle ? 1.5f : .5f), rect.y, handle ? 3 : 1, rect.height), color);
            if (handle) EditorGUI.DrawRect(new Rect(x - 6, rect.y, 12, 7), Color.Lerp(color, Color.white, .18f));
        }
    }
}
