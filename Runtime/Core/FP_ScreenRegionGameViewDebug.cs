namespace FuzzPhyte.Utility
{
#if UNITY_EDITOR
    using UnityEngine;
    using UnityEditor;
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FP_ScreenRegionGameViewDebug : MonoBehaviour
    {
        [SerializeField] private bool _draw = true;

        [Tooltip("Normalized rect (0..1) in screen space. (0,0)=bottom-left).")]
        [SerializeField] private FP_ScreenRegionAsset _regionAsset;
        [SerializeField] private Color _fill = new Color(0f, 1f, 1f, 0.15f);
        [SerializeField] private Color _outline = new Color(0f, 1f, 1f, 0.9f);
        [SerializeField] private float _outlineThickness = 2f;

        private Texture2D _tex;

        public void SetRegionAsset(FuzzPhyte.Utility.FP_ScreenRegionAsset asset)
        {
            _regionAsset = asset;
            RequestEditorRepaint();
        }

        private void OnEnable()
        {
            EnsureTex();
            RequestEditorRepaint();
        }

        private void OnDisable()
        {
            if (_tex != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(_tex);
                else
#endif
                    Destroy(_tex);
                _tex = null;
            }
        }

        private void OnValidate()
        {
            EnsureTex();
            RequestEditorRepaint();
        }

        private void EnsureTex()
        {
            if (_tex != null) return;
            _tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _tex.SetPixel(0, 0, Color.white);
            _tex.Apply();
        }

        private void OnGUI()
        {
#if UNITY_EDITOR
            if(Application.isPlaying) return;
#endif
            if (!_draw) return;
            if (_tex == null) EnsureTex();
            if (_regionAsset == null) return;

            foreach(var aRegion in _regionAsset.Region)
            {
                Rect pixel = ToGameViewRect(aRegion.NormalizedRect);

                GUI.color = _fill;
                GUI.DrawTexture(pixel, _tex);

                GUI.color = _outline;
                DrawOutline(pixel, _outlineThickness);

                GUI.color = Color.white;
            }
           
        }

        private static Rect ToGameViewRect(Rect normalized)
        {
            float w = Screen.width;
            float h = Screen.height;

            // OnGUI origin is top-left; normalized is bottom-left
            float x = normalized.xMin * w;
            float yTop = (1f - normalized.yMax) * h;

            return new Rect(
                x,
                yTop,
                normalized.width * w,
                normalized.height * h
            );
        }

        private void DrawOutline(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), _tex);             // top
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), _tex);         // bottom
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), _tex);            // left
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), _tex);        // right
        }

        private static void RequestEditorRepaint()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;

            // Repaint GameView/SceneView so changes show immediately.
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
        }
    }

#endif
}
