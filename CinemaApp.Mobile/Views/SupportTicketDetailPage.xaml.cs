using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using CinemaApp.Mobile.Services;

namespace CinemaApp.Mobile.Views;

public partial class SupportTicketDetailPage : ContentPage
{
    private readonly int _ticketId;
    private readonly CinemaApiClient _apiClient;
    private readonly AuthService _authService;
    private SupportTicketDetailDto? _ticketDetail;
    private IDispatcherTimer? _autoRefreshTimer;

    public SupportTicketDetailPage(int ticketId, CinemaApiClient apiClient, AuthService authService)
    {
        InitializeComponent();
        _ticketId = ticketId;
        _apiClient = apiClient;
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTicketAsync();
        StartAutoRefresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        if (_autoRefreshTimer != null && _autoRefreshTimer.IsRunning) return;

        _autoRefreshTimer = Dispatcher.CreateTimer();
        if (_autoRefreshTimer != null)
        {
            _autoRefreshTimer.Interval = TimeSpan.FromSeconds(3);
            _autoRefreshTimer.Tick += async (_, _) =>
            {
                await LoadTicketAsync(silent: true);
            };
            _autoRefreshTimer.Start();
        }
    }

    private void StopAutoRefresh()
    {
        if (_autoRefreshTimer != null)
        {
            _autoRefreshTimer.Stop();
            _autoRefreshTimer = null;
        }
    }

    private async Task LoadTicketAsync(bool silent = false)
    {
        var detail = await _apiClient.GetTicketDetailsAsync(_ticketId);
        if (detail == null)
        {
            if (!silent)
            {
                await DisplayAlertAsync("Error", "Could not load ticket details.", "OK");
            }
            return;
        }
        _ticketDetail = detail;

        TicketIdLabel.Text = $"#{_ticketDetail.Id}";
        SubjectLabel.Text = _ticketDetail.Subject;
        CategoryLabel.Text = _ticketDetail.Category;
        StatusLabel.Text = _ticketDetail.Status.ToUpperInvariant();

        // Color status badge
        StatusBorder.BackgroundColor = _ticketDetail.Status switch
        {
            "Open" => Color.FromArgb("#10B981"),
            "InProgress" => Color.FromArgb("#06B6D4"),
            "Resolved" => Color.FromArgb("#F59E0B"),
            _ => Color.FromArgb("#64748B")
        };

        if (_authService.IsAdmin)
        {
            AdminControlsGrid.IsVisible = true;
            CloseTicketButton.IsVisible = false;
            AdminStatusPicker.SelectedItem = _ticketDetail.Status;
        }
        else
        {
            AdminControlsGrid.IsVisible = false;
            CloseTicketButton.IsVisible = _ticketDetail.Status != "Closed";
        }

        RenderMessages(_ticketDetail.Messages);
    }

    private void RenderMessages(List<TicketMessageDto> messages)
    {
        MessagesContainer.Children.Clear();

        foreach (var msg in messages.OrderBy(m => m.CreatedAt))
        {
            var isStaff = msg.IsStaffReply || msg.SenderRole == UserRole.Admin;

            var bubbleLayout = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = isStaff ? LayoutOptions.Start : LayoutOptions.End
            };

            var headerLabel = new Label
            {
                Text = isStaff ? "🛡️ Bartigran Support" : (string.IsNullOrEmpty(msg.SenderName) ? msg.SenderEmail : msg.SenderName),
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = isStaff ? Color.FromArgb("#38BDF8") : Color.FromArgb("#94A3B8"),
                HorizontalOptions = isStaff ? LayoutOptions.Start : LayoutOptions.End
            };

            var bubble = new Border
            {
                BackgroundColor = isStaff ? Color.FromArgb("#0E2238") : Color.FromArgb("#162032"),
                Stroke = isStaff ? Color.FromArgb("#06B6D4") : Color.FromArgb("#1E2638"),
                StrokeThickness = isStaff ? 1 : 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
                Padding = new Thickness(12, 8),
                MaximumWidthRequest = 280
            };

            var messageText = new Label
            {
                Text = msg.Message,
                FontSize = 13,
                TextColor = Color.FromArgb("#F8FAFC"),
                LineBreakMode = LineBreakMode.WordWrap
            };
            bubble.Content = messageText;

            var timeLabel = new Label
            {
                Text = msg.CreatedAt.ToString("g"),
                FontSize = 9,
                TextColor = Color.FromArgb("#64748B"),
                HorizontalOptions = isStaff ? LayoutOptions.Start : LayoutOptions.End
            };

            bubbleLayout.Children.Add(headerLabel);
            bubbleLayout.Children.Add(bubble);
            bubbleLayout.Children.Add(timeLabel);

            MessagesContainer.Children.Add(bubbleLayout);
        }

        // Auto scroll to bottom
        Task.Delay(100).ContinueWith(_ =>
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await MessagesScrollView.ScrollToAsync(0, MessagesContainer.Height, true);
            });
        });
    }

    private async void OnSendReplyClicked(object? sender, EventArgs e)
    {
        var text = ReplyEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        SendButton.IsEnabled = false;
        try
        {
            var res = await _apiClient.SendTicketReplyAsync(_ticketId, text);
            if (res != null)
            {
                ReplyEntry.Text = string.Empty;
                await LoadTicketAsync();
            }
            else
            {
                await DisplayAlertAsync("Error", "Could not send reply.", "OK");
            }
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private async void OnCloseTicketClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync("Close Ticket", "Are you sure you want to close this support ticket?", "Yes", "No");
        if (!confirm) return;

        var success = await _apiClient.UpdateTicketStatusAsync(_ticketId, "Closed");
        if (success)
        {
            await LoadTicketAsync();
        }
    }

    private async void OnUpdateStatusClicked(object? sender, EventArgs e)
    {
        var selectedStatus = AdminStatusPicker.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selectedStatus)) return;

        var success = await _apiClient.UpdateTicketStatusAsync(_ticketId, selectedStatus);
        if (success)
        {
            await LoadTicketAsync();
            await DisplayAlertAsync("Success", $"Status updated to {selectedStatus}", "OK");
        }
        else
        {
            await DisplayAlertAsync("Error", "Could not update status.", "OK");
        }
    }
}
