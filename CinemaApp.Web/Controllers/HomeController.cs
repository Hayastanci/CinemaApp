using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using CinemaApp.Web.Helpers;
using CinemaApp.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers;

public class HomeController : Controller
{
    private readonly CinemaDbContext _dbContext;

    public HomeController(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private string ResolveCulture(string? lang)
    {
        if (!string.IsNullOrWhiteSpace(lang))
            return lang.Trim();

        var cookieLang = Request.Cookies["cinema_lang"];
        return !string.IsNullOrWhiteSpace(cookieLang) ? cookieLang : "en-US";
    }

    public async Task<IActionResult> Index(
        [FromQuery] string? lang,
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int? year,
        [FromQuery] decimal? minRating)
    {
        var culture = ResolveCulture(lang);
        var activeLanguages = await _dbContext.Languages.Where(l => l.IsActive).OrderByDescending(l => l.IsDefault).ToListAsync();
        var currentLang = activeLanguages.FirstOrDefault(l => l.CultureCode.Equals(culture, StringComparison.OrdinalIgnoreCase))
                          ?? activeLanguages.FirstOrDefault(l => l.IsDefault)
                          ?? activeLanguages.FirstOrDefault();

        var langId = currentLang?.Id ?? 1;

        // Categories for sidebar (with translated names)
        var categories = await _dbContext.Categories
            .Include(c => c.CategoryTranslations)
            .AsNoTracking()
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Slug = c.Slug,
                Name = (c.CategoryTranslations.FirstOrDefault(t => t.LanguageId == langId) ?? c.CategoryTranslations.FirstOrDefault())!.Name ?? c.Slug,
                Description = (c.CategoryTranslations.FirstOrDefault(t => t.LanguageId == langId) ?? c.CategoryTranslations.FirstOrDefault())!.Description ?? ""
            })
            .ToListAsync();

        // Query Movies
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

        if (year.HasValue)
        {
            query = query.Where(m => m.ReleaseYear == year.Value);
        }

        var movieList = await query.ToListAsync();

        // Attach rating aggregates up-front so filtering/sorting can use them
        var ratingMap = await GetRatingAggregatesAsync(movieList.Select(m => m.Id).ToList());
        var createdMap = movieList.ToDictionary(m => m.Id, m => m.CreatedAt);

        var movieDtos = new List<MovieSummaryDto>();

        foreach (var m in movieList)
        {
            var trans = m.MovieTranslations.FirstOrDefault(t => t.LanguageId == langId)
                        ?? m.MovieTranslations.FirstOrDefault();

            var title = trans?.Title ?? "Untitled";
            var desc = trans?.Description ?? "";
            var genres = trans?.Genres ?? "";

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

            ratingMap.TryGetValue(m.Id, out var rating);
            movieDtos.Add(new MovieSummaryDto
            {
                Id = m.Id,
                Title = title,
                Description = desc,
                Genres = genres,
                ReleaseYear = m.ReleaseYear,
                DurationMinutes = m.DurationMinutes,
                PosterUrl = MediaUrls.ResolvePoster(m.PosterUrl, m.Id),
                BannerUrl = m.BannerUrl,
                AvailableQualities = m.MediaStreams.Where(s => s.IsReady).Select(s => s.Quality).ToList(),
                AverageRating = rating.Avg,
                RatingCount = rating.Count
            });
        }

        // High-rated filter (min average rating)
        if (minRating.HasValue && minRating.Value > 0)
        {
            movieDtos = movieDtos
                .Where(m => m.RatingCount > 0 && m.AverageRating >= minRating.Value)
                .ToList();
        }

        // Sorting
        var selectedSort = string.IsNullOrWhiteSpace(sort) ? "new" : sort.Trim().ToLowerInvariant();
        movieDtos = selectedSort switch
        {
            "top" => movieDtos.OrderByDescending(m => m.AverageRating).ThenByDescending(m => m.RatingCount).ToList(),
            _ => movieDtos.OrderByDescending(m => createdMap.GetValueOrDefault(m.Id)).ToList()
        };

        // New Releases strip: newest 8 movies by CreatedAt
        var newReleases = new List<MovieSummaryDto>();
        foreach (var m in movieList.OrderByDescending(m => m.CreatedAt).Take(8))
        {
            var trans = m.MovieTranslations.FirstOrDefault(t => t.LanguageId == langId)
                        ?? m.MovieTranslations.FirstOrDefault();
            ratingMap.TryGetValue(m.Id, out var rating);
            newReleases.Add(new MovieSummaryDto
            {
                Id = m.Id,
                Title = trans?.Title ?? "Untitled",
                Description = trans?.Description ?? "",
                Genres = trans?.Genres ?? "",
                ReleaseYear = m.ReleaseYear,
                DurationMinutes = m.DurationMinutes,
                PosterUrl = MediaUrls.ResolvePoster(m.PosterUrl, m.Id),
                BannerUrl = m.BannerUrl,
                AvailableQualities = m.MediaStreams.Where(s => s.IsReady).Select(s => s.Quality).ToList(),
                AverageRating = rating.Avg,
                RatingCount = rating.Count
            });
        }

        var years = await _dbContext.Movies.Select(m => m.ReleaseYear).Distinct().OrderByDescending(y => y).ToListAsync();

        var viewModel = new HomeCatalogViewModel
        {
            Movies = movieDtos,
            NewReleases = newReleases,
            Categories = categories,
            Languages = activeLanguages.Select(l => new LanguageDto
            {
                Id = l.Id,
                CultureCode = l.CultureCode,
                DisplayName = l.DisplayName,
                IsActive = l.IsActive,
                IsDefault = l.IsDefault
            }).ToList(),
            CurrentCulture = currentLang?.CultureCode ?? "en-US",
            SelectedCategoryId = categoryId,
            SearchQuery = search,
            SelectedSort = selectedSort,
            Years = years,
            SelectedYear = year,
            MinRating = minRating,
            FeaturedMovie = newReleases.FirstOrDefault()
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Watch(int id, [FromQuery] string? quality, [FromQuery] string? lang)
    {
        var culture = ResolveCulture(lang);
        var activeLanguages = await _dbContext.Languages.Where(l => l.IsActive).OrderByDescending(l => l.IsDefault).ToListAsync();
        var currentLang = activeLanguages.FirstOrDefault(l => l.CultureCode.Equals(culture, StringComparison.OrdinalIgnoreCase))
                          ?? activeLanguages.FirstOrDefault(l => l.IsDefault)
                          ?? activeLanguages.FirstOrDefault();

        var langId = currentLang?.Id ?? 1;

        var movie = await _dbContext.Movies
            .Include(m => m.MovieTranslations)
            .Include(m => m.MediaStreams)
            .Include(m => m.Categories)
                .ThenInclude(c => c.CategoryTranslations)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
        {
            return NotFound();
        }

        var trans = movie.MovieTranslations.FirstOrDefault(t => t.LanguageId == langId)
                    ?? movie.MovieTranslations.FirstOrDefault();

        var streams = movie.MediaStreams.Where(s => s.IsReady).ToList();
        var selectedQuality = !string.IsNullOrWhiteSpace(quality)
            ? quality
            : (streams.FirstOrDefault(s => s.Quality == "480p")?.Quality ?? streams.FirstOrDefault()?.Quality ?? "360p");

        var isSubscribed = false;
        if (User.Identity?.IsAuthenticated == true)
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                isSubscribed = user?.IsSubscribed == true || user?.Role == UserRole.Admin;
            }
        }

        var requiresSub = !StreamQualityConstants.IsFreeQuality(selectedQuality) && !isSubscribed;

        var streamUrl = $"/api/v1/stream/{movie.Id}/{selectedQuality}";

        var rating = await GetMovieRatingAsync(movie.Id);

        var viewModel = new WatchViewModel
        {
            Movie = new MovieDetailDto
            {
                Id = movie.Id,
                Title = trans?.Title ?? "Untitled",
                Description = trans?.Description ?? "",
                Genres = trans?.Genres ?? "",
                ReleaseYear = movie.ReleaseYear,
                DurationMinutes = movie.DurationMinutes,
                PosterUrl = MediaUrls.ResolvePoster(movie.PosterUrl, movie.Id),
                BannerUrl = movie.BannerUrl,
                AvailableQualities = streams.Select(s => s.Quality).ToList(),
                AverageRating = rating.Avg,
                RatingCount = rating.Count
            },
            CurrentQuality = selectedQuality,
            StreamUrl = streamUrl,
            UserIsSubscribed = isSubscribed,
            RequiresSubscription = requiresSub,
            AllQualities = StreamQualityConstants.All.ToList(),
            CurrentCulture = currentLang?.CultureCode ?? "en-US",
            Languages = activeLanguages.Select(l => new LanguageDto
            {
                Id = l.Id,
                CultureCode = l.CultureCode,
                DisplayName = l.DisplayName,
                IsActive = l.IsActive,
                IsDefault = l.IsDefault
            }).ToList()
        };

        return View(viewModel);
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