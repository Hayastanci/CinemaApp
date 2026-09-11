using CinemaApp.Core.Entities;

namespace CinemaApp.Core.DTOs;

public class SupportTicketSummaryDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Open";
    public string UserEmail { get; set; } = string.Empty;
    public int UserId { get; set; }
    public bool HasUnreadStaffReply { get; set; }
    public bool HasUnreadUserReply { get; set; }
    public string? LastReplyPreview { get; set; }
    public string? LastReplyBy { get; set; }
    public int MessageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SupportTicketDetailDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Open";
    public bool HasUnreadStaffReply { get; set; }
    public bool HasUnreadUserReply { get; set; }
    public string? LastMessagePreview { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TicketMessageDto> Messages { get; set; } = new();
}

public class TicketMessageDto
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int SenderUserId { get; set; }
    public UserRole SenderRole { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsStaffReply { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTicketDto
{
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Normal";
    public string InitialMessage { get; set; } = string.Empty;
}

public class AddTicketReplyDto
{
    public string Message { get; set; } = string.Empty;
}

public class UpdateTicketStatusDto
{
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed
}

public class NotificationDto
{
    public int Id { get; set; }
    public int? TicketId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ShortMessage { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public int UnansweredTicketsCount { get; set; }
    public List<NotificationDto> RecentNotifications { get; set; } = new();
}
