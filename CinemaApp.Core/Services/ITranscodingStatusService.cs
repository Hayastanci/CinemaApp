namespace CinemaApp.Core.Services;

public class TranscodingProgressInfo
{
    public int MovieId { get; set; }
    public string Status { get; set; } = "Queued"; // Queued, Processing, Completed, Failed
    public int Percent { get; set; } = 0;
    public string CurrentStep { get; set; } = "Queued for conversion...";
    public List<string> CompletedQualities { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public interface ITranscodingStatusService
{
    void SetQueued(int movieId);
    void UpdateProgress(int movieId, int percent, string currentStep, string? completedQuality = null);
    void SetCompleted(int movieId, IEnumerable<string> completedQualities);
    void SetFailed(int movieId, string error);
    TranscodingProgressInfo? GetProgress(int movieId);
    IReadOnlyDictionary<int, TranscodingProgressInfo> GetAll();
}
