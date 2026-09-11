using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class AiController : ControllerBase
{
    private readonly IAiContentService _aiContentService;
    private readonly CinemaDbContext _dbContext;

    public AiController(IAiContentService aiContentService, CinemaDbContext dbContext)
    {
        _aiContentService = aiContentService;
        _dbContext = dbContext;
    }

    [HttpPost("generate-enrichment")]
    public async Task<IActionResult> GenerateEnrichment([FromBody] AiGenerateEnrichmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SeedTitle))
        {
            return BadRequest(new { error = "ValidationError", message = "SeedTitle is required to generate AI enrichment." });
        }

        var activeLanguages = await _dbContext.Languages
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.IsDefault)
            .ToListAsync();

        var result = await _aiContentService.GenerateMovieEnrichmentAsync(
            request.SeedTitle.Trim(),
            request.HintGenre?.Trim(),
            activeLanguages,
            HttpContext.RequestAborted);

        return Ok(result);
    }
}
