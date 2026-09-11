using System.Security.Claims;
using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers;

[Authorize]
public class SupportController : Controller
{
    private readonly CinemaDbContext _dbContext;

    public SupportController(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return null;
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    [HttpGet]
    [Route("support")]
    public async Task<IActionResult> Index([FromQuery] string? view, [FromQuery] string? status)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var isAdmin = user.Role == UserRole.Admin;
        var showAdminHelpdesk = isAdmin && (view == "admin" || string.IsNullOrEmpty(view));

        var query = _dbContext.SupportTickets
            .Include(t => t.Messages)
            .AsNoTracking();

        if (showAdminHelpdesk)
        {
            // Admin helpdesk: see all tickets
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                if (status == "Unanswered")
                {
                    query = query.Where(t => t.Status != "Closed" && t.Status != "Resolved" && t.HasUnreadUserReply);
                }
                else
                {
                    query = query.Where(t => t.Status == status);
                }
            }
        }
        else
        {
            // Regular user: see only own tickets
            query = query.Where(t => t.UserId == user.Id);
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(t => t.Status == status);
            }
        }

        var tickets = await query
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();

        var unansweredCount = 0;
        if (isAdmin)
        {
            unansweredCount = await _dbContext.SupportTickets
                .Where(t => t.Status != "Closed" && t.Status != "Resolved" && t.HasUnreadUserReply)
                .CountAsync();
        }

        var unreadUserNotifs = await _dbContext.UserNotifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .CountAsync();

        ViewBag.IsAdmin = isAdmin;
        ViewBag.ShowAdminHelpdesk = showAdminHelpdesk;
        ViewBag.CurrentStatus = status ?? "All";
        ViewBag.UnansweredCount = unansweredCount;
        ViewBag.UnreadCount = unreadUserNotifs;
        ViewBag.CurrentUser = user;

        return View(tickets);
    }

    [HttpGet]
    [Route("support/new")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Route("support/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string subject, string category, string priority, string message)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
        {
            ViewBag.Error = "Subject and message are required.";
            return View();
        }

        var ticket = new SupportTicket
        {
            UserId = user.Id,
            UserEmail = user.Email,
            Subject = subject.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
            Priority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority.Trim(),
            Status = "Open",
            HasUnreadStaffReply = false,
            HasUnreadUserReply = true,
            LastMessagePreview = message.Length > 150 ? message[..147] + "..." : message,
            LastReplyByRole = user.Role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync();

        var ticketMsg = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderUserId = user.Id,
            SenderRole = user.Role,
            SenderEmail = user.Email,
            SenderName = user.Email.Split('@')[0],
            Message = message.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TicketMessages.Add(ticketMsg);

        // Create Admin notification
        _dbContext.UserNotifications.Add(new UserNotification
        {
            TicketId = ticket.Id,
            Title = $"New Ticket #{ticket.Id}: {ticket.Subject}",
            ShortMessage = message.Length > 100 ? message[..97] + "..." : message,
            TargetRole = UserRole.Admin,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = "Your support ticket has been created successfully!";
        return RedirectToAction(nameof(Details), new { id = ticket.Id });
    }

    [HttpGet]
    [Route("support/ticket/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var ticket = await _dbContext.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();

        var isAdmin = user.Role == UserRole.Admin;
        if (!isAdmin && ticket.UserId != user.Id)
        {
            return Forbid();
        }

        // If ticket owner views, clear staff unread flag and user notifications
        if (ticket.UserId == user.Id && ticket.HasUnreadStaffReply)
        {
            ticket.HasUnreadStaffReply = false;
            var notifs = await _dbContext.UserNotifications
                .Where(n => n.UserId == user.Id && n.TicketId == ticket.Id && !n.IsRead)
                .ToListAsync();
            foreach (var n in notifs) n.IsRead = true;
            await _dbContext.SaveChangesAsync();
        }

        // If admin views, clear user unread reply flag
        if (isAdmin && ticket.HasUnreadUserReply)
        {
            ticket.HasUnreadUserReply = false;
            await _dbContext.SaveChangesAsync();
        }

        ViewBag.IsAdmin = isAdmin;
        ViewBag.CurrentUser = user;

        return View(ticket);
    }

    [HttpPost]
    [Route("support/ticket/{id:int}/reply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReply(int id, string message)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(message))
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        var isAdmin = user.Role == UserRole.Admin;
        if (!isAdmin && ticket.UserId != user.Id)
        {
            return Forbid();
        }

        var clean = message.Trim();
        var ticketMsg = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderUserId = user.Id,
            SenderRole = user.Role,
            SenderEmail = user.Email,
            SenderName = isAdmin ? "Bartigran Support" : user.Email.Split('@')[0],
            Message = clean,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TicketMessages.Add(ticketMsg);

        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.LastMessagePreview = clean.Length > 150 ? clean[..147] + "..." : clean;
        ticket.LastReplyByRole = user.Role;

        if (isAdmin)
        {
            ticket.HasUnreadStaffReply = true;
            ticket.HasUnreadUserReply = false;
            if (ticket.Status == "Open")
            {
                ticket.Status = "InProgress";
            }

            _dbContext.UserNotifications.Add(new UserNotification
            {
                UserId = ticket.UserId,
                TicketId = ticket.Id,
                Title = $"Support Replied to Ticket #{ticket.Id}",
                ShortMessage = clean.Length > 100 ? clean[..97] + "..." : clean,
                TargetRole = UserRole.Registered,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            ticket.HasUnreadUserReply = true;
            ticket.HasUnreadStaffReply = false;
            if (ticket.Status is "Resolved" or "Closed")
            {
                ticket.Status = "Open";
            }

            _dbContext.UserNotifications.Add(new UserNotification
            {
                TicketId = ticket.Id,
                Title = $"User Reply on Ticket #{ticket.Id}",
                ShortMessage = clean.Length > 100 ? clean[..97] + "..." : clean,
                TargetRole = UserRole.Admin,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Route("support/ticket/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HandleTicketPost(int id, [FromForm] string? status, [FromForm] string? message, [FromQuery] string? returnUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            return await UpdateStatus(id, status, returnUrl);
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            return await AddReply(id, message);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Route("support/ticket/{id:int}/status")]
    [Route("Support/UpdateStatus/{id:int?}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status, [FromQuery] string? returnUrl = null)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        var isAdmin = user.Role == UserRole.Admin;
        if (!isAdmin && ticket.UserId != user.Id)
        {
            return Forbid();
        }

        var oldStatus = ticket.Status;
        ticket.Status = status;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (status is "Resolved" or "Closed")
        {
            ticket.HasUnreadUserReply = false;
        }

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
                    Title = $"Ticket #{ticket.Id} status: {status}",
                    ShortMessage = $"Status changed from '{oldStatus}' to '{status}' by {adminActor}",
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
                Title = $"Ticket #{ticket.Id} status: {status}",
                ShortMessage = $"Status changed from '{oldStatus}' to '{status}' by {adminActor}",
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
                Title = $"Ticket #{ticket.Id} is now {status}",
                ShortMessage = $"Your ticket status was updated to '{status}'",
                TargetRole = UserRole.Registered,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Ticket #{ticket.Id} status changed to {status}.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Route("support/notifications/{id:int}/read")]
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
        return Json(new { success = true });
    }

    [HttpPost]
    [Route("support/notifications/read-all")]
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

        return Json(new { success = true, markedCount = list.Count });
    }

    [HttpGet]
    [Route("support/notifications/feed")]
    public async Task<IActionResult> GetNotificationFeed()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var isAdmin = user.Role == UserRole.Admin;
        int unansweredCount = 0;
        if (isAdmin)
        {
            unansweredCount = await _dbContext.SupportTickets
                .Where(t => t.Status != "Closed" && t.Status != "Resolved" && t.HasUnreadUserReply)
                .CountAsync();
        }

        var query = _dbContext.UserNotifications.AsNoTracking();
        if (isAdmin)
        {
            query = query.Where(n => n.TargetRole == UserRole.Admin || n.UserId == user.Id);
        }
        else
        {
            query = query.Where(n => n.UserId == user.Id);
        }

        var unreadCount = await query.CountAsync(n => !n.IsRead);
        var recent = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(8)
            .Select(n => new
            {
                n.Id,
                n.TicketId,
                n.Title,
                n.ShortMessage,
                n.IsRead,
                CreatedAt = n.CreatedAt.ToString("g")
            })
            .ToListAsync();

        return Json(new
        {
            unreadCount,
            unansweredCount,
            recent
        });
    }
}
