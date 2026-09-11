using CinemaApp.Core.Data;
using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;

namespace CinemaApp.Core.Services;

public class StripePaymentService : IStripePaymentService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        CinemaDbContext dbContext,
        IConfiguration configuration,
        ILogger<StripePaymentService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;

        var apiKey = _configuration["Stripe:SecretKey"];
        if (!string.IsNullOrWhiteSpace(apiKey) && !apiKey.Contains("YOUR_"))
        {
            StripeConfiguration.ApiKey = apiKey;
        }
    }

    public async Task<CheckoutSessionResultDto> CreateSubscriptionCheckoutSessionAsync(
        User user,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Stripe:SecretKey"];
        var pubKey = _configuration["Stripe:PublishableKey"] ?? "pk_test_sample";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("YOUR_"))
        {
            _logger.LogInformation("Stripe test mode: generating sandbox checkout session for user {UserId}", user.Id);
            var mockSessionId = $"cs_test_{Guid.NewGuid():N}";
            return new CheckoutSessionResultDto
            {
                SessionId = mockSessionId,
                CheckoutUrl = successUrl.Contains("?")
                    ? $"{successUrl}&session_id={mockSessionId}"
                    : $"{successUrl}?session_id={mockSessionId}",
                PublishableKey = pubKey
            };
        }

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            CustomerEmail = user.Email,
            ClientReferenceId = user.Id.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "UserId", user.Id.ToString() }
            },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = 999, // $9.99
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Cinema Pro All-Access Subscription",
                            Description = "Unlimited 1080p and 720p HD streaming access"
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return new CheckoutSessionResultDto
        {
            SessionId = session.Id,
            CheckoutUrl = session.Url ?? successUrl,
            PublishableKey = pubKey
        };
    }

    public async Task<bool> HandleWebhookEventAsync(
        string jsonPayload,
        string stripeSignatureHeader,
        CancellationToken cancellationToken = default)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        try
        {
            Event? stripeEvent = null;

            if (!string.IsNullOrWhiteSpace(webhookSecret) && !webhookSecret.Contains("YOUR_"))
            {
                stripeEvent = EventUtility.ConstructEvent(jsonPayload, stripeSignatureHeader, webhookSecret);
            }
            else
            {
                // Unsigned / local simulation parse
                stripeEvent = EventUtility.ParseEvent(jsonPayload, throwOnApiVersionMismatch: false);
            }

            if (stripeEvent == null)
            {
                _logger.LogWarning("Failed to parse Stripe webhook event.");
                return false;
            }

            _logger.LogInformation("Processing Stripe webhook event type: {Type}", stripeEvent.Type);

            if (stripeEvent.Type == "checkout.session.completed")
            {
                if (stripeEvent.Data.Object is Session session)
                {
                    var userIdStr = session.ClientReferenceId ?? (session.Metadata != null && session.Metadata.TryGetValue("UserId", out var uid) ? uid : null);
                    if (int.TryParse(userIdStr, out var userId))
                    {
                        var user = await _dbContext.Users.FindAsync([userId], cancellationToken);
                        if (user != null)
                        {
                            user.IsSubscribed = true;

                            var transaction = new Transaction
                            {
                                UserId = user.Id,
                                StripeSessionId = session.Id,
                                StripePaymentIntentId = session.PaymentIntentId,
                                Amount = (session.AmountTotal ?? 999) / 100m,
                                Currency = session.Currency ?? "usd",
                                Status = PaymentStatusConstants.Succeeded,
                                CreatedAt = DateTime.UtcNow
                            };

                            _dbContext.Transactions.Add(transaction);
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            _logger.LogInformation("Successfully upgraded user {UserId} to Subscribed and recorded transaction.", userId);
                            return true;
                        }
                    }
                }
            }
            else if (stripeEvent.Type == "payment_intent.succeeded")
            {
                if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
                {
                    if (paymentIntent.Metadata != null && paymentIntent.Metadata.TryGetValue("UserId", out var uid) && int.TryParse(uid, out var userId))
                    {
                        var user = await _dbContext.Users.FindAsync([userId], cancellationToken);
                        if (user != null)
                        {
                            user.IsSubscribed = true;

                            var transaction = new Transaction
                            {
                                UserId = user.Id,
                                StripePaymentIntentId = paymentIntent.Id,
                                Amount = paymentIntent.Amount / 100m,
                                Currency = paymentIntent.Currency,
                                Status = PaymentStatusConstants.Succeeded,
                                CreatedAt = DateTime.UtcNow
                            };

                            _dbContext.Transactions.Add(transaction);
                            await _dbContext.SaveChangesAsync(cancellationToken);
                            return true;
                        }
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook handling failed.");
            return false;
        }
    }
}
