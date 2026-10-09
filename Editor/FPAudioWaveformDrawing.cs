// Copyright (c) 2026 John B. Shull. See LICENSE.md.
namespace FuzzPhyte.Utility.Editor
{
    using UnityEditor;
    using UnityEngine;
    /// <summary>Shared signed waveform envelope column used by audio timelines.</summary>
    public static class FPAudioWaveformDrawing
    {
        public static void DrawColumn(Rect rect, float x, float minimum, float maximum, float padding = 2)
        {
            float half = Mathf.Max(0, rect.height * .5f - padding);
            float top = Mathf.Clamp(rect.center.y - Mathf.Clamp(maximum, -1, 1) * half, rect.y + padding, rect.yMax - padding);
            float bottom = Mathf.Clamp(rect.center.y - Mathf.Clamp(minimum, -1, 1) * half, rect.y + padding, rect.yMax - padding);
            Handles.DrawLine(new Vector3(x, top), new Vector3(x, bottom));
        }
    }
}
