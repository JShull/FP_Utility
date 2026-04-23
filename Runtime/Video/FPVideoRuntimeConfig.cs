namespace FuzzPhyte.Utility.Video
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "FPVideoRuntimeConfig", menuName = "FuzzPhyte/Utility/Video/Runtime Config")]
    public class FPVideoRuntimeConfig : ScriptableObject
    {
        [Header("Manifest")]
        [SerializeField] private string manifestUrl;

        [Header("Cache")]
        [SerializeField] private string cacheRootFolderName = "VideoCache";
        [SerializeField] private string videosFolderName = "videos";
        [SerializeField] private string metadataFolderName = "meta";

        [Header("Validation")]
        [SerializeField] private FPVideoValidationMode validationMode = FPVideoValidationMode.Strict;
        [SerializeField] private bool enableHashValidation = true;
        [SerializeField] private bool enableSizeValidation = true;

        [Header("Startup")]
        [SerializeField] private bool preloadAllVideosOnInitialize;

        public string ManifestUrl => manifestUrl;
        public string CacheRootFolderName => string.IsNullOrWhiteSpace(cacheRootFolderName) ? "VideoCache" : cacheRootFolderName.Trim();
        public string VideosFolderName => string.IsNullOrWhiteSpace(videosFolderName) ? "videos" : videosFolderName.Trim();
        public string MetadataFolderName => string.IsNullOrWhiteSpace(metadataFolderName) ? "meta" : metadataFolderName.Trim();
        public FPVideoValidationMode ValidationMode => validationMode;
        public bool EnableHashValidation => enableHashValidation;
        public bool EnableSizeValidation => enableSizeValidation;
        public bool PreloadAllVideosOnInitialize => preloadAllVideosOnInitialize;
        public bool UseStrictValidation => validationMode == FPVideoValidationMode.Strict;
    }
}
