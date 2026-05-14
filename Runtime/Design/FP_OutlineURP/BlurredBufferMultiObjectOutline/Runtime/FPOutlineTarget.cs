namespace FuzzPhyte.Utility
{
    using UnityEngine;

    /// <summary>
    /// Add this component to any object that should be included in the URP outline pass.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class FPOutlineTarget : MonoBehaviour
    {
        [SerializeField] private bool includeChildren = true;
        [SerializeField] private Renderer[] explicitRenderers;
        [SerializeField] private FPOutlineProfile outlineProfile;
        [SerializeField, ColorUsage(false, true)] private Color outlineColor = Color.cyan;
        [SerializeField] private FPOutlineAlphaMode alphaMode = FPOutlineAlphaMode.MeshSilhouette;
        [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.5f;
        [SerializeField] private Texture customMaskTexture;

        private Renderer[] _cachedRenderers = new Renderer[0];

        public Color OutlineColor
        {
            get => outlineProfile ? outlineProfile.OutlineColor : outlineColor;
            set => outlineColor = value;
        }

        public FPOutlineAlphaMode AlphaMode
        {
            get => outlineProfile ? outlineProfile.AlphaMode : alphaMode;
            set => alphaMode = value;
        }

        public float AlphaCutoff
        {
            get => outlineProfile ? outlineProfile.AlphaCutoff : alphaCutoff;
            set => alphaCutoff = Mathf.Clamp01(value);
        }

        public Texture CustomMaskTexture
        {
            get => outlineProfile ? outlineProfile.CustomMaskTexture : customMaskTexture;
            set => customMaskTexture = value;
        }

        public FPOutlineProfile OutlineProfile
        {
            get => outlineProfile;
            set => outlineProfile = value;
        }

        public bool IncludeChildren
        {
            get => includeChildren;
            set
            {
                if (includeChildren == value)
                    return;

                includeChildren = value;
                RefreshRenderers();
            }
        }

        public Renderer[] Renderers => _cachedRenderers;

        public bool HasRenderableRenderers
        {
            get
            {
                if (_cachedRenderers == null || _cachedRenderers.Length == 0)
                    return false;

                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    Renderer targetRenderer = _cachedRenderers[i];
                    if (targetRenderer && targetRenderer.enabled && targetRenderer.gameObject.activeInHierarchy)
                        return true;
                }

                return false;
            }
        }

        public void SetExplicitRenderers(Renderer[] renderers)
        {
            explicitRenderers = renderers;
            RefreshRenderers();
        }

        public void RefreshRenderers()
        {
            if (explicitRenderers != null && explicitRenderers.Length > 0)
            {
                _cachedRenderers = explicitRenderers;
                return;
            }

            if (includeChildren)
            {
                _cachedRenderers = GetComponentsInChildren<Renderer>(true);
                return;
            }

            Renderer ownRenderer = GetComponent<Renderer>();
            _cachedRenderers = ownRenderer ? new[] { ownRenderer } : new Renderer[0];
        }

        private void OnEnable()
        {
            RefreshRenderers();
            FPOutlineRegistry.Register(this);
        }

        private void OnDisable()
        {
            FPOutlineRegistry.Unregister(this);
        }

        private void OnTransformChildrenChanged()
        {
            if (includeChildren && (explicitRenderers == null || explicitRenderers.Length == 0))
                RefreshRenderers();
        }

        private void OnValidate()
        {
            alphaCutoff = Mathf.Clamp01(alphaCutoff);
            RefreshRenderers();
        }
    }
}
