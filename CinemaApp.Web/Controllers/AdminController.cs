using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using CinemaApp.Core.Services;
using CinemaApp.Web.Helpers;
using CinemaApp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly CinemaDbContext _dbContext;
    private readonly ITranscodingQueue _transcodingQueue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        CinemaDbContext dbContext,
        ITranscodingQueue transcodingQueue,
        IConfiguration configuration,
        ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _transcodingQueue = transcodingQueue;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var movies = await _dbContext.Movies
            .Include(m => m.MovieTranslations)
            .Include(m => m.MediaStreams)
            .OrderByDescending(m => m.Id)
            .Take(10)
            .ToListAsync();

        var defaultLang = await _dbContext.Languages.FirstOrDefaultAsync(l => l.IsDefault) ?? await _dbContext.Languages.FirstOrDefaultAsync();
        var defaultLangId = defaultLang?.Id ?? 1;

        var recentMovieDtos = movies.Select(m =>
        {
            var trans = m.MovieTranslations.FirstOrDefault(t => t.LanguageId == defaultLangId)
                        ?? m.MovieTranslations.FirstOrDefault();
            return new MovieSummaryDto
            {
                Id = m.Id,
                Title = trans?.Title ?? "Untitled",
                Description = trans?.Description ?? "",
                Genres = trans?.Genres ?? "",
                ReleaseYear = m.ReleaseYear,
                DurationMinutes = m.DurationMinutes,
                PosterUrl = MediaUrls.ResolvePoster(m.PosterUrl, m.Id),
                BannerUrl = m.BannerUrl,
                AvailableQualities = m.MediaStreams.Where(s => s.IsReady).Select(s => s.Quality).ToList()
            };
        }).ToList();

        var activeLanguages = await _dbContext.Languages.ToListAsync();

        var viewModel = new AdminDashboardViewModel
        {
            TotalMovies = await _dbContext.Movies.CountAsync(),
            TotalCategories = await _dbContext.Categories.CountAsync(),
            TotalStreams = await _dbContext.MediaStreams.CountAsync(s => s.IsReady),
            ActiveLanguagesCount = activeLanguages.Count(l => l.IsActive),
            TotalSubscribers = await _dbContext.Users.CountAsync(u => u.IsSubscribed),
            OpenTicketsCount = await _dbContext.SupportTickets.CountAsync(t => t.Status != "Closed"),
            UnansweredTicketsCount = await _dbContext.SupportTickets.CountAsync(t => t.Status != "Closed" && t.Status != "Resolved" && t.HasUnreadUserReply),
            RecentMovies = recentMovieDtos,
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

    [HttpGet]
    public async Task<IActionResult> Languages()
    {
        var languages = await _dbContext.Languages.OrderByDescending(l => l.IsDefault).ThenBy(l => l.DisplayName).ToListAsync();
        return View(languages);
    }

    [HttpPost]
    public async Task<IActionResult> AddLanguage(string cultureCode, string displayName, bool isActive = true, bool isDefault = false)
    {
        if (!string.IsNullOrWhiteSpace(cultureCode) && !string.IsNullOrWhiteSpace(displayName))
        {
            var existing = await _dbContext.Languages.FirstOrDefaultAsync(l => l.CultureCode.ToLower() == cultureCode.Trim().ToLower());
            if (existing != null)
            {
                existing.DisplayName = displayName.Trim();
                existing.IsActive = isActive;
                if (isDefault) existing.IsDefault = true;
            }
            else
            {
                _dbContext.Languages.Add(new Language
                {
                    CultureCode = cultureCode.Trim(),
                    DisplayName = displayName.Trim(),
                    IsActive = isActive,
                    IsDefault = isDefault
                });
            }
            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Language '{displayName}' configured successfully.";
        }

        return RedirectToAction(nameof(Languages));
    }

    [HttpGet]
    public async Task<IActionResult> CreateMovie()
    {
        var activeLanguages = await GetActiveLanguagesAsync();
        var categories = await GetCategoryDtosAsync();

        var viewModel = new MovieCreateViewModel
        {
            ReleaseYear = DateTime.UtcNow.Year,
            DurationMinutes = 120,
            AvailableCategories = categories,
            Translations = activeLanguages.Select(l => new MovieTranslationFormItem
            {
                LanguageId = l.Id,
                CultureCode = l.CultureCode,
                DisplayName = l.DisplayName,
                IsDefault = l.IsDefault,
                Title = "",
                Description = "",
                Genres = ""
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [RequestSizeLimit(60_000_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60_000_000_000)]
    public async Task<IActionResult> CreateMovie(MovieCreateViewModel? model, IFormFile? masterVideo)
    {
        if (model is null)
        {
            _logger.LogWarning("CreateMovie POST received a null model. ContentType={ContentType}, FormCount={FormCount}, HasFile={HasFile}",
                Request.ContentType, Request.Form.Count, Request.HasFormContentType && Request.Form.Files.Count > 0);
            TempData["ErrorMessage"] = "The movie form could not be submitted. Please try again — make sure your browser submits the form fields (cookies/JS enabled).";
            return RedirectToAction(nameof(CreateMovie));
        }

        model.Translations ??= new();
        model.SelectedCategoryIds ??= new();

        if (!ModelState.IsValid)
        {
            var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            _logger.LogWarning("CreateMovie POST failed model validation: {Errors}", errors);
            TempData["ErrorMessage"] = $"Form validation failed: {errors}";
            return RedirectToAction(nameof(CreateMovie));
        }

        var movie = new Movie
        {
            ReleaseYear = model.ReleaseYear,
            DurationMinutes = model.DurationMinutes,
            CreatedAt = DateTime.UtcNow,
            PosterUrl = model.PosterUrl ?? MediaUrls.FallbackPoster,
            BannerUrl = model.BannerUrl ?? MediaUrls.FallbackPoster
        };

        await AttachCategoriesAsync(movie, model.SelectedCategoryIds);
        var genresByLanguage = GetGenresByLanguage(movie.Categories);
        ApplyTranslations(movie, model.Translations, genresByLanguage);

        _dbContext.Movies.Add(movie);
        await _dbContext.SaveChangesAsync();

        var videoMessage = await StageMasterVideoAsync(movie, masterVideo);
        TempData["SuccessMessage"] = $"Movie #{movie.Id} created successfully. {videoMessage}";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditMovie(int id)
    {
        var movie = await _dbContext.Movies
            .Include(m => m.MovieTranslations)
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
        {
            TempData["ErrorMessage"] = "Movie not found.";
            return RedirectToAction(nameof(Index));
        }

        var activeLanguages = await GetActiveLanguagesAsync();
        var categories = await GetCategoryDtosAsync();

        var selectedIds = movie.Categories.Select(c => c.Id).ToList();

        var viewModel = new MovieCreateViewModel
        {
            Id = movie.Id,
            ReleaseYear = movie.ReleaseYear,
            DurationMinutes = movie.DurationMinutes,
            PosterUrl = MediaUrls.ResolvePoster(movie.PosterUrl, movie.Id),
            BannerUrl = movie.BannerUrl,
            SelectedCategoryIds = selectedIds,
            AvailableCategories = categories,
            Translations = activeLanguages.Select(l =>
            {
                var existing = movie.MovieTranslations.FirstOrDefault(t => t.LanguageId == l.Id);
                return new MovieTranslationFormItem
                {
                    LanguageId = l.Id,
                    CultureCode = l.CultureCode,
                    DisplayName = l.DisplayName,
                    IsDefault = l.IsDefault,
                    Title = existing?.Title ?? "",
                    Description = existing?.Description ?? "",
                    Genres = existing?.Genres ?? ""
                };
            }).ToList()
        };

        return View("CreateMovie", viewModel);
    }

    [HttpPost]
    [RequestSizeLimit(60_000_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 60_000_000_000)]
    public async Task<IActionResult> EditMovie(MovieCreateViewModel? model, IFormFile? masterVideo)
    {
        if (model is null)
        {
            TempData["ErrorMessage"] = "The movie form could not be submitted. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        model.Translations ??= new();
        model.SelectedCategoryIds ??= new();

        var movie = await _dbContext.Movies
            .Include(m => m.MovieTranslations)
            .Include(m => m.Categories)
            .FirstOrDefaultAsync(m => m.Id == model.Id);

        if (movie == null)
        {
            TempData["ErrorMessage"] = "Movie not found.";
            return RedirectToAction(nameof(Index));
        }

        movie.ReleaseYear = model.ReleaseYear;
        movie.DurationMinutes = model.DurationMinutes;
        if (!string.IsNullOrWhiteSpace(model.PosterUrl))
        {
            movie.PosterUrl = model.PosterUrl;
        }
        if (!string.IsNullOrWhiteSpace(model.BannerUrl))
        {
            movie.BannerUrl = model.BannerUrl;
        }

        // Update category associations
        movie.Categories.Clear();
        await AttachCategoriesAsync(movie, model.SelectedCategoryIds);
        var genresByLanguage = GetGenresByLanguage(movie.Categories);

        // Update translations (upsert), remove translations no longer posted
        var postedLanguageIds = model.Translations
            .Where(t => !string.IsNullOrWhiteSpace(t.Title))
            .Select(t => t.LanguageId)
            .ToHashSet();

        foreach (var t in model.Translations)
        {
            if (string.IsNullOrWhiteSpace(t.Title))
            {
                continue;
            }

            var genres = genresByLanguage.TryGetValue(t.LanguageId, out var derivedGenres) ? derivedGenres : "";
            var existing = movie.MovieTranslations.FirstOrDefault(mt => mt.LanguageId == t.LanguageId);
            if (existing != null)
            {
                existing.Title = t.Title.Trim();
                existing.Description = t.Description?.Trim() ?? "";
                existing.Genres = genres;
            }
            else
            {
                movie.MovieTranslations.Add(new MovieTranslation
                {
                    LanguageId = t.LanguageId,
                    Title = t.Title.Trim(),
                    Description = t.Description?.Trim() ?? "",
                    Genres = genres
                });
            }
        }

        foreach (var stale in movie.MovieTranslations.Where(mt => !postedLanguageIds.Contains(mt.LanguageId)).ToList())
        {
            _dbContext.MovieTranslations.Remove(stale);
        }

        await _dbContext.SaveChangesAsync();

        var videoMessage = await StageMasterVideoAsync(movie, masterVideo);
        TempData["SuccessMessage"] = $"Movie #{movie.Id} updated successfully. {videoMessage}";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMovie(int id)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.Id == id);
        if (movie == null)
        {
            TempData["ErrorMessage"] = "Movie not found.";
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Movies.Remove(movie);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Movie #{id} deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var languages = await GetActiveLanguagesAsync();
        var categories = await _dbContext.Categories
            .Include(c => c.CategoryTranslations)
                .ThenInclude(t => t.Language)
            .OrderBy(c => c.Slug)
            .ToListAsync();

        var viewModel = new CategoryAdminViewModel
        {
            Categories = categories.Select(c => new CategoryAdminItem
            {
                Id = c.Id,
                Slug = c.Slug,
                Translations = c.CategoryTranslations.Select(t => new CategoryTranslationFormItem
                {
                    LanguageId = t.LanguageId,
                    CultureCode = t.Language?.CultureCode ?? "",
                    DisplayName = t.Language?.DisplayName ?? "",
                    Name = t.Name,
                    Description = t.Description
                }).ToList()
            }).ToList(),
            Languages = languages.Select(l => new LanguageDto
            {
                Id = l.Id,
                CultureCode = l.CultureCode,
                DisplayName = l.DisplayName,
                IsActive = l.IsActive,
                IsDefault = l.IsDefault
            }).ToList(),
            ErrorMessage = TempData["ErrorMessage"] as string,
            SuccessMessage = TempData["SuccessMessage"] as string
        };

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory(string slug, List<CategoryTranslationFormItem> Translations)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            TempData["ErrorMessage"] = "Slug is required.";
            return RedirectToAction(nameof(Categories));
        }

        slug = slug.Trim().ToLowerInvariant();
        var slugExists = await _dbContext.Categories.AnyAsync(c => c.Slug == slug);
        if (slugExists)
        {
            TempData["ErrorMessage"] = $"A category with slug '{slug}' already exists.";
            return RedirectToAction(nameof(Categories));
        }

        if (Translations == null || !Translations.Any(t => !string.IsNullOrWhiteSpace(t.Name)))
        {
            TempData["ErrorMessage"] = "Provide at least one category name.";
            return RedirectToAction(nameof(Categories));
        }

        var category = new Category { Slug = slug };
        foreach (var t in Translations)
        {
            if (string.IsNullOrWhiteSpace(t.Name))
            {
                continue;
            }

            category.CategoryTranslations.Add(new CategoryTranslation
            {
                LanguageId = t.LanguageId,
                Name = t.Name.Trim(),
                Description = t.Description?.Trim() ?? ""
            });
        }

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Category '{slug}' created successfully.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
        {
            TempData["ErrorMessage"] = "Category not found.";
            return RedirectToAction(nameof(Categories));
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Category '{category.Slug}' deleted.";
        return RedirectToAction(nameof(Categories));
    }

    private Task<List<Language>> GetActiveLanguagesAsync()
    {
        return _dbContext.Languages
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.DisplayName)
            .ToListAsync();
    }

    private async Task<List<CategoryDto>> GetCategoryDtosAsync()
    {
        var defaultLang = await _dbContext.Languages.FirstOrDefaultAsync(l => l.IsDefault);
        var defaultLangId = defaultLang?.Id ?? 1;

        var categories = await _dbContext.Categories
            .Include(c => c.CategoryTranslations)
            .OrderBy(c => c.Slug)
            .ToListAsync();

        return categories.Select(c =>
        {
            var trans = c.CategoryTranslations.FirstOrDefault(t => t.LanguageId == defaultLangId)
                        ?? c.CategoryTranslations.FirstOrDefault();
            return new CategoryDto
            {
                Id = c.Id,
                Slug = c.Slug,
                Name = trans?.Name ?? c.Slug
            };
        }).ToList();
    }

    private async Task AttachCategoriesAsync(Movie movie, List<int> selectedIds)
    {
        if (selectedIds.Count > 0)
        {
            var categories = await _dbContext.Categories
                .Include(c => c.CategoryTranslations)
                .Where(c => selectedIds.Contains(c.Id))
                .ToListAsync();
            foreach (var cat in categories)
            {
                movie.Categories.Add(cat);
            }
        }
    }

    private static Dictionary<int, string> GetGenresByLanguage(IEnumerable<Category> categories)
    {
        return categories
            .SelectMany(c => c.CategoryTranslations)
            .GroupBy(t => t.LanguageId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(x => x.Name).Distinct()));
    }

    private static void ApplyTranslations(
        Movie movie,
        List<MovieTranslationFormItem> translations,
        Dictionary<int, string> genresByLanguage)
    {
        foreach (var t in translations)
        {
            if (string.IsNullOrWhiteSpace(t.Title))
            {
                continue;
            }

            var genres = genresByLanguage.TryGetValue(t.LanguageId, out var derivedGenres) ? derivedGenres : "";
            movie.MovieTranslations.Add(new MovieTranslation
            {
                LanguageId = t.LanguageId,
                Title = t.Title.Trim(),
                Description = t.Description?.Trim() ?? "",
                Genres = genres
            });
        }
    }

    private async Task<string> StageMasterVideoAsync(Movie movie, IFormFile? masterVideo)
    {
        if (masterVideo == null || masterVideo.Length == 0)
        {
            return string.Empty;
        }

        var stagingDir = _configuration["FFmpeg:StagingPath"] ?? "/var/www/cinema/media/staging";
        if (!OperatingSystem.IsLinux() || !Directory.Exists("/var/www"))
        {
            stagingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "media", "staging");
        }
        Directory.CreateDirectory(stagingDir);

        var stagingFilePath = Path.Combine(stagingDir, $"{movie.Id}.mp4");

        using (var fileStream = new FileStream(stagingFilePath, FileMode.Create))
        {
            await masterVideo.CopyToAsync(fileStream);
        }

        movie.MasterVideoPath = stagingFilePath;
        await _dbContext.SaveChangesAsync();

        await _transcodingQueue.QueueJobAsync(new TranscodingJob
        {
            MovieId = movie.Id,
            StagingFilePath = stagingFilePath,
            QueuedAt = DateTime.UtcNow
        });

        return "New master video staged and queued for automated FFmpeg transcoding (1080p, 720p, 480p, 360p + Poster).";
    }
}
