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
