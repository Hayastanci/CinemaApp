namespace CinemaApp.Core.Entities;

public class MovieRating
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string VisitorId { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Movie Movie { get; set; } = null!;
}