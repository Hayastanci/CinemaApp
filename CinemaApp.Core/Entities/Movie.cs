namespace CinemaApp.Core.Entities;

public class Movie
{
    public int Id { get; set; }
    public int ReleaseYear { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? PosterUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? MasterVideoPath { get; set; }

    public ICollection<MovieTranslation> MovieTranslations { get; set; } = new List<MovieTranslation>();
    public ICollection<MediaStream> MediaStreams { get; set; } = new List<MediaStream>();
    public ICollection<MovieRating> MovieRatings { get; set; } = new List<MovieRating>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
