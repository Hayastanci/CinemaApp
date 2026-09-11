namespace CinemaApp.Core.Entities;

public enum UserRole
{
    Admin,
    Registered,
    Guest
}

public static class StreamQualityConstants
{
    public const string P360 = "360p";
    public const string P480 = "480p";
    public const string P720 = "720p";
    public const string P1080 = "1080p";

    public static readonly string[] All = [P360, P480, P720, P1080];
    public static readonly string[] FreeQualities = [P360, P480];
    public static readonly string[] PremiumQualities = [P720, P1080];

    public static bool IsFreeQuality(string quality)
    {
        return quality.Equals(P360, StringComparison.OrdinalIgnoreCase) ||
               quality.Equals(P480, StringComparison.OrdinalIgnoreCase);
    }
}

public static class PaymentStatusConstants
{
    public const string Pending = "Pending";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}
