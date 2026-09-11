using CinemaApp.Core.DTOs;
using CinemaApp.Core.Entities;

namespace CinemaApp.Core.Services;

public interface IStripePaymentService
{
    Task<CheckoutSessionResultDto> CreateSubscriptionCheckoutSessionAsync(
        User user,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    Task<bool> HandleWebhookEventAsync(
        string jsonPayload,
        string stripeSignatureHeader,
        CancellationToken cancellationToken = default);
}
