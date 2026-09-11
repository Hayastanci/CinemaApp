using CinemaApp.Core.Entities;

namespace CinemaApp.Core.DTOs;

public class LoginRequestDto
{
    public required string Email { get; set; } = string.Empty;
    public required string Password { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    public required string Email { get; set; } = string.Empty;
    public required string Password { get; set; } = string.Empty;
    public string? ConfirmPassword { get; set; }
}

public class AuthResponseDto
{
    public required string Token { get; set; } = string.Empty;
    public required UserProfileDto User { get; set; } = null!;
}

public class UserProfileDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsSubscribed { get; set; }
}
