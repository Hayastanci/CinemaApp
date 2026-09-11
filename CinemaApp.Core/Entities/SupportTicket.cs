namespace CinemaApp.Core.Entities;

public class SupportTicket
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string UserEmail { get; set; } = string.Empty;
    public required string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Technical, Streaming & Playback, Billing & Subscription, Movie Request, General
    public string Priority { get; set; } = "Normal"; // Normal, High, Urgent
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed

    public bool HasUnreadStaffReply { get; set; } = false;
    public bool HasUnreadUserReply { get; set; } = true;
    public string? LastMessagePreview { get; set; }
    public UserRole LastReplyByRole { get; set; } = UserRole.Registered;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}
