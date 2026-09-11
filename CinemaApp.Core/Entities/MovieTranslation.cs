namespace CinemaApp.Core.Entities;

public class MovieTranslation
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public int LanguageId { get; set; }
    public Language Language { get; set; } = null!;

    public required string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Genres { get; set; } = string.Empty; // Comma separated or tags, e.g. "Action, Sci-Fi"
}
