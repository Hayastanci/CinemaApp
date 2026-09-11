using System.Security.Claims;
using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using CinemaApp.Core.Security;
using CinemaApp.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers;

public class AccountController : Controller
{
    private readonly CinemaDbContext _dbContext;
    private readonly IStripePaymentService _stripePaymentService;

    public AccountController(CinemaDbContext dbContext, IStripePaymentService stripePaymentService)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
    }

    [HttpGet]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Please enter both email and password.";
            return View();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null || !PasswordHasher.VerifyPassword(password, user.PasswordHash))
        {
            ViewBag.Error = "Invalid email or password.";
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("is_subscribed", user.IsSubscribed ? "true" : "false")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(string email, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Email and password are required.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Passwords do not match.";
            return View();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            ViewBag.Error = "An account with this email already exists.";
            return View();
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = PasswordHasher.HashPassword(password),
            Role = UserRole.Registered,
            IsSubscribed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Route("subscription")]
    public async Task<IActionResult> Subscription()
    {
        User? user = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            }
        }

        ViewBag.IsSubscribed = user?.IsSubscribed == true;
        return View();
    }

    [HttpPost]
    [Route("subscription/checkout")]
    public async Task<IActionResult> StartSubscriptionCheckout()
    {
        User? user = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            }
        }

        if (user == null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl = "/subscription" });
        }

        var host = $"{Request.Scheme}://{Request.Host}";
        var session = await _stripePaymentService.CreateSubscriptionCheckoutSessionAsync(
            user,
            $"{host}/subscription/success",
            $"{host}/subscription");

        return Redirect(session.CheckoutUrl);
    }

    [HttpGet]
    [Route("subscription/success")]
    public async Task<IActionResult> SubscriptionSuccess([FromQuery] string? session_id)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(email))
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null && !user.IsSubscribed)
                {
                    user.IsSubscribed = true;
                    _dbContext.Transactions.Add(new Transaction
                    {
                        UserId = user.Id,
                        StripeSessionId = session_id ?? "demo_session",
                        Amount = 9.99m,
                        Currency = "usd",
                        Status = PaymentStatusConstants.Succeeded,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        return View();
    }

    [HttpGet]
    [Route("profile")]
    [Route("account/profile")]
    public async Task<IActionResult> Profile()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction(nameof(Login), new { returnUrl = "/profile" });
        }

        var email = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email)) return RedirectToAction(nameof(Login));

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null) return RedirectToAction(nameof(Login));

        var ticketCount = await _dbContext.SupportTickets.CountAsync(t => t.UserId == user.Id);
        var openTicketCount = await _dbContext.SupportTickets.CountAsync(t => t.UserId == user.Id && t.Status != "Closed");

        ViewBag.User = user;
        ViewBag.TicketCount = ticketCount;
        ViewBag.OpenTicketCount = openTicketCount;

        return View(user);
    }
}
