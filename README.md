# CinemaApp Enterprise Streaming Platform (.NET 10 LTS)

A complete, production-ready Movie Streaming and Online Cinema platform engineered with **.NET 10 LTS (C# 14)**, **MySQL EF Core 10**, an automated background **Linux FFmpeg Transcoding Pipeline**, **AI-Powered Multilingual Content Generation** with tabbed manual overrides, **Quality-Restricted HTTP Byte-Range Video Streaming**, **Stripe Subscription Management**, and a cross-platform **.NET MAUI Android Application**.

---

## 🏛️ System Architecture

```
CinemaApp/
├── CinemaApp.slnx                     # .NET 10 Solution definition
├── Dockerfile                         # Linux Ubuntu + .NET 10 + FFmpeg multi-stage build
├── docker-compose.yml                 # Production compose (MySQL 8.4 + CinemaApp.Web)
├── CinemaApp.Core/                    # Shared Domain, EF Core 10, Services, DTOs
│   ├── Entities/                      # Language, Movie, MovieTranslation, MediaStream, User, Transaction
│   ├── Data/CinemaDbContext.cs        # MySQL EF Core 10 Context & Seeding (EN, HY, RU)
│   ├── DTOs/                          # Movie, Auth, AI, Stream, Stripe DTOs
│   ├── Services/                      # IAiContentService, IFFmpegService, ITranscodingQueue, IStripePaymentService, ITokenService
│   └── Security/                      # PasswordHasher & Cryptography
├── CinemaApp.Web/                     # ASP.NET Core 10 Web App & REST Web API
│   ├── BackgroundServices/            # VideoTranscodingWorker (Channel queue consumer)
│   ├── Controllers/Api/               # /api/v1/ (Auth, Movies, Languages, Streaming, Payments, AI)
│   ├── Controllers/                   # MVC (HomeController, AdminController, AccountController)
│   ├── Views/                         # Dark Cinema UI, Tabbed AI Editor, HTML5 Player
│   └── wwwroot/css/cinema.css         # Modern glassmorphism & dark cinema design system
└── CinemaApp.Mobile/                  # .NET MAUI Android Client (net10.0-android)
    ├── Services/                      # CinemaApiClient & AuthService
    ├── ViewModels/                    # Catalog, MovieDetail, Player, Login ViewModels
    └── Views/                         # CatalogPage, MovieDetailPage, PlayerPage (MediaElement)
```

---

## 🚀 Key Functional Capabilities

### 1. Multilingual DB Schema & Dynamic Languages
- **Database Entities**:
  - `Languages`: `Id`, `CultureCode` (e.g. `en-US`, `hy-AM`, `ru-RU`), `DisplayName`, `IsActive`, `IsDefault`.
  - `Movies`: `Id`, `ReleaseYear`, `DurationMinutes`, `CreatedAt`, `PosterUrl`, `BannerUrl`, `MasterVideoPath`.
  - `MovieTranslations`: `Id`, `MovieId`, `LanguageId`, `Title`, `Description`, `Genres`.
  - `Categories` & `CategoryTranslations`: Dynamic multilingual taxonomy.
  - `MediaStreams`: `Id`, `MovieId`, `Quality` (`360p`, `480p`, `720p`, `1080p`), `FilePath`, `FileSize`, `IsReady`.
  - `Users` & `Transactions`: Authentication, roles (`Admin`, `Registered`, `Guest`), and Stripe transaction logs.
- **Dynamic Language Admin UI**: Active languages can be viewed and added dynamically via `/Admin/Languages` or `POST /api/v1/languages`.

### 2. AI Content Generation & Manual Translation Overrides
- **Service**: `IAiContentService` / `AiContentService` integrating **Google Gemini API** (`generateContent`) and **OpenAI API** (`gpt-4o-mini`) with high-fidelity cinematic heuristics fallback.
- **Workflow**:
  1. Admin navigates to `/Admin/CreateMovie`.
  2. Enters seed title (e.g. *"Gladiator II"*, *"Արևածագ"*, *"Интерстеллар"*) and clicks **⚡ Auto-Generate Content & Translations**.
  3. AI simultaneously populates Title, Description, and Genre tags across all active language tabs (`[English]`, `[Armenian]`, `[Russian]`, etc.).
  4. The Admin retains full editing control across all tabs to manually fine-tune any translation prior to database persistence.

### 3. Automated Linux FFmpeg Transcoding Pipeline
- When master video is uploaded via `/Admin/CreateMovie`:
  - Video is saved to staging at `/var/www/cinema/media/staging/{id}.mp4`.
  - Background job is queued into `ITranscodingQueue` (`System.Threading.Channels.Channel<TranscodingJob>`).
  - `VideoTranscodingWorker` (`BackgroundService`) executes FFmpeg commands:
    * `1080p.mp4`: Bitrate ~4500k, 1920x1080, H.264 / AAC
    * `720p.mp4`: Bitrate ~2500k, 1280x720, H.264 / AAC
    * `480p.mp4`: Bitrate ~1200k, 854x480, H.264 / AAC
    * `360p.mp4`: Bitrate ~800k, 640x360, H.264 / AAC
  - Extracts poster frame at `00:00:10` to `/movies/{id}/poster.jpg`.
  - Registers all streams in `MediaStreams` and marks `IsReady = true`.

### 4. Quality-Restricted HTTP Byte-Range Streaming
- **Endpoint**: `/api/v1/stream/{movieId}/{quality}`
- **Range Support**: Fully RFC 7233 compliant `HTTP 206 Partial Content` with `Accept-Ranges: bytes` and `Content-Range`.
- **Quality Gatekeeper**:
  - Unregistered / Guest Users: Permitted up to **360p** and **480p SD**.
  - Pro Subscribers / Admins: Unlocks **720p HD** and **1080p FHD**.
  - Unauthenticated access to 720p/1080p returns `HTTP 403 Forbidden` with a JSON payload prompting the user to upgrade to Cinema Pro.

### 5. Stripe Subscription Integration
- `StripePaymentService` integrates `Stripe.net` for monthly subscriptions ($9.99/mo).
- Webhook endpoint at `/api/payments/stripe-webhook` processes `checkout.session.completed` and `payment_intent.succeeded`, automatically upgrading `User.IsSubscribed = true` and logging records in `Transactions`.

### 6. .NET MAUI Android Mobile Client
- Target Framework: `net10.0-android`.
- Uses `CommunityToolkit.Maui.MediaElement` (v10.0.0) for video playback.
- Dynamic multilingual catalog browsing (English, Armenian, Russian).
- Video resolution selector with pro restriction lock overlay.

---

## 🛠️ Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=cinema_db;User=cinema_user;Password=cinema_secure_password_2026;CharSet=utf8mb4;"
  },
  "Jwt": {
    "Key": "CinemaApp_Ultra_Secure_Secret_Key_For_Net10_Cinema_App_2026!",
    "Issuer": "CinemaApp",
    "Audience": "CinemaAppClient"
  },
  "FFmpeg": {
    "BinaryPath": "/usr/bin/ffmpeg",
    "ProbePath": "/usr/bin/ffprobe",
    "MediaRoot": "/var/www/cinema/media",
    "StagingPath": "/var/www/cinema/media/staging",
    "MoviesPath": "/var/www/cinema/media/movies"
  },
  "Stripe": {
    "PublishableKey": "pk_test_sample",
    "SecretKey": "sk_test_sample",
    "WebhookSecret": "whsec_sample"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY"
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY"
  }
}
```

---

## 🚢 Deployment Options

### Option A: Docker Deployment (Recommended for Linux)
Run with a single command to deploy MySQL 8.4 and the ASP.NET Core 10 container equipped with Linux FFmpeg:

```bash
cd CinemaApp
docker compose up --build -d
```
The web app and API will be live on `http://localhost:8080`.

### Option B: Local Development
```bash
# Build the entire solution
dotnet build CinemaApp.slnx

# Run the Web & API backend
dotnet run --project CinemaApp.Web/CinemaApp.Web.csproj
```
Navigate to:
- Web Catalog: `http://localhost:5000/`
- Admin Dashboard: `http://localhost:5000/Admin`
- Tabbed AI Movie Editor: `http://localhost:5000/Admin/CreateMovie`
- Subscription Page: `http://localhost:5000/subscription`
- REST API Documentation / Endpoints: `http://localhost:5000/api/v1/movies`

### Option C: Build .NET MAUI Android Application
```bash
dotnet build CinemaApp.Mobile/CinemaApp.Mobile.csproj -f net10.0-android
```

---

## 🔐 Seed Accounts
- **Admin**: `admin@cinema.local` / `AdminPass123!` (Role: Admin, IsSubscribed: true)
- **Subscriber**: `subscriber@cinema.local` / `UserPass123!` (Role: Registered, IsSubscribed: true)
