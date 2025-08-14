namespace FuzzPhyte.Utility.Animation.Editor
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.Timeline;
    using UnityEngine;
    using UnityEngine.Timeline;
    [CustomTimelineEditor(typeof(FPSplineCommandMarker))]
    public class FPSplineCommandMarkerEditor : MarkerEditor
    {
        // --- Colors per command ---
        static Color ColorFor(FPSplineCommand cmd) => cmd switch
        {
            FPSplineCommand.Pause => new Color(0.95f, 0.55f, 0.10f), // orange
            FPSplineCommand.Resume => new Color(0.20f, 0.75f, 0.30f), // green
            FPSplineCommand.Stop => new Color(0.80f, 0.20f, 0.20f), // red
            FPSplineCommand.Unstop => new Color(0.60f, 0.40f, 0.85f), // purple
            FPSplineCommand.SetSpeedMultiplier => new Color(0.20f, 0.65f, 0.85f), // teal
            FPSplineCommand.SetT => new Color(0.25f, 0.55f, 0.95f), // blue
            FPSplineCommand.WarpToT => new Color(0.95f, 0.25f, 0.55f), // pink
            _ => new Color(0.5f, 0.5f, 0.5f)
        };
        // --- Different built-in icons per command ---
        static Texture2D IconFor(FPSplineCommand cmd)
        {
            // Uses Unity's built-in icon names; swap to your own textures if you prefer.
            // (The "d_" variants pick dark-skin versions.)
            string name = cmd switch
            {
                FPSplineCommand.Pause => "PauseButton On",
                FPSplineCommand.Resume => "PlayButton On",
                FPSplineCommand.Stop => "d_preAudioLoopOff",
                FPSplineCommand.Unstop => "d_preAudioAutoPlayOff",
                FPSplineCommand.SetSpeedMultiplier => "d_Animation.AddEvent",
                FPSplineCommand.SetT => "d_Animation.AddKeyframe",
                FPSplineCommand.WarpToT => "d_SceneViewOrtho",
                _ => "d_Profiler.Timeline"
            };
            return (Texture2D)EditorGUIUtility.IconContent(name).image;
        }
        public override MarkerDrawOptions GetMarkerOptions(IMarker marker)
        {
            var opts = base.GetMarkerOptions(marker);

            if (marker is FPSplineCommandMarker m)
            {
                // Some Timeline versions only support tooltip here.
                // We'll set tooltip safely; colors/icons will be drawn in DrawOverlay for max compatibility.
                opts.tooltip = m.command switch
                {
                    FPSplineCommand.SetSpeedMultiplier => $"Set Speed x{m.value:0.##}",
                    FPSplineCommand.SetT => $"Set t = {m.value:0.###}",
                    _ => m.command.ToString()
                };

            }

            return opts;
        }
        // Optional: draw a tiny label on top of the marker
        public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region)
        {
            if (marker is not FPSplineCommandMarker m) return;

            var r = region.markerRegion;
            var bg = new Rect(r.x, r.y, r.width, r.height);

            // Background bar (works on all versions)
            EditorGUI.DrawRect(bg, ColorFor(m.command));

            // Icon (works on all versions)
            var icon = IconFor(m.command);
            if (icon != null)
            {
                float size = Mathf.Min(r.height - 2f, 14f);
                var iconRect = new Rect(
                    r.x + (r.width - size) * 0.5f,
                    r.y + 2f,
                    size,
                    size
                );
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            // Tiny label (works on all versions)
            using (new GUIEnabledScope(true))
            using (new GUIColorScope(Color.white))
            {
                var style = EditorStyles.miniBoldLabel;
                style.alignment = TextAnchor.LowerCenter;
                string label = m.command switch
                {
                    FPSplineCommand.SetSpeedMultiplier => $"x{m.value:0.##}",
                    FPSplineCommand.SetT => $"t {m.value:0.###}",
                    _ => m.command.ToString()
                };
                var labelRect = new Rect(r.x, r.y, r.width, r.height - 1f);
                GUI.Label(labelRect, label, style);
            }
        }

        // helpers to keep GUI state sane
        private readonly struct GUIColorScope : System.IDisposable
        {
            private readonly Color prev;
            public GUIColorScope(Color c) { prev = GUI.color; GUI.color = c; }
            public void Dispose() { GUI.color = prev; }
        }
        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool prev;
            public GUIEnabledScope(bool enabled) { prev = GUI.enabled; GUI.enabled = enabled; }
            public void Dispose() { GUI.enabled = prev; }
        }
    }
#endif
}
