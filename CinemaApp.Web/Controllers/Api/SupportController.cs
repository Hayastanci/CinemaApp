using System.Security.Claims;
using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
public class SupportController : ControllerBase
{
    private readonly CinemaDbContext _dbContext;
    private readonly ILogger<SupportController> _logger;

    public SupportController(CinemaDbContext dbContext, ILogger<SupportController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var email = User.Claims.FirstOrDefault(c => 
            c.Type == ClaimTypes.Email || 
            c.Type == "email" || 
            c.Type.EndsWith("/emailaddress", StringComparison.OrdinalIgnoreCase))?.Value;

        if (!string.IsNullOrEmpty(email))
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());
            if (user != null) return user;
        }

        var idStr = User.Claims.FirstOrDefault(c => 
            c.Type == ClaimTypes.NameIdentifier || 
            c.Type == "sub" || 
            c.Type == "id" || 
            c.Type.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;

        if (int.TryParse(idStr, out var userId))
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null) return user;
        }

        return null;
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetTickets([FromQuery] bool all = false)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var query = _dbContext.SupportTickets
            .Include(t => t.Messages)
            .AsNoTracking();

        if (user.Role == UserRole.Admin && all)
        {
            // Admin viewing all tickets
        }
        else
        {
            // Match by either UserId OR UserEmail so tickets for subscriber@cinema.local always show
            query = query.Where(t => t.UserId == user.Id || t.UserEmail.ToLower() == user.Email.ToLower());
        }

        var tickets = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new SupportTicketSummaryDto
            {
                Id = t.Id,
                Subject = t.Subject,
                Category = t.Category,
                Priority = t.Priority,
                Status = t.Status,
                UserEmail = t.UserEmail,
                UserId = t.UserId,
                HasUnreadStaffReply = t.HasUnreadStaffReply,
                HasUnreadUserReply = t.HasUnreadUserReply,
                LastReplyPreview = t.LastMessagePreview,
                LastReplyBy = t.LastReplyByRole == UserRole.Admin ? "Admin Support" : "User",
                MessageCount = t.Messages.Count,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return Ok(tickets);
    }

    [HttpGet("tickets/{id:int}")]
    public async Task<IActionResult> GetTicketById(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var ticket = await _dbContext.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound(new { error = "Ticket not found" });

        if (user.Role != UserRole.Admin && ticket.UserId != user.Id && !string.Equals(ticket.UserEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        // If user reads staff reply, clear the unread flag
        if (user.Id == ticket.UserId && ticket.HasUnreadStaffReply)
        {
            ticket.HasUnreadStaffReply = false;
            var unreadNotifs = await _dbContext.UserNotifications
                .Where(n => n.UserId == user.Id && n.TicketId == ticket.Id && !n.IsRead)
                .ToListAsync();
            foreach (var n in unreadNotifs) n.IsRead = true;
            await _dbContext.SaveChangesAsync();
        }

        // If admin reads ticket, clear the unread user reply flag
        if (user.Role == UserRole.Admin && ticket.HasUnreadUserReply)
        {
            ticket.HasUnreadUserReply = false;
            await _dbContext.SaveChangesAsync();
        }

        var detail = new SupportTicketDetailDto
        {
            Id = ticket.Id,
            UserId = ticket.UserId,
            UserEmail = ticket.UserEmail,
            Subject = ticket.Subject,
            Category = ticket.Category,
            Priority = ticket.Priority,
            Status = ticket.Status,
            HasUnreadStaffReply = ticket.HasUnreadStaffReply,
            HasUnreadUserReply = ticket.HasUnreadUserReply,
            LastMessagePreview = ticket.LastMessagePreview,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            Messages = ticket.Messages.OrderBy(m => m.CreatedAt).Select(m => new TicketMessageDto
            {
                Id = m.Id,
                TicketId = m.TicketId,
                SenderUserId = m.SenderUserId,
                SenderRole = m.SenderRole,
                SenderEmail = m.SenderEmail,
                SenderName = string.IsNullOrEmpty(m.SenderName) ? m.SenderEmail.Split('@')[0] : m.SenderName,
                Message = m.Message,
                IsStaffReply = m.SenderRole == UserRole.Admin,
                CreatedAt = m.CreatedAt
            }).ToList()
        };

        return Ok(detail);
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto dto)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Subject) || string.IsNullOrWhiteSpace(dto.InitialMessage))
        {
            return BadRequest(new { error = "Subject and InitialMessage are required." });
        }

        var ticket = new SupportTicket
        {
            UserId = user.Id,
            UserEmail = user.Email,
            Subject = dto.Subject.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
            Priority = string.IsNullOrWhiteSpace(dto.Priority) ? "Normal" : dto.Priority.Trim(),
            Status = "Open",
            HasUnreadStaffReply = false,
            HasUnreadUserReply = true,
            LastMessagePreview = dto.InitialMessage.Length > 150 ? dto.InitialMessage[..147] + "..." : dto.InitialMessage,
            LastReplyByRole = user.Role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderUserId = user.Id,
            SenderRole = user.Role,
            SenderEmail = user.Email,
            SenderName = user.Email.Split('@')[0],
            Message = dto.InitialMessage.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TicketMessages.Add(message);

        // Notify Admin of new ticket
        _dbContext.UserNotifications.Add(new UserNotification
        {
            TicketId = ticket.Id,
            Title = $"New Ticket #{ticket.Id}: {ticket.Subject}",
            ShortMessage = message.Message.Length > 100 ? message.Message[..97] + "..." : message.Message,
            TargetRole = UserRole.Admin,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, new SupportTicketSummaryDto
        {
            Id = ticket.Id,
            Subject = ticket.Subject,
            Category = ticket.Category,
            Priority = ticket.Priority,
            Status = ticket.Status,
            UserEmail = ticket.UserEmail,
            UserId = ticket.UserId,
            HasUnreadStaffReply = false,
            HasUnreadUserReply = true,
            LastReplyPreview = ticket.LastMessagePreview,
            LastReplyBy = "User",
            MessageCount = 1,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        });
    }

    [HttpPost("tickets/{id:int}/reply")]
    public async Task<IActionResult> AddReply(int id, [FromBody] AddTicketReplyDto dto)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest(new { error = "Message cannot be empty." });
        }

        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        if (user.Role != UserRole.Admin && ticket.UserId != user.Id && !string.Equals(ticket.UserEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var isStaff = user.Role == UserRole.Admin;
        var cleanMessage = dto.Message.Trim();

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderUserId = user.Id,
            SenderRole = user.Role,
            SenderEmail = user.Email,
            SenderName = isStaff ? "Bartigran Support" : user.Email.Split('@')[0],
            Message = cleanMessage,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TicketMessages.Add(message);

        // Update ticket meta
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.LastMessagePreview = cleanMessage.Length > 150 ? cleanMessage[..147] + "..." : cleanMessage;
        ticket.LastReplyByRole = user.Role;

        if (isStaff)
        {
            ticket.HasUnreadStaffReply = true;
            ticket.HasUnreadUserReply = false;
            if (ticket.Status == "Open")
            {
                ticket.Status = "InProgress";
            }

            // Create notification for ticket owner
            _dbContext.UserNotifications.Add(new UserNotification
            {
                UserId = ticket.UserId,
                TicketId = ticket.Id,
                Title = $"Support Replied to Ticket #{ticket.Id}",
                ShortMessage = cleanMessage.Length > 100 ? cleanMessage[..97] + "..." : cleanMessage,
                TargetRole = UserRole.Registered,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            ticket.HasUnreadUserReply = true;
            ticket.HasUnreadStaffReply = false;

            // If user replies to a resolved ticket, reopen
            if (ticket.Status is "Resolved" or "Closed")
            {
                ticket.Status = "Open";
            }

            // Create notification for Admins
            _dbContext.UserNotifications.Add(new UserNotification
            {
                TicketId = ticket.Id,
                Title = $"User Reply on Ticket #{ticket.Id}",
                ShortMessage = cleanMessage.Length > 100 ? cleanMessage[..97] + "..." : cleanMessage,
                TargetRole = UserRole.Admin,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new TicketMessageDto
        {
            Id = message.Id,
            TicketId = message.TicketId,
            SenderUserId = message.SenderUserId,
            SenderRole = message.SenderRole,
            SenderEmail = message.SenderEmail,
            SenderName = message.SenderName,
            Message = message.Message,
            IsStaffReply = isStaff,
            CreatedAt = message.CreatedAt
        });
    }

    [HttpPut("tickets/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTicketStatusDto dto)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        if (user.Role != UserRole.Admin && ticket.UserId != user.Id)
        {
            return Forbid();
        }

        // Regular users can only close their ticket or reopen
        if (user.Role != UserRole.Admin && dto.Status != "Closed" && dto.Status != "Open")
        {
            return BadRequest(new { error = "Users can only Open or Close their tickets." });
        }

        var oldStatus = ticket.Status;
        ticket.Status = dto.Status;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (dto.Status is "Resolved" or "Closed")
        {
            ticket.HasUnreadUserReply = false;
        }

        var isAdmin = user.Role == UserRole.Admin;
        var adminActor = isAdmin ? (user.Email ?? "Admin") : user.Email;

        // Send notification to Admin users with bell
        var adminUsers = await _dbContext.Users.Where(u => u.Role == UserRole.Admin).ToListAsync();
        if (adminUsers.Count > 0)
        {
            foreach (var admin in adminUsers)
            {
                _dbContext.UserNotifications.Add(new UserNotification
                {
                    UserId = admin.Id,
                    TicketId = ticket.Id,
                    Title = $"Ticket #{ticket.Id} status: {dto.Status}",
                    ShortMessage = $"Status changed from '{oldStatus}' to '{dto.Status}' by {adminActor}",
                    TargetRole = UserRole.Admin,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            _dbContext.UserNotifications.Add(new UserNotification
            {
                TicketId = ticket.Id,
                Title = $"Ticket #{ticket.Id} status: {dto.Status}",
                ShortMessage = $"Status changed from '{oldStatus}' to '{dto.Status}' by {adminActor}",
                TargetRole = UserRole.Admin,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Also notify the ticket owner if the change was made by someone else
        if (ticket.UserId != user.Id)
        {
            _dbContext.UserNotifications.Add(new UserNotification
            {
                UserId = ticket.UserId,
                TicketId = ticket.Id,
                Title = $"Ticket #{ticket.Id} is now {dto.Status}",
                ShortMessage = $"Your ticket status was updated to '{dto.Status}'",
                TargetRole = UserRole.Registered,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, status = ticket.Status });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        int unansweredCount = 0;
        if (user.Role == UserRole.Admin)
        {
            // Unanswered count: tickets where status is Open or InProgress and latest reply is from user
            unansweredCount = await _dbContext.SupportTickets
                .Where(t => t.Status != "Closed" && t.Status != "Resolved" && t.HasUnreadUserReply)
                .CountAsync();
        }

        var notifQuery = _dbContext.UserNotifications
            .AsNoTracking();

        if (user.Role == UserRole.Admin)
        {
            notifQuery = notifQuery.Where(n => n.TargetRole == UserRole.Admin || n.UserId == user.Id);
        }
        else
        {
            notifQuery = notifQuery.Where(n => n.UserId == user.Id);
        }

        var unreadCount = await notifQuery.CountAsync(n => !n.IsRead);
        var recent = await notifQuery
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                TicketId = n.TicketId,
                Title = n.Title,
                ShortMessage = n.ShortMessage,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return Ok(new NotificationSummaryDto
        {
            UnreadCount = unreadCount,
            UnansweredTicketsCount = unansweredCount,
            RecentNotifications = recent
        });
    }

    [HttpPost("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var notif = await _dbContext.UserNotifications.FirstOrDefaultAsync(n => n.Id == id);
        if (notif != null)
        {
            if (user.Role == UserRole.Admin || notif.UserId == user.Id)
            {
                notif.IsRead = true;
                await _dbContext.SaveChangesAsync();
            }
        }
        return Ok(new { success = true });
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var query = _dbContext.UserNotifications.Where(n => !n.IsRead);
        if (user.Role == UserRole.Admin)
        {
            query = query.Where(n => n.TargetRole == UserRole.Admin || n.UserId == user.Id);
        }
        else
        {
            query = query.Where(n => n.UserId == user.Id);
        }

        var list = await query.ToListAsync();
        foreach (var item in list) item.IsRead = true;
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, markedCount = list.Count });
    }

    [HttpPost("push-token")]
    public IActionResult RegisterPushToken([FromBody] PushTokenRegistrationDto dto)
    {
        _logger.LogInformation("Received device push token registration: {Platform} - {Token}", dto.Platform, dto.Token);
        return Ok(new { success = true });
    }
}

public class PushTokenRegistrationDto
{
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "Android";
}
