// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Video
{
    using System.Threading;
    using System.Threading.Tasks;

    public class FPVideoPlaybackResolver
    {
        private readonly FPVideoCacheManager cacheManager;

        public FPVideoPlaybackResolver(FPVideoCacheManager manager)
        {
            cacheManager = manager;
        }

        public Task<FPVideoRequestResult> ResolveAsync(string videoId, CancellationToken cancellationToken = default)
        {
            return cacheManager.RequestVideoAsync(videoId, cancellationToken);
        }

        public async Task<string> ResolveLocalPathAsync(string videoId, CancellationToken cancellationToken = default)
        {
            FPVideoRequestResult result = await ResolveAsync(videoId, cancellationToken);
            return result != null && result.Success ? result.ResolvedLocalPath : string.Empty;
        }
    }
}
