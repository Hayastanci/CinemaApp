namespace CinemaApp.Core.Entities;

public class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int SenderUserId { get; set; }
    public UserRole SenderRole { get; set; } = UserRole.Registered;
    public required string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public required string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SupportTicket? Ticket { get; set; }
    public User? SenderUser { get; set; }
}
