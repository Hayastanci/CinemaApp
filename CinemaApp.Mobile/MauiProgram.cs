using CommunityToolkit.Maui;
using CinemaApp.Mobile.Services;
using CinemaApp.Mobile.ViewModels;
using CinemaApp.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace CinemaApp.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement(false)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Use the native platform-agnostic sockets handler structure to bypass linker trimming blocks
        var handler = GetPlatformHttpMessageHandler();

        builder.Services.AddSingleton(sp => new HttpClient(handler));
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<CinemaApiClient>();
        builder.Services.AddSingleton<MobileNotificationService>();

        // Register ViewModels
        builder.Services.AddSingleton<CatalogViewModel>();
        builder.Services.AddTransient<MovieDetailViewModel>();
        builder.Services.AddTransient<PlayerViewModel>();
        builder.Services.AddTransient<LoginViewModel>();

        // Register Pages
        builder.Services.AddSingleton<CatalogPage>();
        builder.Services.AddTransient<MovieDetailPage>();
        builder.Services.AddTransient<PlayerPage>();
        builder.Services.AddTransient<LoginPage>();

        // Register Shell (needs AuthService injected for tab title updates)
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static HttpClientHandler GetPlatformHttpMessageHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        // Bypasses local computer SSL/Cleartext constraints cleanly without triggering cross-platform compilation errors
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
        return handler;
    }
}
