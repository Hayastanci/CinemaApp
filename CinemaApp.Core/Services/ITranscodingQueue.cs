using CinemaApp.Core.DTOs;

namespace CinemaApp.Core.Services;

public interface ITranscodingQueue
{
    ValueTask QueueJobAsync(TranscodingJob job, CancellationToken cancellationToken = default);
    ValueTask<TranscodingJob> DequeueJobAsync(CancellationToken cancellationToken = default);
}
