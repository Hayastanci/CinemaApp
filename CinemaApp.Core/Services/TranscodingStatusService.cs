using System.Collections.Concurrent;

namespace CinemaApp.Core.Services;

public class TranscodingStatusService : ITranscodingStatusService
{
    private readonly ConcurrentDictionary<int, TranscodingProgressInfo> _statuses = new();

    public void SetQueued(int movieId)
    {
        _statuses[movieId] = new TranscodingProgressInfo
        {
            MovieId = movieId,
            Status = "Queued",
            Percent = 5,
            CurrentStep = "In background conversion queue...",
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProgress(int movieId, int percent, string currentStep, string? completedQuality = null)
    {
        _statuses.AddOrUpdate(movieId,
            id =>
            {
                var info = new TranscodingProgressInfo
                {
                    MovieId = id,
                    Status = "Processing",
                    Percent = percent,
                    CurrentStep = currentStep,
                    UpdatedAt = DateTime.UtcNow
                };
                if (!string.IsNullOrEmpty(completedQuality))
                {
                    info.CompletedQualities.Add(completedQuality);
                }
                return info;
            },
            (id, existing) =>
            {
                existing.Status = "Processing";
                existing.Percent = percent;
                existing.CurrentStep = currentStep;
                existing.UpdatedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(completedQuality) && !existing.CompletedQualities.Contains(completedQuality))
                {
                    existing.CompletedQualities.Add(completedQuality);
                }
                return existing;
            });
    }

    public void SetCompleted(int movieId, IEnumerable<string> completedQualities)
    {
        _statuses.AddOrUpdate(movieId,
            id => new TranscodingProgressInfo
            {
                MovieId = id,
                Status = "Completed",
                Percent = 100,
                CurrentStep = "All video streams transcoded successfully.",
                CompletedQualities = completedQualities.ToList(),
                UpdatedAt = DateTime.UtcNow
            },
            (id, existing) =>
            {
                existing.Status = "Completed";
                existing.Percent = 100;
                existing.CurrentStep = "All video streams transcoded successfully.";
                existing.CompletedQualities = completedQualities.ToList();
                existing.UpdatedAt = DateTime.UtcNow;
                return existing;
            });
    }

    public void SetFailed(int movieId, string error)
    {
        _statuses.AddOrUpdate(movieId,
            id => new TranscodingProgressInfo
            {
                MovieId = id,
                Status = "Failed",
                Percent = 100,
                CurrentStep = "Conversion failed.",
                ErrorMessage = error,
                UpdatedAt = DateTime.UtcNow
            },
            (id, existing) =>
            {
                existing.Status = "Failed";
                existing.CurrentStep = "Conversion failed.";
                existing.ErrorMessage = error;
                existing.UpdatedAt = DateTime.UtcNow;
                return existing;
            });
    }

    public TranscodingProgressInfo? GetProgress(int movieId)
    {
        _statuses.TryGetValue(movieId, out var info);
        return info;
    }

    public IReadOnlyDictionary<int, TranscodingProgressInfo> GetAll()
    {
        return _statuses;
    }
}
