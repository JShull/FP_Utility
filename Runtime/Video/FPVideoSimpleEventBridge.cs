// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Video
{
    using UnityEngine;

    public class FPVideoSimpleEventBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FPVideoCacheBootstrap bootstrap;

        [Header("Simple Inspector Events")]
        [SerializeField] private FPVideoStringEvent onVideoIdResolved = new FPVideoStringEvent();
        [SerializeField] private FPVideoStringEvent onResolvedLocalPath = new FPVideoStringEvent();
        [SerializeField] private FPVideoBoolEvent onRequestSuccess = new FPVideoBoolEvent();
        [SerializeField] private FPVideoBoolEvent onSourceWasCache = new FPVideoBoolEvent();
        [SerializeField] private FPVideoBoolEvent onDownloadWasPerformed = new FPVideoBoolEvent();
        [SerializeField] private FPVideoStringEvent onErrorMessage = new FPVideoStringEvent();

        public FPVideoStringEvent OnVideoIdResolved => onVideoIdResolved;
        public FPVideoStringEvent OnResolvedLocalPath => onResolvedLocalPath;
        public FPVideoBoolEvent OnRequestSuccess => onRequestSuccess;
        public FPVideoBoolEvent OnSourceWasCache => onSourceWasCache;
        public FPVideoBoolEvent OnDownloadWasPerformed => onDownloadWasPerformed;
        public FPVideoStringEvent OnErrorMessage => onErrorMessage;

        private void OnEnable()
        {
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.VideoRequestCompleted += HandleVideoRequestCompleted;
        }

        private void OnDisable()
        {
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.VideoRequestCompleted -= HandleVideoRequestCompleted;
        }

        private void HandleVideoRequestCompleted(FPVideoRequestResult result)
        {
            if (result == null)
            {
                onRequestSuccess?.Invoke(false);
                onErrorMessage?.Invoke("Video request result was null.");
                return;
            }

            onVideoIdResolved?.Invoke(result.VideoId ?? string.Empty);
            onResolvedLocalPath?.Invoke(result.ResolvedLocalPath ?? string.Empty);
            onRequestSuccess?.Invoke(result.Success);
            onSourceWasCache?.Invoke(result.SourceWasCache);
            onDownloadWasPerformed?.Invoke(result.DownloadWasPerformed);
            onErrorMessage?.Invoke(result.ErrorMessage ?? string.Empty);
        }
    }
}
