namespace CinemaApp.Mobile;

/// <summary>
/// Centralized API endpoint configuration for the mobile client.
/// 
/// IMPORTANT NETWORKING RULES:
/// - 0.0.0.0 is ONLY a server bind address ("listen on all network cards"). Clients CANNOT connect to 0.0.0.0.
/// - In Production (Docker / Cloud VPS): Point to your public server IP or domain (e.g., https://cinema.yourdomain.com or http://YOUR_SERVER_IP:8080).
/// - In Android Emulator: 10.0.2.2 is the virtual loopback address that maps to the host PC (e.g., http://10.0.2.2:5109 or http://10.0.2.2:8080 for Docker).
/// - On Physical Device via Wi-Fi: Use your host PC's LAN IP (e.g., http://192.168.1.100:5109).
/// - On Physical Device via ADB Reverse: Use http://localhost:5109 (requires: adb reverse tcp:5109 tcp:5109).
/// </summary>
public static class ApiConfig
{
    /// <summary>
    /// Set this to your production domain or VPS public IP when deploying Docker to production.
    /// Example: "https://cinema.bartigran.com" or "http://203.0.113.10:8080"
    /// </summary>
    public const string ProductionUrl = "http://localhost:5109";

    public static readonly string[] Candidates =
    {
        // 1. Production / Custom Server endpoint (top priority when configured)
        ProductionUrl,

        // 2. Android Emulator -> Local Kestrel Web App (Port 5109)
        "http://10.0.2.2:5109",

        // 3. Android Emulator -> Docker Container (Port 8080 mapped in docker-compose)
        "http://10.0.2.2:8080",

        // 4. ADB Reverse Proxy or Desktop / Windows Localhost
        "http://localhost:5109",
        "http://localhost:8080",

        // 5. Local Network Wi-Fi IP (for physical Android phone testing on same Wi-Fi)
        "http://192.168.2.104:5109",
        "http://192.168.2.104:8080",

        // 6. HTTPS Local (requires trusted development cert)
        "https://10.0.2.2:7078",
        "https://localhost:7078"
    };

    /// <summary>
    /// Default fallback endpoint if dynamic probing hasn't finished.
    /// Defaults to the Android emulator bridge (10.0.2.2:5109) or ProductionUrl.
    /// </summary>
    public static string ServerUrl => ProductionUrl.StartsWith("http") && !ProductionUrl.Contains("localhost") 
        ? ProductionUrl 
        : "https://0.0.0.0:7078";
}
