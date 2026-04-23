namespace FuzzPhyte.Utility.Video
{
    using System;
    using UnityEngine.Events;

    [Serializable]
    public class FPVideoManifestItem
    {
        public string id;
        public string version;
        public string fileName;
        public string downloadUrl;
        public string sha256;
        public long contentLength;
    }

    [Serializable]
    public class FPVideoManifestCollection
    {
        public FPVideoManifestItem[] videos;
    }

    [Serializable]
    public class FPVideoLocalMeta
    {
        public string id;
        public string version;
        public string sha256;
        public string fileName;
        public long contentLength;
        public string cachedAtUtc;
    }

    [Serializable]
    public class FPVideoRequestResult
    {
        public bool Success;
        public string VideoId;
        public string ResolvedLocalPath;
        public bool SourceWasCache;
        public bool DownloadWasPerformed;
        public string ErrorMessage;
        public FPVideoManifestItem ManifestItem;
        public FPVideoLocalMeta LocalMeta;
    }

    public enum FPVideoValidationMode
    {
        Fast = 0,
        Strict = 1
    }

    [Serializable]
    public class FPVideoManifestItemEvent : UnityEvent<FPVideoManifestItem>
    {
    }

    [Serializable]
    public class FPVideoRequestResultEvent : UnityEvent<FPVideoRequestResult>
    {
    }

    [Serializable]
    public class FPVideoStringEvent : UnityEvent<string>
    {
    }

    [Serializable]
    public class FPVideoBoolEvent : UnityEvent<bool>
    {
    }
}
