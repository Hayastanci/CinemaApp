namespace CinemaApp.Core.DTOs;

public class AiGenerateEnrichmentRequest
{
    public required string SeedTitle { get; set; } = string.Empty;
    public string? HintGenre { get; set; }
    public string? SourceCulture { get; set; } = "en-US";
}

public class AiMovieEnrichmentResultDto
{
    public string SuggestedYear { get; set; } = DateTime.UtcNow.Year.ToString();
    public int SuggestedDurationMinutes { get; set; } = 115;
    public List<AiLanguageTranslationDto> Translations { get; set; } = new();
}

public class AiLanguageTranslationDto
{
    public string CultureCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Genres { get; set; } = string.Empty;
}
