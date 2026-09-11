using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using CinemaApp.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class MoviesController : ControllerBase
{
    private readonly CinemaDbContext _dbContext;
    private readonly string _mediaRoot;

    public MoviesController(CinemaDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _mediaRoot = configuration["FFmpeg:MediaRoot"]
            ?? (OperatingSystem.IsWindows()
                ? Path.Combine("C:\\", "CinemaMedia")
                : "/var/www/cinema/media");
    }

    [HttpGet]
    public async Task<IActionResult> GetMovies(
        [FromQuery] string? lang,
        [FromQuery] int? categoryId,
        [FromQuery] string? search)
    {
        // 1. Resolve requested culture or default
        var culture = !string.IsNullOrWhiteSpace(lang) ? lang.Trim() : "en-US";
        var language = await _dbContext.Languages.FirstOrDefaultAsync(l => l.CultureCode.ToLower() == culture.ToLower() && l.IsActive)
                       ?? await _dbContext.Languages.FirstOrDefaultAsync(l => l.IsDefault)
                       ?? await _dbContext.Languages.FirstOrDefaultAsync();

        var langId = language?.Id ?? 1;

        // 2. Query movies
        var query = _dbContext.Movies
            .Include(m => m.MovieTranslations)
            .Include(m => m.Categories)
            .Include(m => m.MediaStreams)
            .AsNoTracking()
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(m => m.Categories.Any(c => c.Id == categoryId.Value));
        }

        var movies = await query.ToListAsync();

        var movieIds = movies.Select(m => m.Id).ToList();
        var ratingMap = await GetRatingAggregatesAsync(movieIds);

        var result = new List<MovieSummaryDto>();

        foreach (var movie in movies)
        {
            var translation = movie.MovieTranslations.FirstOrDefault(t => t.LanguageId == langId)
                              ?? movie.MovieTranslations.FirstOrDefault();

            var title = translation?.Title ?? "Untitled";
            var desc = translation?.Description ?? "";
            var genres = translation?.Genres ?? "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                if (!title.ToLowerInvariant().Contains(term) &&
                    !desc.ToLowerInvariant().Contains(term) &&
                    !genres.ToLowerInvariant().Contains(term))
                {
                    continue;
                }
            }

            ratingMap.TryGetValue(movie.Id, out var rating);
            result.Add(new MovieSummaryDto
            {
                Id = movie.Id,
                Title = title,
                Description = desc,
                Genres = genres,
                ReleaseYear = movie.ReleaseYear,
                DurationMinutes = movie.DurationMinutes,
                PosterUrl = Abs(MediaUrls.ResolvePoster(movie.PosterUrl, movie.Id, _mediaRoot)),
                BannerUrl = Abs(MediaUrls.ResolvePoster(movie.BannerUrl, movie.Id, _mediaRoot)),
                AvailableQualities = movie.MediaStreams.Where(s => s.IsReady).Select(s => s.Quality).ToList(),
                AverageRating = rating.Avg,
                RatingCount = rating.Count
            });
        }

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetMovieById(int id, [FromQuery] string? lang)
    {
        var culture = !string.IsNullOrWhiteSpace(lang) ? lang.Trim() : "en-US";
        var language = await _dbContext.Languages.FirstOrDefaultAsync(l => l.CultureCode.ToLower() == culture.ToLower() && l.IsActive)
                       ?? await _dbContext.Languages.FirstOrDefaultAsync(l => l.IsDefault)
                       ?? await _dbContext.Languages.FirstOrDefaultAsync();

        var langId = language?.Id ?? 1;

        var movie = await _dbContext.Movies
            .Include(m => m.MovieTranslations)
            .Include(m => m.Categories)
                .ThenInclude(c => c.CategoryTranslations)
            .Include(m => m.MediaStreams)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
        {
            return NotFound(new { error = "MovieNotFound", message = $"Movie with Id {id} not found." });
        }

        var translation = movie.MovieTranslations.FirstOrDefault(t => t.LanguageId == langId)
                          ?? movie.MovieTranslations.FirstOrDefault();

        var categories = movie.Categories.Select(c =>
        {
            var catTrans = c.CategoryTranslations.FirstOrDefault(ct => ct.LanguageId == langId)
                           ?? c.CategoryTranslations.FirstOrDefault();
            return new CategoryDto
            {
                Id = c.Id,
                Slug = c.Slug,
                Name = catTrans?.Name ?? c.Slug,
                Description = catTrans?.Description ?? ""
            };
        }).ToList();

        var streams = movie.MediaStreams
            .Where(s => s.IsReady)
            .OrderBy(s => s.Quality switch
            {
                "360p" => 1,
                "480p" => 2,
                "720p" => 3,
                "1080p" => 4,
                _ => 5
            })
            .Select(s => new MediaStreamDto
            {
                Id = s.Id,
                Quality = s.Quality,
                FileSize = s.FileSize,
                IsReady = s.IsReady,
                StreamUrl = Abs($"/api/v1/stream/{movie.Id}/{s.Quality}"),
                RequiresSubscription = !StreamQualityConstants.IsFreeQuality(s.Quality)
            }).ToList();

        var ratingAggregate = await GetMovieRatingAsync(movie.Id);

        return Ok(new MovieDetailDto
        {
            Id = movie.Id,
            Title = translation?.Title ?? "Untitled",
            Description = translation?.Description ?? "",
            Genres = translation?.Genres ?? "",
            ReleaseYear = movie.ReleaseYear,
            DurationMinutes = movie.DurationMinutes,
            PosterUrl = Abs(MediaUrls.ResolvePoster(movie.PosterUrl, movie.Id, _mediaRoot)),
            BannerUrl = Abs(MediaUrls.ResolvePoster(movie.BannerUrl, movie.Id, _mediaRoot)),
            AvailableQualities = streams.Select(s => s.Quality).ToList(),
            Categories = categories,
            Streams = streams,
            AverageRating = ratingAggregate.Avg,
            RatingCount = ratingAggregate.Count
        });
    }

    [HttpGet("{id:int}/rating")]
    public async Task<IActionResult> GetMovieRating(int id)
    {
        var movieExists = await _dbContext.Movies.AnyAsync(m => m.Id == id);
        if (!movieExists)
        {
            return NotFound(new { error = "MovieNotFound", message = $"Movie with Id {id} not found." });
        }

        var rating = await GetMovieRatingAsync(id);

        return Ok(new MovieRatingDto
        {
            MovieId = id,
            Score = 0,
            AverageRating = rating.Avg,
            RatingCount = rating.Count
        });
    }

    [HttpPost("{id:int}/rating")]
    public async Task<IActionResult> SubmitMovieRating(int id, [FromBody] MovieRatingRequestDto request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "InvalidRequest", message = "Request body is required." });
        }

        var movieExists = await _dbContext.Movies.AnyAsync(m => m.Id == id);
        if (!movieExists)
        {
            return NotFound(new { error = "MovieNotFound", message = $"Movie with Id {id} not found." });
        }

        if (request.Score < 1 || request.Score > 5)
        {
            return BadRequest(new { error = "InvalidScore", message = "Score must be between 1 and 5." });
        }

        var visitorId = string.IsNullOrWhiteSpace(request.VisitorId) ? "anonymous" : request.VisitorId.Trim();

        var existing = await _dbContext.MovieRatings
            .FirstOrDefaultAsync(r => r.MovieId == id && r.VisitorId == visitorId);

        bool updated;
        if (existing != null)
        {
            existing.Score = request.Score;
            updated = true;
        }
        else
        {
            _dbContext.MovieRatings.Add(new MovieRating
            {
                MovieId = id,
                VisitorId = visitorId,
                Score = request.Score,
                CreatedAt = DateTime.UtcNow
            });
            updated = false;
        }

        await _dbContext.SaveChangesAsync();

        var rating = await GetMovieRatingAsync(id);
        return Ok(new MovieRatingDto
        {
            MovieId = id,
            Score = request.Score,
            AverageRating = rating.Avg,
            RatingCount = rating.Count,
            Updated = updated
        });
    }

    private string Abs(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url ?? string.Empty;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var origin = $"{Request.Scheme}://{Request.Host}";
        return origin + (url.StartsWith('/') ? url : "/" + url);
    }

    private async Task<(decimal Avg, int Count)> GetMovieRatingAsync(int movieId)
    {
        var ratings = await _dbContext.MovieRatings.Where(r => r.MovieId == movieId).ToListAsync();
        if (ratings.Count == 0)
        {
            return (0m, 0);
        }

        return (Math.Round((decimal)ratings.Average(r => r.Score), 1), ratings.Count);
    }

    private async Task<Dictionary<int, (decimal Avg, int Count)>> GetRatingAggregatesAsync(List<int> movieIds)
    {
        var grouped = await _dbContext.MovieRatings
            .Where(r => movieIds.Contains(r.MovieId))
            .GroupBy(r => r.MovieId)
            .Select(g => new { MovieId = g.Key, Avg = g.Average(r => r.Score), Count = g.Count() })
            .ToListAsync();

        return grouped.ToDictionary(
            g => g.MovieId,
            g => (Math.Round((decimal)g.Avg, 1), g.Count));
    }
}
