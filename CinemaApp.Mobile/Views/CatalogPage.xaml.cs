using CinemaApp.Core.DTOs;
using CinemaApp.Mobile.ViewModels;

namespace CinemaApp.Mobile.Views;

public partial class CatalogPage : ContentPage
{
    private readonly CatalogViewModel _viewModel;
    private Button? _activeButton;   // tracks which category button is currently highlighted

    public CatalogPage(CatalogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Movies.Count == 0)
        {
            await _viewModel.InitializeAsync();
        }
        var code = _viewModel.SelectedLanguage?.CultureCode ?? "en-US";
        UpdateLanguageButtonStyles(code);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Category filter: tap a category to filter; tap again (or "All") to clear
    // ──────────────────────────────────────────────────────────────────────────

    private async void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not CategoryDto cat) return;

        // Toggle off if the same category is tapped again
        if (_viewModel.SelectedCategory?.Id == cat.Id)
        {
            await ClearFilterAsync();
            return;
        }

        // Set new active filter
        SetActiveButton(btn);
        await _viewModel.SelectCategoryAsync(cat);
    }

    private async void OnClearCategoryClicked(object? sender, EventArgs e)
    {
        await ClearFilterAsync();
    }

    private async Task ClearFilterAsync()
    {
        SetActiveButton(null);
        await _viewModel.ClearAllFiltersAsync();
    }

    /// <summary>
    /// Highlights the active category button and resets all others.
    /// Passing <c>null</c> resets everything (All mode).
    /// </summary>
    private void SetActiveButton(Button? target)
    {
        // Reset previously active button
        if (_activeButton != null)
        {
            _activeButton.BackgroundColor = Color.FromArgb("#141A29");
            _activeButton.TextColor       = Color.FromArgb("#94A3B8");
            _activeButton.FontAttributes  = FontAttributes.None;
        }

        // Highlight the new target
        if (target != null)
        {
            target.BackgroundColor = Color.FromArgb("#06B6D4");
            target.TextColor       = Color.FromArgb("#06080D");
            target.FontAttributes  = FontAttributes.Bold;
        }
        _activeButton = target;

        // The "All" button is active (cyan) when no category is selected
        AllCategoriesButton.BackgroundColor = target == null
            ? Color.FromArgb("#06B6D4") : Color.FromArgb("#141A29");
        AllCategoriesButton.TextColor = target == null
            ? Color.FromArgb("#06080D") : Color.FromArgb("#94A3B8");
        AllCategoriesButton.FontAttributes = target == null
            ? FontAttributes.Bold : FontAttributes.None;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Movie tap → navigate to detail
    // ──────────────────────────────────────────────────────────────────────────

    private async void OnMovieTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is MovieSummaryDto movie)
        {
            var langCode = _viewModel.SelectedLanguage?.CultureCode ?? "en-US";
            await Shell.Current.GoToAsync($"movieDetails?id={movie.Id}&lang={langCode}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Language Pill Switcher with Country Flags
    // ──────────────────────────────────────────────────────────────────────────

    private async void OnLangEnClicked(object? sender, EventArgs e)
    {
        UpdateLanguageButtonStyles("en-US");
        await _viewModel.SetLanguageByCodeAsync("en-US");
    }

    private async void OnLangHyClicked(object? sender, EventArgs e)
    {
        UpdateLanguageButtonStyles("hy-AM");
        await _viewModel.SetLanguageByCodeAsync("hy-AM");
    }

    private async void OnLangRuClicked(object? sender, EventArgs e)
    {
        UpdateLanguageButtonStyles("ru-RU");
        await _viewModel.SetLanguageByCodeAsync("ru-RU");
    }

    private void UpdateLanguageButtonStyles(string activeCulture)
    {
        var isEn = activeCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var isHy = activeCulture.StartsWith("hy", StringComparison.OrdinalIgnoreCase);
        var isRu = activeCulture.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        SetLangBtnState(LangEnBtn, isEn);
        SetLangBtnState(LangHyBtn, isHy);
        SetLangBtnState(LangRuBtn, isRu);
    }

    private void SetLangBtnState(Button btn, bool isActive)
    {
        btn.BackgroundColor = isActive ? Color.FromArgb("#06B6D4") : Color.FromArgb("#141A29");
        btn.TextColor = isActive ? Color.FromArgb("#06080D") : Color.FromArgb("#94A3B8");
    }
}
