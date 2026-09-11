using CinemaApp.Core.Data;
using CinemaApp.Core.Entities;
using CinemaApp.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.BackgroundServices;

public class VideoTranscodingWorker : BackgroundService
{
    private readonly ITranscodingQueue _queue;
    private readonly IFFmpegService _ffmpegService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VideoTranscodingWorker> _logger;

    public VideoTranscodingWorker(
        ITranscodingQueue queue,
        IFFmpegService ffmpegService,
        IServiceProvider serviceProvider,
        ILogger<VideoTranscodingWorker> logger)
    {
        _queue = queue;
        _ffmpegService = ffmpegService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VideoTranscodingWorker background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueJobAsync(stoppingToken);
                _logger.LogInformation("Dequeued transcoding job for Movie Id: {MovieId}, File: {Path}", job.MovieId, job.StagingFilePath);

                var pipelineResult = await _ffmpegService.ProcessMasterVideoAsync(job.MovieId, job.StagingFilePath, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

                var movie = await dbContext.Movies
                    .Include(m => m.MediaStreams)
                    .FirstOrDefaultAsync(m => m.Id == job.MovieId, stoppingToken);

                if (movie != null)
                {
                    // Relative web url for poster (served via /media -> mediaRoot)
                    movie.PosterUrl = $"/media/movies/{movie.Id}/poster.jpg";

                    foreach (var res in pipelineResult.Streams)
                    {
                        var existingStream = movie.MediaStreams
                            .FirstOrDefault(s => s.Quality.Equals(res.Quality, StringComparison.OrdinalIgnoreCase));

                        if (existingStream != null)
                        {
                            existingStream.FilePath = res.FilePath;
                            existingStream.FileSize = res.FileSize;
                            existingStream.IsReady = res.Success;
                        }
                        else
                        {
                            movie.MediaStreams.Add(new MediaStream
                            {
                                MovieId = movie.Id,
                                Quality = res.Quality,
                                FilePath = res.FilePath,
                                FileSize = res.FileSize,
                                IsReady = res.Success,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    if (pipelineResult.Success)
                    {
                        movie.MasterVideoPath = null;
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Successfully saved transcoded media streams into database for Movie Id: {MovieId}", movie.Id);
                }

                // Delete the staged master video to free disk space (applies in both Docker production & local dev)
                if (pipelineResult.Success && !string.IsNullOrWhiteSpace(job.StagingFilePath))
                {
                    try
                    {
                        if (File.Exists(job.StagingFilePath))
                        {
                            var fileSizeMb = (new FileInfo(job.StagingFilePath).Length / (1024.0 * 1024.0)).ToString("0.0");
                            File.Delete(job.StagingFilePath);
                            _logger.LogInformation("Deleted staged master video after successful transcoding: {Path} ({SizeMb} MB reclaimed)", job.StagingFilePath, fileSizeMb);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete staged master video file: {Path}", job.StagingFilePath);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in VideoTranscodingWorker.");
            }
        }

        _logger.LogInformation("VideoTranscodingWorker background service stopped.");
    }
}
