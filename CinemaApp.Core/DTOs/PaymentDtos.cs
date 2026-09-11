namespace CinemaApp.Core.DTOs;

public class CreateCheckoutSessionRequest
{
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

public class CheckoutSessionResultDto
{
    public string SessionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
}
