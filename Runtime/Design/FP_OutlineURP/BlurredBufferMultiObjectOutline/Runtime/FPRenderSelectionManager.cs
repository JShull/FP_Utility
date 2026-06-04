// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

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
