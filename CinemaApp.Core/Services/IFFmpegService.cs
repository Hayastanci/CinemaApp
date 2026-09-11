namespace CinemaApp.Core.Services;

public record TranscodingResult(
    string Quality,
    string FilePath,
    long FileSize,
    bool Success,
    string? ErrorMessage = null);

public record TranscodingPipelineResult(
    int MovieId,
    string PosterPath,
    List<TranscodingResult> Streams,
    bool Success,
    string? ErrorMessage = null);

public interface IFFmpegService
{
    Task<TranscodingPipelineResult> ProcessMasterVideoAsync(
        int movieId,
        string stagingSourcePath,
        CancellationToken cancellationToken = default);

    Task<bool> ExtractThumbnailAsync(
        string sourcePath,
        string outputImagePath,
        string timestamp = "00:00:10",
        CancellationToken cancellationToken = default);

    Task<TranscodingResult> TranscodeQualityAsync(
        string sourcePath,
        string outputPath,
        string quality,
        int width,
        int height,
        int bitrateK,
        CancellationToken cancellationToken = default);
}
