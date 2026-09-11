using System.Text;
using CinemaApp.Core.Data;
using CinemaApp.Core.Entities;
using CinemaApp.Core.Security;
using CinemaApp.Core.Services;
using CinemaApp.Web.BackgroundServices;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 0. Allow large master-video uploads (staged for FFmpeg transcoding)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 60_000_000_000; // ~60 GB
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 60_000_000_000;
    options.ValueLengthLimit = 2_147_483_647;
});

// 1. Database Configuration (MySQL with EF Core 10)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Port=3306;Database=cinema_db;User=cinema_user;Password=cinema_secure_password_2026;";

builder.Services.AddDbContext<CinemaDbContext>(options =>
{
    options.UseMySQL(connectionString);
});

// 2. Core Business Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAiContentService, AiContentService>();
builder.Services.AddSingleton<IFFmpegService, FFmpegService>();
builder.Services.AddSingleton<ITranscodingQueue, TranscodingQueue>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IStripePaymentService, StripePaymentService>();

// 3. Background FFmpeg Transcoding Worker
builder.Services.AddHostedService<VideoTranscodingWorker>();

// 4. Dual Authentication (Cookies for MVC Web UI + JWT Bearer for REST Web API)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "CinemaApp_Ultra_Secure_Secret_Key_For_Net10_Cinema_App_2026!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CinemaApp";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CinemaAppClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ClockSkew = TimeSpan.Zero
    };
});

// 5. CORS for .NET MAUI Mobile and External Clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllersWithViews();

// Swagger/OpenAPI for API discovery (restricted to Admin role in the pipeline)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 6. Automatic Database Creation & Seeding on Startup
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        logger.LogInformation("Verifying and ensuring database creation and seeding...");
        db.Database.EnsureCreated();
        await SeedRuntimeDataAsync(db);
        logger.LogInformation("Database initialized successfully.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not automatically initialize MySQL database on startup. Ensure MySQL server credentials are active.");
    }
}

// 7. HTTP Request Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Skip HTTPS redirection for media/images/API requests. HttpsRedirectionMiddleware would
// otherwise 307-redirect emulator/device requests (non-loopback hosts) to the self-signed
// HTTPS port, which breaks image loading (Glide cannot trust the dev certificate).
app.UseWhen(
    context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return !path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWith("/movies/", StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
    },
    branch => branch.UseHttpsRedirection());

app.UseStaticFiles();

// Serve local media storage if directory exists
// Use configured path from appsettings.json (works on both Windows and Linux)
var mediaRoot = builder.Configuration["FFmpeg:MediaRoot"] 
    ?? (OperatingSystem.IsWindows() 
        ? Path.Combine("C:\\", "CinemaMedia") 
        : "/var/www/cinema/media");
Directory.CreateDirectory(mediaRoot);
Directory.CreateDirectory(Path.Combine(mediaRoot, "movies"));
Directory.CreateDirectory(Path.Combine(mediaRoot, "staging"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mediaRoot),
    RequestPath = "/media"
});

// Backward-compatible serving for older /movies/{id}/poster.jpg URLs
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(mediaRoot, "movies")),
    RequestPath = "/movies"
});

app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();

// Restrict Swagger UI + OpenAPI document to Admin users only
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        if (!context.User.IsInRole("Admin"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    await next();
});

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CinemaApp API v1"));

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();

static async Task SeedRuntimeDataAsync(CinemaDbContext db)
{
    // Ensure the ratings table exists on pre-existing databases created before
    // the MovieRatings entity was added (EnsureCreated only models fresh DBs).
    await db.Database.ExecuteSqlRawAsync(
        @"CREATE TABLE IF NOT EXISTS `MovieRatings` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `MovieId` int NOT NULL,
            `VisitorId` varchar(64) NOT NULL,
            `Score` int NOT NULL,
            `CreatedAt` datetime(6) NOT NULL,
            PRIMARY KEY (`Id`),
            UNIQUE KEY `UX_MovieRatings_MovieId_VisitorId` (`MovieId`, `VisitorId`),
            KEY `IX_MovieRatings_MovieId` (`MovieId`),
            CONSTRAINT `FK_MovieRatings_Movies_MovieId` FOREIGN KEY (`MovieId`) REFERENCES `Movies` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // Ensure SupportTickets, TicketMessages, and UserNotifications tables exist
    await db.Database.ExecuteSqlRawAsync(
        @"CREATE TABLE IF NOT EXISTS `SupportTickets` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `UserId` int NOT NULL,
            `UserEmail` varchar(150) NOT NULL,
            `Subject` varchar(200) NOT NULL,
            `Category` varchar(50) NOT NULL,
            `Priority` varchar(20) NOT NULL,
            `Status` varchar(30) NOT NULL,
            `HasUnreadStaffReply` tinyint(1) NOT NULL DEFAULT 0,
            `HasUnreadUserReply` tinyint(1) NOT NULL DEFAULT 1,
            `LastMessagePreview` varchar(500) NULL,
            `LastReplyByRole` varchar(30) NOT NULL DEFAULT 'Registered',
            `CreatedAt` datetime(6) NOT NULL,
            `UpdatedAt` datetime(6) NOT NULL,
            PRIMARY KEY (`Id`),
            KEY `IX_SupportTickets_UserId` (`UserId`),
            CONSTRAINT `FK_SupportTickets_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    await db.Database.ExecuteSqlRawAsync(
        @"CREATE TABLE IF NOT EXISTS `TicketMessages` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `TicketId` int NOT NULL,
            `SenderUserId` int NOT NULL,
            `SenderRole` varchar(30) NOT NULL,
            `SenderEmail` varchar(150) NOT NULL,
            `SenderName` varchar(100) NULL,
            `Message` text NOT NULL,
            `CreatedAt` datetime(6) NOT NULL,
            PRIMARY KEY (`Id`),
            KEY `IX_TicketMessages_TicketId` (`TicketId`),
            KEY `IX_TicketMessages_SenderUserId` (`SenderUserId`),
            CONSTRAINT `FK_TicketMessages_SupportTickets_TicketId` FOREIGN KEY (`TicketId`) REFERENCES `SupportTickets` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_TicketMessages_Users_SenderUserId` FOREIGN KEY (`SenderUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    await db.Database.ExecuteSqlRawAsync(
        @"CREATE TABLE IF NOT EXISTS `UserNotifications` (
            `Id` int NOT NULL AUTO_INCREMENT,
            `UserId` int NULL,
            `TicketId` int NULL,
            `Title` varchar(200) NOT NULL,
            `ShortMessage` varchar(500) NOT NULL,
            `IsRead` tinyint(1) NOT NULL DEFAULT 0,
            `TargetRole` varchar(30) NULL,
            `CreatedAt` datetime(6) NOT NULL,
            PRIMARY KEY (`Id`),
            KEY `IX_UserNotifications_UserId` (`UserId`),
            KEY `IX_UserNotifications_TicketId` (`TicketId`),
            CONSTRAINT `FK_UserNotifications_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_UserNotifications_SupportTickets_TicketId` FOREIGN KEY (`TicketId`) REFERENCES `SupportTickets` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");

    // Sample demo ratings so the UI is not empty
    if (!await db.MovieRatings.AnyAsync())
    {
        var movie1 = await db.Movies.FirstOrDefaultAsync();
        if (movie1 != null)
        {
            db.MovieRatings.AddRange(
                new MovieRating { MovieId = movie1.Id, VisitorId = "seed-visitor-a", Score = 5, CreatedAt = DateTime.UtcNow },
                new MovieRating { MovieId = movie1.Id, VisitorId = "seed-visitor-b", Score = 4, CreatedAt = DateTime.UtcNow },
                new MovieRating { MovieId = movie1.Id, VisitorId = "seed-visitor-c", Score = 5, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
    }

    var demoUsers = new[]
    {
        (Role: UserRole.Admin, Email: "admin@cinema.local", Password: "AdminPass123!", IsSubscribed: true),
        (Role: UserRole.Registered, Email: "subscriber@cinema.local", Password: "UserPass123!", IsSubscribed: true)
    };

    foreach (var demo in demoUsers)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == demo.Email);
        if (existing == null)
        {
            db.Users.Add(new User
            {
                Email = demo.Email,
                PasswordHash = PasswordHasher.HashPassword(demo.Password),
                Role = demo.Role,
                IsSubscribed = demo.IsSubscribed,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.PasswordHash = PasswordHasher.HashPassword(demo.Password);
            existing.Role = demo.Role;
            existing.IsSubscribed = demo.IsSubscribed;
        }
    }

    await db.SaveChangesAsync();

    // Seed a sample support ticket and admin answer if empty
    if (!await db.SupportTickets.AnyAsync())
    {
        var subscriber = await db.Users.FirstOrDefaultAsync(u => u.Email == "subscriber@cinema.local");
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@cinema.local");

        if (subscriber != null && admin != null)
        {
            var ticket = new SupportTicket
            {
                UserId = subscriber.Id,
                UserEmail = subscriber.Email,
                Subject = "Request for 4K Ultra HD Streaming and Audio Sync",
                Category = "Streaming & Playback",
                Priority = "High",
                Status = "InProgress",
                HasUnreadStaffReply = true,
                HasUnreadUserReply = false,
                LastMessagePreview = "Hello! We have optimized the 1080p stream and are rolling out 4K support soon.",
                LastReplyByRole = UserRole.Admin,
                CreatedAt = DateTime.UtcNow.AddHours(-6),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync();

            var msg1 = new TicketMessage
            {
                TicketId = ticket.Id,
                SenderUserId = subscriber.Id,
                SenderRole = UserRole.Registered,
                SenderEmail = subscriber.Email,
                SenderName = "subscriber",
                Message = "Hello Support team! Are you planning to add 4K streaming resolution for Cinema Pro subscribers? Also, Смотри мою любовь playback was fixed, thank you!",
                CreatedAt = DateTime.UtcNow.AddHours(-6)
            };

            var msg2 = new TicketMessage
            {
                TicketId = ticket.Id,
                SenderUserId = admin.Id,
                SenderRole = UserRole.Admin,
                SenderEmail = admin.Email,
                SenderName = "Bartigran Support",
                Message = "Hello! We have optimized the 1080p stream and are rolling out 4K support soon. Your subscription will automatically include 4K once released!",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            db.TicketMessages.AddRange(msg1, msg2);

            var notification = new UserNotification
            {
                UserId = subscriber.Id,
                TicketId = ticket.Id,
                Title = "Support Replied to Ticket #" + ticket.Id,
                ShortMessage = "Hello! We have optimized the 1080p stream and are rolling out 4K support soon.",
                IsRead = false,
                TargetRole = UserRole.Registered,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };

            db.UserNotifications.Add(notification);
            await db.SaveChangesAsync();
        }
    }
}
