using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using StartLine.Infrastructure.Persistence;

namespace StartLine.IntegrationTests;

public sealed class IntegrationTestFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("startline_test")
        .WithUsername("startline")
        .WithPassword("startline")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
                builder.UseSetting("Otel:Endpoint", "http://localhost:4317");
                builder.UseSetting("Jwt:SecretKey", "test-secret-key-for-integration-tests-only-32chars!");
                builder.UseSetting("Jwt:Issuer", "StartLine");
                builder.UseSetting("Jwt:Audience", "StartLine");
                builder.UseSetting("Jwt:AccessTokenMinutes", "15");
                builder.UseSetting("Jwt:RefreshTokenDays", "7");
                builder.UseSetting("RateLimit:AuthPermitLimit", "1000");
                builder.UseSetting("RateLimit:AuthWindowSeconds", "60");
            });

        // Apply migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
