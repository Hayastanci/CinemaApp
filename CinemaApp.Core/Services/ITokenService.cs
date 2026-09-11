using CinemaApp.Core.Entities;

namespace CinemaApp.Core.Services;

public interface ITokenService
{
    string GenerateToken(User user);
    int? ValidateTokenAndGetUserId(string token);
}
