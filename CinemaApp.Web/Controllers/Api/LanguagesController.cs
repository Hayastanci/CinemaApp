using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class LanguagesController : ControllerBase
{
    private readonly CinemaDbContext _dbContext;

    public LanguagesController(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetLanguages([FromQuery] bool activeOnly = true)
    {
        var query = _dbContext.Languages.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(l => l.IsActive);
        }

        var languages = await query.OrderByDescending(l => l.IsDefault).ThenBy(l => l.DisplayName).ToListAsync();
        var dtos = languages.Select(l => new LanguageDto
        {
            Id = l.Id,
            CultureCode = l.CultureCode,
            DisplayName = l.DisplayName,
            IsActive = l.IsActive,
            IsDefault = l.IsDefault
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    public async Task<IActionResult> AddLanguage([FromBody] AddLanguageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CultureCode) || string.IsNullOrWhiteSpace(dto.DisplayName))
        {
            return BadRequest(new { error = "ValidationError", message = "CultureCode and DisplayName are required." });
        }

        var normalizedCode = dto.CultureCode.Trim();
        var existing = await _dbContext.Languages.FirstOrDefaultAsync(l => l.CultureCode.ToLower() == normalizedCode.ToLower());
        if (existing != null)
        {
            existing.DisplayName = dto.DisplayName.Trim();
            existing.IsActive = dto.IsActive;
            if (dto.IsDefault) existing.IsDefault = true;
            await _dbContext.SaveChangesAsync();
            return Ok(new LanguageDto
            {
                Id = existing.Id,
                CultureCode = existing.CultureCode,
                DisplayName = existing.DisplayName,
                IsActive = existing.IsActive,
                IsDefault = existing.IsDefault
            });
        }

        var newLang = new Language
        {
            CultureCode = normalizedCode,
            DisplayName = dto.DisplayName.Trim(),
            IsActive = dto.IsActive,
            IsDefault = dto.IsDefault
        };

        _dbContext.Languages.Add(newLang);
        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLanguages), new { id = newLang.Id }, new LanguageDto
        {
            Id = newLang.Id,
            CultureCode = newLang.CultureCode,
            DisplayName = newLang.DisplayName,
            IsActive = newLang.IsActive,
            IsDefault = newLang.IsDefault
        });
    }
}
