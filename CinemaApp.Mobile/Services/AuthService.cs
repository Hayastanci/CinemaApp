using CinemaApp.Core.Entities;
using Microsoft.Maui.Storage;

namespace CinemaApp.Mobile.Services;

public class AuthService
{
    private string? _token;
    private bool _isSubscribed;
    private string? _email;
    private UserRole _role;
    private int _userId;

    public AuthService()
    {
        _token = Preferences.Default.Get<string?>("auth.token", null);
        _email = Preferences.Default.Get<string?>("auth.email", null);
        _isSubscribed = Preferences.Default.Get<bool>("auth.subscribed", false);
        var roleStr = Preferences.Default.Get<string>("auth.role", "Registered");
        _role = Enum.TryParse<UserRole>(roleStr, out var r) ? r : UserRole.Registered;
        _userId = Preferences.Default.Get<int>("auth.userid", 0);
    }

    public string? Token => _token;
    public bool IsSubscribed => _isSubscribed;
    public string? Email => _email;
    public UserRole Role => _role;
    public int UserId => _userId;
    public bool IsAdmin => _role == UserRole.Admin;
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    public void SetAuth(string token, string email, bool isSubscribed, UserRole role = UserRole.Registered, int userId = 0)
    {
        _token = token;
        _email = email;
        _isSubscribed = isSubscribed;
        _role = role;
        _userId = userId;

        Preferences.Default.Set("auth.token", token);
        Preferences.Default.Set("auth.email", email);
        Preferences.Default.Set("auth.subscribed", isSubscribed);
        Preferences.Default.Set("auth.role", role.ToString());
        Preferences.Default.Set("auth.userid", userId);
    }

    public void Logout()
    {
        _token = null;
        _email = null;
        _isSubscribed = false;
        _role = UserRole.Registered;
        _userId = 0;

        Preferences.Default.Remove("auth.token");
        Preferences.Default.Remove("auth.email");
        Preferences.Default.Remove("auth.subscribed");
        Preferences.Default.Remove("auth.role");
        Preferences.Default.Remove("auth.userid");
    }
}