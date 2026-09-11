# ==========================================================
# CinemaApp Enterprise Dockerfile (.NET 10 LTS + Linux FFmpeg)
# ==========================================================

# Stage 1: Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for layer caching
COPY ["CinemaApp.Core/CinemaApp.Core.csproj", "CinemaApp.Core/"]
COPY ["CinemaApp.Web/CinemaApp.Web.csproj", "CinemaApp.Web/"]

# Restore NuGet dependencies
RUN dotnet restore "CinemaApp.Web/CinemaApp.Web.csproj"

# Copy full source tree
COPY CinemaApp.Core/ CinemaApp.Core/
COPY CinemaApp.Web/ CinemaApp.Web/

# Build and publish release binaries
WORKDIR "/src/CinemaApp.Web"
RUN dotnet publish "CinemaApp.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Environment with Linux FFmpeg
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install Linux FFmpeg and required media utilities
RUN apt-get update && apt-get install -y --no-install-recommends \
    ffmpeg \
    ca-certificates \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# Create persistent media staging and transcoded video directories
RUN mkdir -p /var/www/cinema/media/staging /var/www/cinema/media/movies \
    && chmod -R 777 /var/www/cinema/media

# Copy compiled application artifacts
COPY --from=build /app/publish .

# Expose Kestrel HTTP port
EXPOSE 8080

# Environment defaults for Linux deployment
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    FFmpeg__BinaryPath=/usr/bin/ffmpeg \
    FFmpeg__ProbePath=/usr/bin/ffprobe \
    FFmpeg__MediaRoot=/var/www/cinema/media \
    FFmpeg__StagingPath=/var/www/cinema/media/staging \
    FFmpeg__MoviesPath=/var/www/cinema/media/movies

# Define storage volume for media persistence
VOLUME ["/var/www/cinema/media"]

ENTRYPOINT ["dotnet", "CinemaApp.Web.dll"]
