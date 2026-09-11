using CinemaApp.Mobile.ViewModels;

namespace CinemaApp.Mobile.Views;

public partial class MovieDetailPage : ContentPage, IQueryAttributable
{
    private readonly MovieDetailViewModel _viewModel;

    public MovieDetailPage(MovieDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && int.TryParse(idObj.ToString(), out var movieId))
        {
            var lang = query.TryGetValue("lang", out var langObj) ? langObj.ToString()! : "en-US";
            await _viewModel.LoadMovieDetailsAsync(movieId, lang);
        }
    }

    private void OnQualityClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string quality)
        {
            _viewModel.SelectedQuality = quality;
        }
    }

    private async void OnPlayClicked(object? sender, EventArgs e)
    {
        if (_viewModel.Movie != null)
        {
            await Shell.Current.GoToAsync($"player?id={_viewModel.Movie.Id}&title={Uri.EscapeDataString(_viewModel.Movie.Title)}&quality={_viewModel.SelectedQuality}");
        }
    }
}
