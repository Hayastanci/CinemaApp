using CinemaApp.Mobile.Services;
using CinemaApp.Mobile.Views;

namespace CinemaApp.Mobile;

public partial class AppShell : Shell
{
    private readonly AuthService _authService;

    public AppShell(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;

        Routing.RegisterRoute("movieDetails", typeof(MovieDetailPage));
        Routing.RegisterRoute("player", typeof(PlayerPage));
        Routing.RegisterRoute("login", typeof(LoginPage));

        // Update the account tab title to reflect current login state
        UpdateAccountTabTitle();
    }

    /// <summary>
    /// Call this after login/logout to refresh the account tab label.
    /// </summary>
    public void UpdateAccountTabTitle()
    {
        var title = _authService.IsAuthenticated
            ? (!string.IsNullOrEmpty(_authService.Email) ? _authService.Email.Split('@')[0] : "Account")
            : "Sign In";

        if (AccountTab != null)
        {
            AccountTab.Title = title;
        }
        if (AccountShellContent != null)
        {
            AccountShellContent.Title = title;
        }
        if (Items.Count > 1)
        {
            Items[1].Title = title;
        }
    }
}
