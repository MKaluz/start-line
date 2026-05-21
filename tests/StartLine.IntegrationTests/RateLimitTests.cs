using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using StartLine.Infrastructure.Persistence;

namespace StartLine.IntegrationTests;

/// <summary>
/// Separate test class for rate limiting, using its own factory with a low permit limit.
/// Kept separate from AuthTests to avoid contaminating the shared rate limit bucket.
/// </summary>
public class RateLimitTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("startline_test")
        .WithUsername("startline")
        .WithPassword("startline")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
                builder.UseSetting("Otel:Endpoint", "http://localhost:4317");
                builder.UseSetting("Jwt:SecretKey", "test-secret-key-for-integration-tests-only-32chars!");
                builder.UseSetting("Jwt:Issuer", "StartLine");
                builder.UseSetting("Jwt:Audience", "StartLine");
                builder.UseSetting("Jwt:AccessTokenMinutes", "15");
                builder.UseSetting("Jwt:RefreshTokenDays", "7");
                // Low limit to make rate limiting trigger quickly in tests
                builder.UseSetting("RateLimit:AuthPermitLimit", "3");
                builder.UseSetting("RateLimit:AuthWindowSeconds", "60");
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Login_ExceedRateLimit_Returns429()
    {
        // With AuthPermitLimit = 3, the 4th request should be rate-limited.
        var request = new { email = "ratelimit@example.com", password = "WrongPassword!" };

        HttpStatusCode? lastStatusCode = null;
        for (var i = 0; i < 4; i++)
        {
            var response = await _client.PostAsJsonAsync("/auth/login", request);
            lastStatusCode = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatusCode);
    }
}
