using System.Collections.ObjectModel;
using System.Net.Http;
using CinemaApp.Core.DTOs;
using CinemaApp.Mobile.Services;
using CinemaApp.Mobile.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CinemaApp.Mobile.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly CinemaApiClient _apiClient;
    private readonly AuthService _authService;
    private readonly MobileNotificationService _notificationService;

    [ObservableProperty]
    private string _email = "subscriber@cinema.local";

    [ObservableProperty]
    private string _password = "UserPass123!";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotAuthenticated))]
    private bool _isAuthenticated;

    public bool IsNotAuthenticated => !IsAuthenticated;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _userEmail = string.Empty;

    [ObservableProperty]
    private bool _isSubscribed;

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private string _subscriptionTier = "Free Standard Tier";

    // ─────────────────────────────────────────────────────────────────────────────
    // Support & Notification States
    // ─────────────────────────────────────────────────────────────────────────────

    private List<SupportTicketSummaryDto> _allTickets = new();

    [ObservableProperty]
    private ObservableCollection<SupportTicketSummaryDto> _filteredTickets = new();

    [ObservableProperty]
    private ObservableCollection<NotificationDto> _recentNotifications = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifs))]
    private int _unreadNotifCount;

    public bool HasUnreadNotifs => UnreadNotifCount > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnansweredTickets))]
    private int _unansweredCount;

    public bool HasUnansweredTickets => UnansweredCount > 0;

    [ObservableProperty]
    private bool _isAdminHelpdeskTab;

    [ObservableProperty]
    private bool _isNotificationsVisible;

    // Ticket Summary Counters
    [ObservableProperty]
    private int _openTicketsCount;

    [ObservableProperty]
    private int _totalTicketsCount;

    [ObservableProperty]
    private int _openStatusCount;

    [ObservableProperty]
    private int _inProgressStatusCount;

    [ObservableProperty]
    private int _resolvedStatusCount;

    [ObservableProperty]
    private int _closedStatusCount;

    [ObservableProperty]
    private string _selectedStatus = "All";

    [ObservableProperty]
    private string _currentSortMode = "Status"; // "Status", "Newest", "Oldest"

    [ObservableProperty]
    private string _currentSortLabel = "Status (Active First)";

    private readonly HashSet<int> _notifiedNotificationIds = new();
    private readonly Dictionary<int, string> _lastKnownStatuses = new();
    private IDispatcherTimer? _autoRefreshTimer;

    public LoginViewModel(CinemaApiClient apiClient, AuthService authService, MobileNotificationService notificationService)
    {
        _apiClient = apiClient;
        _authService = authService;
        _notificationService = notificationService;
        RefreshAuthState();
    }

    public void StartAutoRefresh()
    {
        if (_autoRefreshTimer != null && _autoRefreshTimer.IsRunning) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _autoRefreshTimer = Application.Current?.Dispatcher?.CreateTimer();
            if (_autoRefreshTimer != null)
            {
                _autoRefreshTimer.Interval = TimeSpan.FromSeconds(3);
                _autoRefreshTimer.Tick += async (_, _) =>
                {
                    if (IsAuthenticated)
                    {
                        await LoadTicketsAsync();
                    }
                };
                _autoRefreshTimer.Start();
                System.Diagnostics.Debug.WriteLine("[LoginViewModel] Started auto-refresh polling (every 3s)");
            }
        });
    }

    public void StopAutoRefresh()
    {
        if (_autoRefreshTimer != null)
        {
            _autoRefreshTimer.Stop();
            _autoRefreshTimer = null;
            System.Diagnostics.Debug.WriteLine("[LoginViewModel] Stopped auto-refresh polling");
        }
    }

    public void RefreshAuthState()
    {
        IsAuthenticated = _authService.IsAuthenticated;
        UserEmail = _authService.Email ?? string.Empty;
        UserName = !string.IsNullOrEmpty(_authService.Email)
            ? _authService.Email.Split('@')[0]
            : "User";
        IsSubscribed = _authService.IsSubscribed;
        IsAdmin = _authService.IsAdmin;
        SubscriptionTier = _authService.IsSubscribed ? "⭐ Cinema Pro VIP (1080p FHD)" : "Free Standard Plan (480p)";
        Title = IsAuthenticated ? UserName : "Sign In";
        ErrorMessage = string.Empty;
        IsNotificationsVisible = false;

        _notificationService.RequestNotificationPermission();

        if (IsAuthenticated)
        {
            _ = LoadTicketsAsync();
            StartAutoRefresh();
        }
        else
        {
            StopAutoRefresh();
        }
    }

    [RelayCommand]
    public async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _apiClient.EnsureEndpointAsync();

            bool success;
            try
            {
                success = await _apiClient.LoginAsync(Email.Trim(), Password);
            }
            catch (HttpRequestException)
            {
                ErrorMessage = "Cannot reach the server. Make sure the CinemaApp web server is running and this device can reach it.";
                return;
            }

            if (success)
            {
                RefreshAuthState();

                // Update the Sign In tab title to show the logged-in user name
                if (Shell.Current is AppShell appShell)
                {
                    appShell.UpdateAccountTabTitle();
                }

                // Navigate to the catalog tab
                await Shell.Current.GoToAsync("//catalog");
            }
            else
            {
                ErrorMessage = "Invalid credentials. Please try again.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task LoadTicketsAsync()
    {
        if (!IsAuthenticated) return;

        try
        {
            var list = await _apiClient.GetSupportTicketsAsync(IsAdminHelpdeskTab);
            if (list != null)
            {
                // Detect status changes on existing tickets to show push notification
                if (_lastKnownStatuses.Count > 0)
                {
                    foreach (var t in list)
                    {
                        if (_lastKnownStatuses.TryGetValue(t.Id, out var oldStatus))
                        {
                            if (!string.Equals(oldStatus, t.Status, StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"[Push] Ticket #{t.Id} status update: '{oldStatus}' -> '{t.Status}'");
                                _notificationService.ShowTicketNotification(
                                    t.Id,
                                    $"Ticket #{t.Id} Status: {t.Status}",
                                    $"Ticket status was changed from '{oldStatus}' to '{t.Status}'");
                            }
                        }
                    }
                }

                // Update last known status cache
                foreach (var t in list)
                {
                    _lastKnownStatuses[t.Id] = t.Status;
                }

                _allTickets = list;

                // Calculate counters
                TotalTicketsCount = _allTickets.Count;
                OpenStatusCount = _allTickets.Count(t => t.Status.Equals("Open", StringComparison.OrdinalIgnoreCase));
                InProgressStatusCount = _allTickets.Count(t => t.Status.Equals("InProgress", StringComparison.OrdinalIgnoreCase));
                ResolvedStatusCount = _allTickets.Count(t => t.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase));
                ClosedStatusCount = _allTickets.Count(t => t.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase));

                // Clients opened tickets = all active tickets that are not closed
                OpenTicketsCount = _allTickets.Count(t => !t.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase));

                ApplyFilterAndSort();
            }

            // Fetch notification summary
            var notifs = await _apiClient.GetNotificationSummaryAsync();
            if (notifs != null)
            {
                UnreadNotifCount = notifs.UnreadCount;
                UnansweredCount = notifs.UnansweredTicketsCount;
                RecentNotifications = new ObservableCollection<NotificationDto>(notifs.RecentNotifications);

                // Push banner for unread notifications on Android
                foreach (var n in notifs.RecentNotifications.Where(x => !x.IsRead))
                {
                    if (!_notifiedNotificationIds.Contains(n.Id))
                    {
                        _notifiedNotificationIds.Add(n.Id);
                        _notificationService.ShowTicketNotification(n.TicketId ?? n.Id, n.Title, n.ShortMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] LoadTicketsAsync failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SetStatusFilter(string status)
    {
        SelectedStatus = status;
        ApplyFilterAndSort();
    }

    [RelayCommand]
    public void ToggleSortOrder()
    {
        if (CurrentSortMode == "Status")
        {
            CurrentSortMode = "Newest";
            CurrentSortLabel = "Newest First";
        }
        else if (CurrentSortMode == "Newest")
        {
            CurrentSortMode = "Oldest";
            CurrentSortLabel = "Oldest First";
        }
        else
        {
            CurrentSortMode = "Status";
            CurrentSortLabel = "Status (Active First)";
        }
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<SupportTicketSummaryDto> query = _allTickets;

        // 1. Status Filter
        if (!string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => string.Equals(t.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Sorting
        query = CurrentSortMode switch
        {
            "Status" => query.OrderBy(t => t.Status.ToLowerInvariant() switch
            {
                "open" => 1,
                "inprogress" => 2,
                "resolved" => 3,
                _ => 4
            }).ThenByDescending(t => t.UpdatedAt),
            "Newest" => query.OrderByDescending(t => t.UpdatedAt),
            "Oldest" => query.OrderBy(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.UpdatedAt)
        };

        FilteredTickets = new ObservableCollection<SupportTicketSummaryDto>(query.ToList());
    }

    [RelayCommand]
    public async Task SwitchTabAsync(string tabName)
    {
        IsAdminHelpdeskTab = tabName == "admin";
        await LoadTicketsAsync();
    }

    [RelayCommand]
    public void ToggleNotifications()
    {
        IsNotificationsVisible = !IsNotificationsVisible;
    }

    [RelayCommand]
    public async Task OpenNewTicketAsync()
    {
        var page = new NewTicketPage(_apiClient);
        page.TicketCreated += async (_, _) =>
        {
            await LoadTicketsAsync();
        };
        await Shell.Current.Navigation.PushModalAsync(page);
    }

    [RelayCommand]
    public async Task OpenTicketDetailAsync(SupportTicketSummaryDto? ticket)
    {
        if (ticket == null) return;
        var page = new SupportTicketDetailPage(ticket.Id, _apiClient, _authService);
        await Shell.Current.Navigation.PushAsync(page);
    }

    [RelayCommand]
    public async Task NotificationTappedAsync(NotificationDto? notif)
    {
        if (notif == null) return;
        IsNotificationsVisible = false;

        if (notif.TicketId.HasValue)
        {
            await _apiClient.MarkNotificationAsReadAsync(notif.Id);
            notif.IsRead = true;
            if (UnreadNotifCount > 0) UnreadNotifCount--;

            var page = new SupportTicketDetailPage(notif.TicketId.Value, _apiClient, _authService);
            await Shell.Current.Navigation.PushAsync(page);
        }
    }

    [RelayCommand]
    public void Logout()
    {
        _authService.Logout();
        _allTickets.Clear();
        FilteredTickets.Clear();
        RecentNotifications.Clear();
        UnreadNotifCount = 0;
        UnansweredCount = 0;
        OpenTicketsCount = 0;
        TotalTicketsCount = 0;
        _notifiedNotificationIds.Clear();
        _lastKnownStatuses.Clear();
        RefreshAuthState();
        if (Shell.Current is AppShell appShell)
        {
            appShell.UpdateAccountTabTitle();
        }
    }
}
