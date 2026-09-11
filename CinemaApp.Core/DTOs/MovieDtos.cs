namespace CinemaApp.Core.DTOs;

public class MovieSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Genres { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public int DurationMinutes { get; set; }
    public string? PosterUrl { get; set; }
    public string? BannerUrl { get; set; }
    public List<string> AvailableQualities { get; set; } = new();
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
}

public class MovieRatingDto
{
    public int MovieId { get; set; }
    public int Score { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public bool Updated { get; set; }
}

public class MovieRatingRequestDto
{
    public string VisitorId { get; set; } = string.Empty;
    public int Score { get; set; }
}

public class MovieDetailDto : MovieSummaryDto
{
    public List<MediaStreamDto> Streams { get; set; } = new();
    public List<CategoryDto> Categories { get; set; } = new();
}

public class MediaStreamDto
{
    public int Id { get; set; }
    public string Quality { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsReady { get; set; }
    public string StreamUrl { get; set; } = string.Empty;
    public bool RequiresSubscription { get; set; }
}

public class MovieCreateEditDto
{
    public int Id { get; set; }
    public int ReleaseYear { get; set; } = DateTime.UtcNow.Year;
    public int DurationMinutes { get; set; } = 120;
    public string? PosterUrl { get; set; }
    public string? BannerUrl { get; set; }
    public List<int> SelectedCategoryIds { get; set; } = new();

    // Keyed by CultureCode or LanguageId
    public List<MovieTranslationEditDto> Translations { get; set; } = new();
}

public class MovieTranslationEditDto
{
    public int LanguageId { get; set; }
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Genres { get; set; } = string.Empty;
}
