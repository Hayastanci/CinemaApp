namespace CinemaApp.Core.Entities;

public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string? StripeSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string Status { get; set; } = PaymentStatusConstants.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
