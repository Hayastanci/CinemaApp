namespace CinemaApp.Web.Helpers;

public static class MediaUrls
{
    public const string FallbackPoster =
        "https://images.unsplash.com/photo-1536440136628-849c177e76a1?auto=format&fit=crop&w=600&q=80";

    /// <summary>
    /// Prefer the transcode-generated poster (poster.jpg next to the movie's video files).
    /// Legacy/placeholder paths under /images/ resolve to the generated poster so browsers
    /// request poster.jpg directly instead of a 404 that then drops to the remote fallback.
    /// If the generated poster file does not exist on disk, the remote fallback image is used
    /// so clients never receive a broken 404 image URL.
    /// </summary>
    public static string ResolvePoster(string? posterUrl, int movieId, string? mediaRoot = null)
    {
        var relative = ResolvePosterRelative(posterUrl, movieId);

        if (!string.IsNullOrWhiteSpace(mediaRoot) &&
            IsGeneratedPoster(relative) &&
            !FileExists(mediaRoot, relative))
        {
            return FallbackPoster;
        }

        return relative;
    }

    private static string ResolvePosterRelative(string? posterUrl, int movieId)
    {
        if (string.IsNullOrWhiteSpace(posterUrl))
        {
            return $"/media/movies/{movieId}/poster.jpg";
        }

        if (posterUrl.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            return $"/media/movies/{movieId}/poster.jpg";
        }

        return posterUrl;
    }

    private static bool IsGeneratedPoster(string relative)
    {
        return relative.StartsWith("/media/movies/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool FileExists(string mediaRoot, string relativeUrl)
    {
        try
        {
            // relativeUrl like "/media/movies/2/poster.jpg" -> path under mediaRoot: "movies/2/poster.jpg"
            var rel = relativeUrl.TrimStart('/');
            if (rel.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
            {
                rel = rel["media/".Length..];
            }

            var physical = Path.Combine(mediaRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(physical);
        }
        catch
        {
            return true;
        }
    }
}