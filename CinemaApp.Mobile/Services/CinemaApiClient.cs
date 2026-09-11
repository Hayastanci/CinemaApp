using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CinemaApp.Core.DTOs;

namespace CinemaApp.Mobile.Services;

public class CinemaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private readonly object _lock = new();
    private bool _verified;
    private string _baseUrl = ApiConfig.ServerUrl.TrimEnd('/') + "/";

    public CinemaApiClient(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
        Debug.WriteLine($"[Cinema] ctor base = {_baseUrl}");
    }

    /// <summary>Current base URL (ends with a trailing slash).</summary>
    public string BaseUrl => _baseUrl;

    /// <summary>
    /// Dynamically tests each candidate URL using a safe platform-configured network handler, 
    /// pinning the first one that successfully responds to the API challenge.
    /// </summary>
    public async Task EnsureEndpointAsync(CancellationToken ct = default)
    {
        if (_verified)
        {
            Debug.WriteLine($"[Cinema] already verified: {_httpClient.BaseAddress}");
            return;
        }

        Debug.WriteLine($"[Cinema] dynamically probing {ApiConfig.Candidates.Length} endpoints...");

        // Create a platform-safe handler to prevent Android cleartext policy crashes during discovery
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif

        using var probe = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2.5) };

        foreach (var candidate in ApiConfig.Candidates)
        {
            var baseUrl = candidate.TrimEnd('/') + "/";
            try
            {
                var targetProbeUrl = baseUrl + "api/v1/languages?activeOnly=true";
                Debug.WriteLine($"[Cinema] probing: {targetProbeUrl}");

                using var response = await probe.GetAsync(targetProbeUrl, ct);
                Debug.WriteLine($"[Cinema] response from {baseUrl} -> Status: {(int)response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
lock (_lock)
                    {
                        _baseUrl = baseUrl;
                        _verified = true;
                    }
                    Debug.WriteLine($"[Cinema] pinned endpoint: {baseUrl}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cinema] Dynamic probe to {baseUrl} REJECTED: {ex.GetType().Name} -> {ex.Message}");
            }
        }

        lock (_lock)
        {
            // Fallback strategy if all probes fail: let the default Candidate address register so execution can trace out the network path errors
            _httpClient.BaseAddress = new Uri(ApiConfig.ServerUrl.TrimEnd('/') + "/");
            _verified = true;
        }
        Debug.WriteLine($"[Cinema] Warning: All network candidate probes timed out or failed. Falling back to default: {_httpClient.BaseAddress}");
    }

    /// <summary>
    /// Make a URL absolute against the current base URL. The API may return absolute URLs
    /// pointing at its own published origin (e.g. https://localhost:7078) which are unreachable
    /// from a device, so loopback/hosted API URLs are rebased onto the pinned endpoint. Fully
    /// external URLs (e.g. the Unsplash fallback poster) are left untouched.
    /// </summary>
    private string Abs(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url ?? string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var isLocalHost = uri.Host is "localhost" or "127.0.0.1" or "::1";
            var pointsAtPinnedBase = url.StartsWith(_baseUrl, StringComparison.OrdinalIgnoreCase);
            var isHostedMedia = uri.PathAndQuery.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) ||
                                uri.PathAndQuery.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
                                uri.PathAndQuery.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

            if (isLocalHost || pointsAtPinnedBase || isHostedMedia)
            {
                return _baseUrl.TrimEnd('/') + uri.PathAndQuery;
            }

            return url;
        }

        return _baseUrl.TrimEnd('/') + (url.StartsWith('/') ? url : "/" + url);
    }

    private void ApplyAuthHeader()
    {
        if (!string.IsNullOrEmpty(_authService.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authService.Token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    private void PatchSummary(MovieSummaryDto dto)
    {
        dto.PosterUrl = Abs(dto.PosterUrl);
        dto.BannerUrl = Abs(dto.BannerUrl);
        for (var i = 0; i < dto.AvailableQualities.Count; i++)
        {
            var q = dto.AvailableQualities[i];
            if (!string.IsNullOrWhiteSpace(q))
            {
                dto.AvailableQualities[i] = q.Replace("\uFEFF", "", StringComparison.Ordinal).Trim();
            }
        }
    }

    public async Task<List<LanguageDto>> GetLanguagesAsync()
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<List<LanguageDto>>(_baseUrl + "api/v1/languages?activeOnly=true");
            return res ?? new List<LanguageDto>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] languages request FAILED: {ex.Message}. Falling back to defaults.");
            // Offline default fallback
            return new List<LanguageDto>
            {
                new() { Id = 1, CultureCode = "en-US", DisplayName = "English", IsActive = true, IsDefault = true },
                new() { Id = 2, CultureCode = "hy-AM", DisplayName = "Հայերեն", IsActive = true, IsDefault = false },
                new() { Id = 3, CultureCode = "ru-RU", DisplayName = "Русский", IsActive = true, IsDefault = false }
            };
        }
    }

    public async Task<List<MovieSummaryDto>> GetMoviesAsync(string lang = "en-US", int? categoryId = null, string? search = null)
    {
        try
        {
            var url = $"api/v1/movies?lang={lang}";
            if (categoryId.HasValue) url += $"&categoryId={categoryId.Value}";
            if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";

            var res = await _httpClient.GetFromJsonAsync<List<MovieSummaryDto>>(_baseUrl + url);
            if (res == null) return new List<MovieSummaryDto>();

            foreach (var m in res) PatchSummary(m);
            Debug.WriteLine($"[Cinema] movies loaded: {res.Count} via {_baseUrl}");
            return res;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] movies FAILED: {ex.GetType().Name}: {ex.Message}");
            return new List<MovieSummaryDto>();
        }
    }

    public async Task<MovieDetailDto?> GetMovieByIdAsync(int id, string lang = "en-US")
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<MovieDetailDto>(_baseUrl + $"api/v1/movies/{id}?lang={lang}");
            if (res != null)
            {
                PatchSummary(res);
                foreach (var s in res.Streams)
                {
                    s.StreamUrl = Abs(s.StreamUrl);
                }
            }
            return res;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] movie details FAILED for ID {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(string lang = "en-US")
    {
        try
        {
            var res = await _httpClient.GetFromJsonAsync<List<CategoryDto>>(_baseUrl + $"api/v1/categories?lang={lang}");
            return res ?? new List<CategoryDto>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] categories request FAILED: {ex.Message}");
            return new List<CategoryDto>();
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.PostAsJsonAsync(_baseUrl + "api/v1/auth/login", new LoginRequestDto
            {
                Email = email,
                Password = password
            });

            if (response.IsSuccessStatusCode)
            {
                var authRes = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
                if (authRes != null)
                {
                    _authService.SetAuth(authRes.Token, authRes.User.Email, authRes.User.IsSubscribed, authRes.User.Role, authRes.User.Id);
                    Debug.WriteLine("[Cinema] login OK");
                    return true;
                }
            }
            Debug.WriteLine($"[Cinema] login HTTP {(int)response.StatusCode}");
            return false;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[Cinema] login network error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] login failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public string GetStreamUrl(int movieId, string quality)
    {
        var tokenParam = !string.IsNullOrEmpty(_authService.Token) ? $"?token={_authService.Token}" : "";
        return $"{BaseUrl}api/v1/stream/{movieId}/{quality}{tokenParam}";
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Support Ticket & Notification API Methods
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task<List<SupportTicketSummaryDto>> GetSupportTicketsAsync(bool all = false)
    {
        try
        {
            await EnsureEndpointAsync();
            ApplyAuthHeader();
            var url = _baseUrl + $"api/v1/support/tickets?all={all}";
            var res = await _httpClient.GetFromJsonAsync<List<SupportTicketSummaryDto>>(url);
            return res ?? new List<SupportTicketSummaryDto>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] get support tickets failed: {ex.Message}");
            return new List<SupportTicketSummaryDto>();
        }
    }

    public async Task<SupportTicketDetailDto?> GetTicketDetailsAsync(int ticketId)
    {
        try
        {
            await EnsureEndpointAsync();
            ApplyAuthHeader();
            var url = _baseUrl + $"api/v1/support/tickets/{ticketId}";
            return await _httpClient.GetFromJsonAsync<SupportTicketDetailDto>(url);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] get ticket details failed: {ex.Message}");
            return null;
        }
    }

    public async Task<SupportTicketSummaryDto?> CreateTicketAsync(CreateTicketDto dto)
    {
        try
        {
            await EnsureEndpointAsync();
            ApplyAuthHeader();
            var url = _baseUrl + "api/v1/support/tickets";
            var response = await _httpClient.PostAsJsonAsync(url, dto);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SupportTicketSummaryDto>();
            }
            Debug.WriteLine($"[Cinema] create ticket error: HTTP {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] create ticket failed: {ex.Message}");
            return null;
        }
    }

    public async Task<TicketMessageDto?> SendTicketReplyAsync(int ticketId, string message)
    {
        try
        {
            await EnsureEndpointAsync();
            ApplyAuthHeader();
            var url = _baseUrl + $"api/v1/support/tickets/{ticketId}/reply";
            var response = await _httpClient.PostAsJsonAsync(url, new AddTicketReplyDto { Message = message });
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TicketMessageDto>();
            }
            Debug.WriteLine($"[Cinema] send reply error: HTTP {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] send reply failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateTicketStatusAsync(int ticketId, string status)
    {
        try
        {
            await EnsureEndpointAsync();
            ApplyAuthHeader();
            var url = _baseUrl + $"api/v1/support/tickets/{ticketId}/status";
            var response = await _httpClient.PutAsJsonAsync(url, new UpdateTicketStatusDto { Status = status });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] update ticket status failed: {ex.Message}");
            return false;
        }
    }

    public async Task<NotificationSummaryDto?> GetNotificationSummaryAsync()
    {
        try
        {
            await EnsureEndpointAsync();
            ApplyAuthHeader();
            var url = _baseUrl + "api/v1/support/notifications";
            return await _httpClient.GetFromJsonAsync<NotificationSummaryDto>(url);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] get notifications failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> MarkNotificationAsReadAsync(int notificationId)
    {
        try
        {
            await EnsureEndpointAsync();
            ApplyAuthHeader();
            var url = _baseUrl + $"api/v1/support/notifications/{notificationId}/read";
            var response = await _httpClient.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Cinema] mark notification read failed: {ex.Message}");
            return false;
        }
    }
}
