using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StartLine.Application.Auth;

namespace StartLine.IntegrationTests;

public class AuthTests : IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns201WithTokens()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            email = "athlete@example.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409ProblemDetails()
    {
        var request = new { email = "duplicate@example.com", password = "Password123!" };

        await _client.PostAsJsonAsync("/auth/register", request);
        var response = await _client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(body);
        var status = body.RootElement.GetProperty("status").GetInt32();
        Assert.Equal(409, status);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = "loginok@example.com";
        var password = "Password123!";

        await _client.PostAsJsonAsync("/auth/register", new { email, password });

        var response = await _client.PostAsJsonAsync("/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401ProblemDetails()
    {
        var email = "loginfail@example.com";
        await _client.PostAsJsonAsync("/auth/register", new { email, password = "CorrectPassword!" });

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(body);
        var status = body.RootElement.GetProperty("status").GetInt32();
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            email = "nobody@example.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Token Refresh ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithNewTokens()
    {
        var email = "refresh@example.com";
        var password = "Password123!";

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", new { email, password });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerBody);

        var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = registerBody.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshBody);
        Assert.False(string.IsNullOrWhiteSpace(refreshBody.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshBody.RefreshToken));
        // New tokens should be different from the originals
        Assert.NotEqual(registerBody.RefreshToken, refreshBody.RefreshToken);
    }

    [Fact]
    public async Task Refresh_RevokedToken_Returns401()
    {
        var email = "refreshrevoked@example.com";
        var password = "Password123!";

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", new { email, password });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerBody);

        // Use the refresh token once (rotates it)
        await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = registerBody.RefreshToken });

        // Try to use the old refresh token again
        var response = await _client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = registerBody.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = "this-is-not-a-valid-token"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Rate Limiting ─────────────────────────────────────────────────────────

    // Rate limiting is tested separately in RateLimitTests class, which uses
    // a dedicated factory with a low permit limit (3 per minute).
    // ── JWT / Role in claims ─────────────────────────────────────────────────

    [Fact]
    public async Task AccessToken_ContainsAthleteRole()
    {
        var email = "rolecheck@example.com";
        var password = "Password123!";

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", new { email, password });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerBody);

        // Decode JWT payload (without validation - just check claims present)
        var parts = registerBody.AccessToken.Split('.');
        Assert.Equal(3, parts.Length);

        var paddedPayload = parts[1].PadRight((parts[1].Length + 3) & ~3, '=');
        var payloadBytes = Convert.FromBase64String(paddedPayload.Replace('-', '+').Replace('_', '/'));
        var payload = JsonDocument.Parse(payloadBytes);

        var hasRole = payload.RootElement.TryGetProperty("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", out var roleClaim)
            || payload.RootElement.TryGetProperty("role", out roleClaim);

        Assert.True(hasRole);
        Assert.Equal("Athlete", roleClaim.GetString());
    }

    [Fact]
    public async Task ProtectedEndpoint_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_ValidToken_Returns200()
    {
        var email = "me@example.com";
        var password = "Password123!";

        var registerResponse = await _client.PostAsJsonAsync("/auth/register", new { email, password });
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(registerBody);

        var authenticatedClient = _factory.Factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", registerBody.AccessToken);

        var response = await authenticatedClient.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
