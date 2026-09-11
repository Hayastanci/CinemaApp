namespace CinemaApp.Core.Entities;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; } = string.Empty;
    public required string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Registered;
    public bool IsSubscribed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
