using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;

namespace CinemaApp.Core.Services;

public interface IAiContentService
{
    Task<AiMovieEnrichmentResultDto> GenerateMovieEnrichmentAsync(
        string seedTitle,
        string? hintGenre,
        IEnumerable<Language> targetLanguages,
        CancellationToken cancellationToken = default);
}
