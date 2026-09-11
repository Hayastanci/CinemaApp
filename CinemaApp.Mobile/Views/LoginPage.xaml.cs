using CinemaApp.Mobile.ViewModels;

namespace CinemaApp.Mobile.Views;

public partial class LoginPage : ContentPage
{
    private bool _passwordVisible;

    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshAuthState();
        _viewModel.StartAutoRefresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopAutoRefresh();
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;
        PasswordEntry.IsPassword = !_passwordVisible;
        TogglePasswordButton.Text = _passwordVisible ? "\U0001F648" : "\U0001F441";
    }
}
