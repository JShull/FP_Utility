namespace FuzzPhyte.Utility
{
    using UnityEngine;

    [ExecuteInEditMode]
    public class FPRenderSelectionManager : MonoBehaviour
    {
        [SerializeField] private FPBlurredBufferMultiObjectOutlineRendererFeature outlineRendererFeature;
        [SerializeField] private Renderer[] selectedRenderers;

        [ContextMenu("Reassign Renderers Now")]
        private void OnValidate()
        {
            if (outlineRendererFeature)
                outlineRendererFeature.SetRenderers(selectedRenderers);
        }
    }
}
