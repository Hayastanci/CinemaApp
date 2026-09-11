namespace CinemaApp.Core.DTOs;

public class TranscodingJob
{
    public int MovieId { get; set; }
    public required string StagingFilePath { get; set; } = string.Empty;
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}
