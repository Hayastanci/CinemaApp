namespace CinemaApp.Core.Entities;

public class MediaStream
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public required string Quality { get; set; } = string.Empty; // "360p", "480p", "720p", "1080p"
    public required string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsReady { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
