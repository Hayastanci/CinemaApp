using CinemaApp.Core.DTOs;
using CinemaApp.Mobile.Services;

namespace CinemaApp.Mobile.Views;

public partial class NewTicketPage : ContentPage
{
    private readonly CinemaApiClient _apiClient;
    public event EventHandler? TicketCreated;

    public NewTicketPage(CinemaApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        CategoryPicker.SelectedIndex = 0;
        PriorityPicker.SelectedIndex = 0;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        var subject = SubjectEntry.Text?.Trim();
        var message = MessageEditor.Text?.Trim();

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            ErrorLabel.Text = "Please enter both a subject and details.";
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        SubmitButton.IsEnabled = false;

        try
        {
            var category = CategoryPicker.SelectedItem?.ToString() ?? "General";
            var priority = PriorityPicker.SelectedItem?.ToString() ?? "Normal";

            var result = await _apiClient.CreateTicketAsync(new CreateTicketDto
            {
                Subject = subject,
                Category = category,
                Priority = priority,
                InitialMessage = message
            });

            if (result != null)
            {
                TicketCreated?.Invoke(this, EventArgs.Empty);
                await Navigation.PopModalAsync();
            }
            else
            {
                ErrorLabel.Text = "Could not submit ticket. Please check your connection.";
                ErrorLabel.IsVisible = true;
            }
        }
        finally
        {
            SubmitButton.IsEnabled = true;
        }
    }
}
