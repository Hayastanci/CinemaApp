using CinemaApp.Core.DTOs;
using CinemaApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CinemaApp.Mobile.ViewModels;

public partial class MovieDetailViewModel : BaseViewModel
{
    private readonly CinemaApiClient _apiClient;

    [ObservableProperty]
    private MovieDetailDto? _movie;

    [ObservableProperty]
    private string _selectedQuality = "480p";

    public MovieDetailViewModel(CinemaApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task LoadMovieDetailsAsync(int movieId, string lang)
    {
        IsBusy = true;
        try
        {
            Movie = await _apiClient.GetMovieByIdAsync(movieId, lang);
            if (Movie != null)
            {
                Title = Movie.Title;
                SelectedQuality = Movie.Streams.FirstOrDefault(s => s.Quality == "480p")?.Quality 
                    ?? Movie.Streams.FirstOrDefault()?.Quality ?? "360p";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
