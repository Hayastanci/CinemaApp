using System.Collections.ObjectModel;
using CinemaApp.Core.DTOs;
using CinemaApp.Mobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CinemaApp.Mobile.ViewModels;

public partial class CatalogViewModel : BaseViewModel
{
    private readonly CinemaApiClient _apiClient;

    public ObservableCollection<MovieSummaryDto> Movies { get; } = new();
    public ObservableCollection<CategoryDto> Categories { get; } = new();
    public ObservableCollection<LanguageDto> Languages { get; } = new();

    [ObservableProperty]
    private LanguageDto? _selectedLanguage;

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _allFilterButtonText = "All";

    [ObservableProperty]
    private string _removeFilterButtonText = "✕ Clear Filter";

    public bool HasActiveFilter => SelectedCategory != null || !string.IsNullOrWhiteSpace(SearchQuery);

    /// <summary>True once the catalog has loaded at least once (drives the empty-state hint).</summary>
    [ObservableProperty]
    private bool _hasLoaded;

    public CatalogViewModel(CinemaApiClient apiClient)
    {
        _apiClient = apiClient;
        Movies.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMovies));
            OnPropertyChanged(nameof(ShowEmptyState));
        };
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsBusy))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        };
        Title = "Bartigran Movie App";
        UpdateFilterTexts();
    }

    public void UpdateFilterTexts()
    {
        var culture = SelectedLanguage?.CultureCode?.ToLowerInvariant() ?? "en-us";
        if (culture.StartsWith("hy"))
        {
            AllFilterButtonText = "Բոլորը";
            RemoveFilterButtonText = "✕ Մաքրել ֆիլտրը";
        }
        else if (culture.StartsWith("ru"))
        {
            AllFilterButtonText = "Все";
            RemoveFilterButtonText = "✕ Сбросить фильтр";
        }
        else
        {
            AllFilterButtonText = "All";
            RemoveFilterButtonText = "✕ Clear Filter";
        }
        OnPropertyChanged(nameof(HasActiveFilter));
    }

    public string ActiveFilterLabel
    {
        get
        {
            if (SelectedCategory != null && !string.IsNullOrWhiteSpace(SearchQuery))
                return $"{SelectedCategory.Name} • \"{SearchQuery}\"";
            if (SelectedCategory != null)
                return SelectedCategory.Name;
            if (!string.IsNullOrWhiteSpace(SearchQuery))
                return $"\"{SearchQuery}\"";
            return string.Empty;
        }
    }

    public async Task SetLanguageByCodeAsync(string cultureCode)
    {
        var target = Languages.FirstOrDefault(l => l.CultureCode.Equals(cultureCode, StringComparison.OrdinalIgnoreCase));
        if (target != null)
        {
            SelectedLanguage = target;
        }
        else
        {
            SelectedLanguage = new LanguageDto { CultureCode = cultureCode, DisplayName = cultureCode };
            await LoadCategoriesAndMoviesAsync();
        }
        UpdateFilterTexts();
    }

    partial void OnSelectedCategoryChanged(CategoryDto? value)
    {
        UpdateFilterTexts();
        OnPropertyChanged(nameof(ActiveFilterLabel));
    }

    partial void OnSearchQueryChanged(string value)
    {
        UpdateFilterTexts();
        OnPropertyChanged(nameof(ActiveFilterLabel));
    }

    public bool HasMovies => Movies.Count > 0;

    /// <summary>True when the load finished but there is nothing to show.</summary>
    public bool ShowEmptyState => HasLoaded && !HasMovies && !IsBusy;

    partial void OnHasLoadedChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

    [RelayCommand]
    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _apiClient.EnsureEndpointAsync();
            var langs = await _apiClient.GetLanguagesAsync();
            Languages.Clear();
            foreach (var l in langs) Languages.Add(l);

            SelectedLanguage = Languages.FirstOrDefault(l => l.IsDefault) ?? Languages.FirstOrDefault();

            await LoadCategoriesAndMoviesAsync();
            HasLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedLanguageChanged(LanguageDto? value)
    {
        UpdateFilterTexts();
        if (value != null && HasLoaded)
        {
            _ = LoadCategoriesAndMoviesAsync();
        }
    }

    [RelayCommand]
    public async Task LoadCategoriesAndMoviesAsync()
    {
        var langCode = SelectedLanguage?.CultureCode ?? "en-US";
        IsBusy = true;
        try
        {
            var cats = await _apiClient.GetCategoriesAsync(langCode);
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            var movies = await _apiClient.GetMoviesAsync(langCode, SelectedCategory?.Id, SearchQuery);
            Movies.Clear();
            foreach (var m in movies) Movies.Add(m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SelectCategoryAsync(CategoryDto? category)
    {
        // null = clear the filter and show all movies
        SelectedCategory = category;
        await LoadCategoriesAndMoviesAsync();
    }

    [RelayCommand]
    public async Task ClearAllFiltersAsync()
    {
        SearchQuery = string.Empty;
        SelectedCategory = null;
        await LoadCategoriesAndMoviesAsync();
    }
}
