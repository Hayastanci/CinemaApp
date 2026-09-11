using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;

namespace CinemaApp.Web.Models;

public class HomeCatalogViewModel
{
    public List<MovieSummaryDto> Movies { get; set; } = new();
    public List<MovieSummaryDto> NewReleases { get; set; } = new();
    public List<CategoryDto> Categories { get; set; } = new();
    public List<LanguageDto> Languages { get; set; } = new();
    public string CurrentCulture { get; set; } = "en-US";
    public int? SelectedCategoryId { get; set; }
    public string? SearchQuery { get; set; }
    public string SelectedSort { get; set; } = "new";
    public List<int> Years { get; set; } = new();
    public int? SelectedYear { get; set; }
    public decimal? MinRating { get; set; }
    public MovieSummaryDto? FeaturedMovie { get; set; }
}

public class WatchViewModel
{
    public MovieDetailDto Movie { get; set; } = null!;
    public string CurrentQuality { get; set; } = "480p";
    public string StreamUrl { get; set; } = string.Empty;
    public bool UserIsSubscribed { get; set; }
    public bool RequiresSubscription { get; set; }
    public List<string> AllQualities { get; set; } = new();
    public string CurrentCulture { get; set; } = "en-US";
    public List<LanguageDto> Languages { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int TotalMovies { get; set; }
    public int TotalCategories { get; set; }
    public int TotalStreams { get; set; }
    public int ActiveLanguagesCount { get; set; }
    public int TotalSubscribers { get; set; }
    public int OpenTicketsCount { get; set; }
    public int UnansweredTicketsCount { get; set; }
    public List<MovieSummaryDto> RecentMovies { get; set; } = new();
    public List<LanguageDto> Languages { get; set; } = new();
}

public class MovieTranslationFormItem
{
    public int LanguageId { get; set; }
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Genres { get; set; } = string.Empty;
}

public class MovieCreateViewModel
{
    public int Id { get; set; }
    public int ReleaseYear { get; set; } = DateTime.UtcNow.Year;
    public int DurationMinutes { get; set; } = 120;
    public string? PosterUrl { get; set; }
    public string? BannerUrl { get; set; }
    public List<int> SelectedCategoryIds { get; set; } = new();
    public List<CategoryDto> AvailableCategories { get; set; } = new();

    public List<MovieTranslationFormItem> Translations { get; set; } = new();

    public bool IsEditing => Id > 0;
}

public class CategoryAdminItem
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public List<CategoryTranslationFormItem> Translations { get; set; } = new();
}

public class CategoryAdminViewModel
{
    public List<CategoryAdminItem> Categories { get; set; } = new();
    public List<LanguageDto> Languages { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class CategoryTranslationFormItem
{
    public int LanguageId { get; set; }
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
