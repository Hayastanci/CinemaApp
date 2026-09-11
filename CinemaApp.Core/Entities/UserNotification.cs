namespace CinemaApp.Core.Entities;

public class UserNotification
{
    public int Id { get; set; }
    public int? UserId { get; set; } // Specific user or null for all Admins
    public int? TicketId { get; set; }
    public required string Title { get; set; } = string.Empty;
    public required string ShortMessage { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public UserRole? TargetRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public SupportTicket? Ticket { get; set; }
}
