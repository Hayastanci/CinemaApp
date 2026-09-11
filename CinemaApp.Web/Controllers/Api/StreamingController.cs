using CinemaApp.Core.Data;
using CinemaApp.Core.Entities;
using CinemaApp.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/stream")]
[Route("api/v1/streaming")]
[Route("api/v1/[controller]")]
public class StreamingController : ControllerBase
{
    private readonly CinemaDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StreamingController> _logger;

    public StreamingController(
        CinemaDbContext dbContext,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<StreamingController> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{movieId:int}/{quality}")]
    [HttpHead("{movieId:int}/{quality}")]
    public async Task<IActionResult> StreamVideo(int movieId, string quality, [FromQuery] string? token)
    {
        // 1. Authenticate user from Bearer header or query token parameter
        User? user = null;
        var authHeader = Request.Headers.Authorization.ToString();
        var rawToken = !string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader.Substring(7).Trim()
            : token;

        if (!string.IsNullOrWhiteSpace(rawToken))
        {
            var userId = _tokenService.ValidateTokenAndGetUserId(rawToken);
            if (userId.HasValue)
            {
                user = await _dbContext.Users.FindAsync(userId.Value);
            }
        }

        // Also check if logged in via ASP.NET Core Cookie session (for MVC Web player)
        if (user == null && User.Identity?.IsAuthenticated == true)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(emailClaim))
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);
            }
        }

        // 2. Quality-Restricted Gatekeeper Check
        var isFreeQuality = StreamQualityConstants.IsFreeQuality(quality);
        var hasAccess = isFreeQuality || (user != null && (user.IsSubscribed || user.Role == UserRole.Admin));

        if (!hasAccess)
        {
            _logger.LogWarning("Access denied for quality {Quality} on Movie Id {MovieId}. User is Guest or unsubscribed.", quality, movieId);
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "SubscriptionRequired",
                message = $"Streaming in {quality.ToUpperInvariant()} requires an active Cinema Pro subscription. Guest and free tiers are limited to 360p and 480p.",
                requiredPlan = "Cinema Pro",
                upgradeUrl = "/subscription",
                allowedQualities = StreamQualityConstants.FreeQualities
            });
        }

        // 3. Locate MediaStream in database (fallback to any ready stream for this movie)
        var mediaStream = await _dbContext.MediaStreams
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MovieId == movieId && s.Quality.ToLower() == quality.ToLower() && s.IsReady);

        if (mediaStream == null)
        {
            mediaStream = await _dbContext.MediaStreams
                .AsNoTracking()
                .OrderByDescending(s => s.Quality == "1080p")
                .ThenByDescending(s => s.Quality == "720p")
                .ThenByDescending(s => s.Quality == "480p")
                .FirstOrDefaultAsync(s => s.MovieId == movieId && s.IsReady);
        }

        var movie = await _dbContext.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movieId);

        // 4. Resolve physical file path on disk
        var filePath = ResolveVideoFilePath(movieId, quality, mediaStream?.FilePath, movie?.MasterVideoPath);

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Video file for Movie {MovieId} Quality {Quality} not found on disk.", movieId, quality);
            return NotFound(new { error = "VideoFileNotFound", message = $"Video file for movie {movieId} not found on server storage." });
        }

        var fileInfo = new FileInfo(filePath);
        var totalLength = fileInfo.Length;

        Response.Headers.Append("Accept-Ranges", "bytes");

        // 5. Handle HTTP Byte-Range Requests (206 Partial Content)
        var rangeHeader = Request.Headers.Range.ToString();
        if (!string.IsNullOrWhiteSpace(rangeHeader) && rangeHeader.StartsWith("bytes="))
        {
            var rangeSpec = rangeHeader.Replace("bytes=", "").Trim();
            var parts = rangeSpec.Split('-');

            long start = 0;
            long end = totalLength - 1;

            if (!string.IsNullOrWhiteSpace(parts[0]))
            {
                if (!long.TryParse(parts[0], out start)) start = 0;
            }

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                if (!long.TryParse(parts[1], out end)) end = totalLength - 1;
            }

            if (start > end || start >= totalLength)
            {
                Response.Headers.Append("Content-Range", $"bytes */{totalLength}");
                return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
            }

            end = Math.Min(end, totalLength - 1);
            var contentLength = end - start + 1;

            Response.StatusCode = StatusCodes.Status206PartialContent;
            Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{totalLength}");
            Response.Headers.Append("Content-Length", contentLength.ToString());
            Response.ContentType = "video/mp4";

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStream.Seek(start, SeekOrigin.Begin);

            return new FileStreamResult(new SubStream(fileStream, contentLength), "video/mp4")
            {
                EnableRangeProcessing = false
            };
        }

        // Return full file stream if no Range header provided
        return PhysicalFile(filePath, "video/mp4", enableRangeProcessing: true);
    }

    private string? ResolveVideoFilePath(int movieId, string quality, string? streamPath, string? masterPath)
    {
        var moviesPath = _configuration["FFmpeg:MoviesPath"] ?? "";
        var mediaRoot = _configuration["FFmpeg:MediaRoot"] ?? "";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidates = new List<string>();

        // 1. Direct streamPath if absolute
        if (!string.IsNullOrWhiteSpace(streamPath))
        {
            candidates.Add(streamPath);
            if (!string.IsNullOrWhiteSpace(moviesPath))
            {
                candidates.Add(Path.Combine(moviesPath, streamPath.TrimStart('/', '\\')));
                candidates.Add(Path.Combine(moviesPath, movieId.ToString(), Path.GetFileName(streamPath)));
            }
            if (!string.IsNullOrWhiteSpace(mediaRoot))
            {
                candidates.Add(Path.Combine(mediaRoot, streamPath.TrimStart('/', '\\')));
                candidates.Add(Path.Combine(mediaRoot, "movies", movieId.ToString(), Path.GetFileName(streamPath)));
            }
        }

        // 2. Candidate paths in moviesPath
        if (!string.IsNullOrWhiteSpace(moviesPath))
        {
            candidates.Add(Path.Combine(moviesPath, movieId.ToString(), $"{quality}.mp4"));
            candidates.Add(Path.Combine(moviesPath, movieId.ToString(), "1080p.mp4"));
            candidates.Add(Path.Combine(moviesPath, movieId.ToString(), "720p.mp4"));
            candidates.Add(Path.Combine(moviesPath, movieId.ToString(), "480p.mp4"));
            candidates.Add(Path.Combine(moviesPath, movieId.ToString(), "360p.mp4"));
            candidates.Add(Path.Combine(moviesPath, movieId.ToString(), "master.mp4"));
        }

        // 3. Candidate paths in mediaRoot
        if (!string.IsNullOrWhiteSpace(mediaRoot))
        {
            candidates.Add(Path.Combine(mediaRoot, "movies", movieId.ToString(), $"{quality}.mp4"));
            candidates.Add(Path.Combine(mediaRoot, "movies", movieId.ToString(), "1080p.mp4"));
            candidates.Add(Path.Combine(mediaRoot, "movies", movieId.ToString(), "720p.mp4"));
            candidates.Add(Path.Combine(mediaRoot, "movies", movieId.ToString(), "480p.mp4"));
            candidates.Add(Path.Combine(mediaRoot, "movies", movieId.ToString(), "360p.mp4"));
            candidates.Add(Path.Combine(mediaRoot, "staging", $"{movieId}.mp4"));
        }

        // 4. MasterVideoPath from database
        if (!string.IsNullOrWhiteSpace(masterPath))
        {
            candidates.Add(masterPath);
        }

        // 5. Local bin directory paths
        candidates.Add(Path.Combine(baseDir, "media", "movies", movieId.ToString(), $"{quality}.mp4"));
        candidates.Add(Path.Combine(baseDir, "media", "movies", movieId.ToString(), "1080p.mp4"));
        candidates.Add(Path.Combine(baseDir, "media", "staging", $"{movieId}.mp4"));

        foreach (var path in candidates)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    return path;
                }
            }
            catch { }
        }

        return null;
    }
}

// Helper SubStream for precisely bounded byte-range serving
public class SubStream : Stream
{
    private readonly Stream _baseStream;
    private readonly long _length;
    private long _position;

    public SubStream(Stream baseStream, long length)
    {
        _baseStream = baseStream;
        _length = length;
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var remaining = _length - _position;
        if (remaining <= 0) return 0;
        var toRead = (int)Math.Min(count, remaining);
        var read = _baseStream.Read(buffer, offset, toRead);
        _position += read;
        return read;
    }

    public override void Flush() => _baseStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _baseStream.Dispose();
        base.Dispose(disposing);
    }
}
