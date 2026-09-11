using CinemaApp.Core.Entities;
using CinemaApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CinemaApp.Mobile.ViewModels;

public partial class PlayerViewModel : BaseViewModel
{
    private readonly CinemaApiClient _apiClient;
    private readonly AuthService _authService;

    [ObservableProperty]
    private int _movieId;

    [ObservableProperty]
    private string _movieTitle = string.Empty;

    [ObservableProperty]
    private string _streamUrl = string.Empty;

    [ObservableProperty]
    private string _currentQuality = "480p";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRestricted))]
    private bool _isRestricted = false;

    public bool IsNotRestricted => !IsRestricted;

    [ObservableProperty]
    private string _restrictionMessage = string.Empty;

    public List<string> Qualities { get; } = StreamQualityConstants.All.ToList();

    public PlayerViewModel(CinemaApiClient apiClient, AuthService authService)
    {
        _apiClient = apiClient;
        _authService = authService;
    }

    public void Setup(int movieId, string title, string initialQuality = "480p")
    {
        MovieId = movieId;
        MovieTitle = title;
        Title = $"Playing: {title}";
        ChangeQuality(initialQuality);
    }

    [RelayCommand]
    public void ChangeQuality(string quality)
    {
        CurrentQuality = quality;
        var isFree = StreamQualityConstants.IsFreeQuality(quality);

        if (!isFree && !_authService.IsSubscribed)
        {
            IsRestricted = true;
            RestrictionMessage = $"Streaming in {quality.ToUpperInvariant()} requires an active Cinema Pro subscription. Free users are limited to 360p and 480p.";
            StreamUrl = string.Empty;
        }
        else
        {
            IsRestricted = false;
            StreamUrl = _apiClient.GetStreamUrl(MovieId, quality);
        }
    }
}
