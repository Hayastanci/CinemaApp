using CinemaApp.Core.Entities;
using CinemaApp.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace CinemaApp.Core.Data;

public class CinemaDbContext : DbContext
{
    public CinemaDbContext(DbContextOptions<CinemaDbContext> options) : base(options)
    {
    }

    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<MovieTranslation> MovieTranslations => Set<MovieTranslation>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryTranslation> CategoryTranslations => Set<CategoryTranslation>();
    public DbSet<MediaStream> MediaStreams => Set<MediaStream>();
    public DbSet<MovieRating> MovieRatings => Set<MovieRating>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Language
        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CultureCode).IsUnique();
            entity.Property(e => e.CultureCode).HasMaxLength(10).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(50).IsRequired();
        });

        // Movie
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PosterUrl).HasMaxLength(500);
            entity.Property(e => e.BannerUrl).HasMaxLength(500);
            entity.Property(e => e.MasterVideoPath).HasMaxLength(500);

            entity.HasMany(e => e.MovieTranslations)
                  .WithOne(e => e.Movie)
                  .HasForeignKey(e => e.MovieId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.MediaStreams)
                  .WithOne(e => e.Movie)
                  .HasForeignKey(e => e.MovieId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Categories)
                  .WithMany(e => e.Movies)
                  .UsingEntity(j => j.ToTable("MovieCategories"));
        });

        // MovieTranslation
        modelBuilder.Entity<MovieTranslation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MovieId, e.LanguageId }).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(250).IsRequired();
            entity.Property(e => e.Description).HasColumnType("TEXT");
            entity.Property(e => e.Genres).HasMaxLength(250);

            entity.HasOne(e => e.Language)
                  .WithMany(e => e.MovieTranslations)
                  .HasForeignKey(e => e.LanguageId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Slug).HasMaxLength(100).IsRequired();

            entity.HasMany(e => e.CategoryTranslations)
                  .WithOne(e => e.Category)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CategoryTranslation
        modelBuilder.Entity<CategoryTranslation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CategoryId, e.LanguageId }).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasOne(e => e.Language)
                  .WithMany(e => e.CategoryTranslations)
                  .HasForeignKey(e => e.LanguageId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // MediaStream
        modelBuilder.Entity<MediaStream>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MovieId, e.Quality });
            entity.Property(e => e.Quality).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
        });

        // MovieRating
        modelBuilder.Entity<MovieRating>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MovieId, e.VisitorId }).IsUnique();
            entity.Property(e => e.VisitorId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Score).IsRequired();

            entity.HasOne(e => e.Movie)
                  .WithMany(e => e.MovieRatings)
                  .HasForeignKey(e => e.MovieId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(30);

            entity.HasMany(e => e.Transactions)
                  .WithOne(e => e.User)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StripeSessionId).HasMaxLength(200);
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(200);
            entity.Property(e => e.Currency).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });

        // SupportTicket
        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Priority).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.UserEmail).HasMaxLength(150).IsRequired();
            entity.Property(e => e.LastMessagePreview).HasMaxLength(500);
            entity.Property(e => e.LastReplyByRole).HasConversion<string>().HasMaxLength(30);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Messages)
                  .WithOne(e => e.Ticket)
                  .HasForeignKey(e => e.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TicketMessage
        modelBuilder.Entity<TicketMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SenderEmail).HasMaxLength(150).IsRequired();
            entity.Property(e => e.SenderName).HasMaxLength(100);
            entity.Property(e => e.SenderRole).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Message).HasColumnType("TEXT").IsRequired();

            entity.HasOne(e => e.SenderUser)
                  .WithMany()
                  .HasForeignKey(e => e.SenderUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // UserNotification
        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ShortMessage).HasMaxLength(500).IsRequired();
            entity.Property(e => e.TargetRole).HasConversion<string>().HasMaxLength(30);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Ticket)
                  .WithMany()
                  .HasForeignKey(e => e.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed Initial Data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // 1. Languages
        var langEn = new Language { Id = 1, CultureCode = "en-US", DisplayName = "English", IsActive = true, IsDefault = true };
        var langHy = new Language { Id = 2, CultureCode = "hy-AM", DisplayName = "Հայերեն", IsActive = true, IsDefault = false };
        var langRu = new Language { Id = 3, CultureCode = "ru-RU", DisplayName = "Русский", IsActive = true, IsDefault = false };
        modelBuilder.Entity<Language>().HasData(langEn, langHy, langRu);

        // 2. Users (Admin + Demo Registered)
        var adminUser = new User
        {
            Id = 1,
            Email = "admin@cinema.local",
            PasswordHash = PasswordHasher.HashPassword("AdminPass123!"),
            Role = UserRole.Admin,
            IsSubscribed = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var regularUser = new User
        {
            Id = 2,
            Email = "subscriber@cinema.local",
            PasswordHash = PasswordHasher.HashPassword("UserPass123!"),
            Role = UserRole.Registered,
            IsSubscribed = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        modelBuilder.Entity<User>().HasData(adminUser, regularUser);

        // 3. Categories
        var catAction = new Category { Id = 1, Slug = "action" };
        var catSciFi = new Category { Id = 2, Slug = "sci-fi" };
        var catDrama = new Category { Id = 3, Slug = "drama" };
        modelBuilder.Entity<Category>().HasData(catAction, catSciFi, catDrama);

        // 4. Category Translations
        modelBuilder.Entity<CategoryTranslation>().HasData(
            new CategoryTranslation { Id = 1, CategoryId = 1, LanguageId = 1, Name = "Action", Description = "High-octane excitement and battles" },
            new CategoryTranslation { Id = 2, CategoryId = 1, LanguageId = 2, Name = "Մարտաֆիլմ", Description = "Լարված գործողություններ և մարտեր" },
            new CategoryTranslation { Id = 3, CategoryId = 1, LanguageId = 3, Name = "Боевик", Description = "Динамичные приключения и сражения" },

            new CategoryTranslation { Id = 4, CategoryId = 2, LanguageId = 1, Name = "Sci-Fi", Description = "Futuristic science and space adventures" },
            new CategoryTranslation { Id = 5, CategoryId = 2, LanguageId = 2, Name = "Գիտաֆանտաստիկա", Description = "Ապագայի տեխնոլոգիաներ և տիեզերական ոդիսական" },
            new CategoryTranslation { Id = 6, CategoryId = 2, LanguageId = 3, Name = "Фантастика", Description = "Технологии будущего и космос" },

            new CategoryTranslation { Id = 7, CategoryId = 3, LanguageId = 1, Name = "Drama", Description = "Deep emotional storytelling" },
            new CategoryTranslation { Id = 8, CategoryId = 3, LanguageId = 2, Name = "Դրամա", Description = "Խորիմաստ և հուզիչ պատմություններ" },
            new CategoryTranslation { Id = 9, CategoryId = 3, LanguageId = 3, Name = "Драма", Description = "Глубокие эмоциональные сюжеты" }
        );

        // 5. Sample Movie
        var movie1 = new Movie
        {
            Id = 1,
            ReleaseYear = 2025,
            DurationMinutes = 135,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PosterUrl = "/media/movies/1/poster.jpg",
            BannerUrl = "/media/movies/1/poster.jpg",
            MasterVideoPath = "/movies/1/master.mp4"
        };
        modelBuilder.Entity<Movie>().HasData(movie1);

        // 6. Movie Translations
        modelBuilder.Entity<MovieTranslation>().HasData(
            new MovieTranslation
            {
                Id = 1,
                MovieId = 1,
                LanguageId = 1,
                Title = "Chronicles of Eternity",
                Description = "In a distant future where time itself is fractured, a resilient pilot embarks on an odyssey across the cosmos to restore the rhythm of history.",
                Genres = "Sci-Fi, Action, Adventure"
            },
            new MovieTranslation
            {
                Id = 2,
                MovieId = 1,
                LanguageId = 2,
                Title = "Հավերժության Ժամանակագրություն",
                Description = "Հեռավոր ապագայում, երբ ժամանակն ինքնին մասնատված է, խիզախ օդաչուն մեկնում է տիեզերական ոդիսականի՝ վերականգնելու պատմության ներդաշնակությունը։",
                Genres = "Գիտաֆանտաստիկա, Մարտաֆիլմ, Արկածային"
            },
            new MovieTranslation
            {
                Id = 3,
                MovieId = 1,
                LanguageId = 3,
                Title = "Хроники Вечности",
                Description = "В далеком будущем, где само время раскололось на осколки, бесстрашный пилот отправляется в космическую одиссею, чтобы восстановить ткань истории.",
                Genres = "Фантастика, Боевик, Приключения"
            }
        );

        // 7. Media Streams for Movie 1
        modelBuilder.Entity<MediaStream>().HasData(
            new MediaStream { Id = 1, MovieId = 1, Quality = "360p", FilePath = "/movies/1/360p.mp4", FileSize = 18000000, IsReady = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new MediaStream { Id = 2, MovieId = 1, Quality = "480p", FilePath = "/movies/1/480p.mp4", FileSize = 35000000, IsReady = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new MediaStream { Id = 3, MovieId = 1, Quality = "720p", FilePath = "/movies/1/720p.mp4", FileSize = 75000000, IsReady = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new MediaStream { Id = 4, MovieId = 1, Quality = "1080p", FilePath = "/movies/1/1080p.mp4", FileSize = 140000000, IsReady = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // 8. Sample Movie Ratings (Visitor-driven demo votes)
        modelBuilder.Entity<MovieRating>().HasData(
            new MovieRating { Id = 1, MovieId = 1, VisitorId = "seed-visitor-a", Score = 5, CreatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            new MovieRating { Id = 2, MovieId = 1, VisitorId = "seed-visitor-b", Score = 4, CreatedAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc) },
            new MovieRating { Id = 3, MovieId = 1, VisitorId = "seed-visitor-c", Score = 5, CreatedAt = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
