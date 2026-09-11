using CinemaApp.Mobile.ViewModels;

namespace CinemaApp.Mobile.Views;

public partial class PlayerPage : ContentPage, IQueryAttributable
{
    private readonly PlayerViewModel _viewModel;

    public PlayerPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && int.TryParse(idObj.ToString(), out var movieId))
        {
            var title = query.TryGetValue("title", out var titleObj) ? titleObj.ToString()! : "Movie";
            var quality = query.TryGetValue("quality", out var qObj) ? qObj.ToString()! : "480p";
            _viewModel.Setup(movieId, title, quality);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try
        {
            mediaPlayer.Stop();
        }
        catch
        {
        }
    }
}
