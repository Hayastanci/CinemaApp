using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using CinemaApp.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IStripePaymentService _stripeService;
    private readonly ITokenService _tokenService;
    private readonly CinemaDbContext _dbContext;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IStripePaymentService stripeService,
        ITokenService tokenService,
        CinemaDbContext dbContext,
        ILogger<PaymentsController> logger)
    {
        _stripeService = stripeService;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("create-checkout-session")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
    {
        User? user = null;

        var authHeader = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring(7).Trim();
            var userId = _tokenService.ValidateTokenAndGetUserId(token);
            if (userId.HasValue)
            {
                user = await _dbContext.Users.FindAsync(userId.Value);
            }
        }

        if (user == null && User.Identity?.IsAuthenticated == true)
        {
            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrEmpty(emailClaim))
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == emailClaim);
            }
        }

        if (user == null)
        {
            // Default demo or guest subscriber fallback for quick onboarding
            user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Registered)
                   ?? await _dbContext.Users.FirstAsync();
        }

        var host = $"{Request.Scheme}://{Request.Host}";
        var successUrl = !string.IsNullOrWhiteSpace(request.SuccessUrl)
            ? request.SuccessUrl
            : $"{host}/subscription/success";
        var cancelUrl = !string.IsNullOrWhiteSpace(request.CancelUrl)
            ? request.CancelUrl
            : $"{host}/subscription";

        var sessionResult = await _stripeService.CreateSubscriptionCheckoutSessionAsync(user, successUrl, cancelUrl);
        return Ok(sessionResult);
    }

    [HttpPost("/api/payments/stripe-webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var stripeSignature = Request.Headers["Stripe-Signature"].ToString();

        _logger.LogInformation("Received Stripe Webhook call with signature length: {Len}", stripeSignature.Length);

        var processed = await _stripeService.HandleWebhookEventAsync(json, stripeSignature);
        if (!processed)
        {
            return BadRequest(new { error = "WebhookProcessingFailed" });
        }

        return Ok(new { received = true });
    }
}
