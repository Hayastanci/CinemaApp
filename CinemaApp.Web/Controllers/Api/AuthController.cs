using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using CinemaApp.Core.Security;
using CinemaApp.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CinemaDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public AuthController(CinemaDbContext dbContext, ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { error = "ValidationError", message = "Email and Password are required." });
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var existing = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (existing)
        {
            return Conflict(new { error = "DuplicateEmail", message = "An account with this email already exists." });
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.HashPassword(dto.Password),
            Role = UserRole.Registered,
            IsSubscribed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);
        return Ok(new AuthResponseDto
        {
            Token = token,
            User = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                IsSubscribed = user.IsSubscribed
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null || !PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "InvalidCredentials", message = "Invalid email or password." });
        }

        var token = _tokenService.GenerateToken(user);
        return Ok(new AuthResponseDto
        {
            Token = token,
            User = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                IsSubscribed = user.IsSubscribed
            }
        });
    }

    [Authorize(AuthenticationSchemes = $"{Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme},{Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme}")]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
            IsSubscribed = user.IsSubscribed
        });
    }
}
