using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly CinemaDbContext _dbContext;

    public CategoriesController(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories([FromQuery] string? lang)
    {
        var culture = !string.IsNullOrWhiteSpace(lang) ? lang.Trim() : "en-US";
        var language = await _dbContext.Languages.FirstOrDefaultAsync(l => l.CultureCode.ToLower() == culture.ToLower() && l.IsActive)
                       ?? await _dbContext.Languages.FirstOrDefaultAsync(l => l.IsDefault)
                       ?? await _dbContext.Languages.FirstOrDefaultAsync();

        var langId = language?.Id ?? 1;

        var categories = await _dbContext.Categories
            .Include(c => c.CategoryTranslations)
            .AsNoTracking()
            .ToListAsync();

        var result = categories.Select(c =>
        {
            var trans = c.CategoryTranslations.FirstOrDefault(t => t.LanguageId == langId)
                        ?? c.CategoryTranslations.FirstOrDefault();
            return new CategoryDto
            {
                Id = c.Id,
                Slug = c.Slug,
                Name = trans?.Name ?? c.Slug,
                Description = trans?.Description ?? ""
            };
        }).ToList();

        return Ok(result);
    }
}
