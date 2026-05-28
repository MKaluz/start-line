using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StartLine.Application.Auth;
using StartLine.Application.Events;
using StartLine.Application.Registrations;
using StartLine.Infrastructure.Persistence;

namespace StartLine.IntegrationTests;

public class ReservationExpiryTests : IAsyncLifetime
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> RegisterAthleteTokenAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", new { email, password });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.AccessToken;
    }

    private async Task<string> RegisterOrganizerTokenAsync(string email, string password)
    {
        await _client.PostAsJsonAsync("/auth/register", new { email, password });

        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Users\" SET \"Role\" = 1 WHERE \"Email\" = {0}",
            email.ToLowerInvariant());

        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new { email, password });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return loginBody!.AccessToken;
    }

    private HttpClient CreateAuthenticatedClient(string token)
    {
        var client = _factory.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid eventId, Guid raceId)> CreateEventWithRaceAsync(
        string orgToken,
        int capacity = 5)
    {
        using var org = CreateAuthenticatedClient(orgToken);

        var eventResponse = await org.PostAsJsonAsync("/events", new
        {
            name = "Expiry Test Event",
            date = "2099-06-01",
            location = "Prague",
            description = (string?)null
        });
        var eventBody = await eventResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        var raceResponse = await org.PostAsJsonAsync($"/events/{eventBody!.Id}/races", new
        {
            name = "5K Run",
            capacity,
            basePrice = 50.00m,
            earlyBirdPrice = (decimal?)null,
            earlyBirdDeadline = (string?)null,
            minAge = (int?)null,
            maxAge = (int?)null,
            allowedGender = (string?)null
        });
        var race = await raceResponse.Content.ReadFromJsonAsync<RaceResponse>();

        return (eventBody.Id, race!.Id);
    }

    private static object DefaultRegistrationBody(Guid raceId) => new
    {
        raceId,
        firstName = "Jane",
        lastName = "Smith",
        email = "jane.smith@example.com",
        dateOfBirth = "1990-05-15",
        gender = 0,
        club = (string?)null,
        phone = (string?)null
    };

    private async Task ExpireRegistrationInDbAsync(Guid registrationId)
    {
        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Registrations\" SET \"ReservationExpiresAt\" = {0} WHERE \"Id\" = {1}",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            registrationId);
    }

    private async Task<IRegistrationRepository> GetRepositoryAsync()
    {
        var scope = _factory.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpireReservations_SingleExpiredReservation_TransitionsToExpired()
    {
        var orgToken = await RegisterOrganizerTokenAsync("expiry.org1@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("expiry.ath1@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);
        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        await ExpireRegistrationInDbAsync(reg!.Id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var count = await repo.ExpireReservationsAsync();

        Assert.Equal(1, count);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Registrations.FindAsync(reg.Id);
        Assert.Equal("Expired", updated!.Status.ToString());
    }

    [Fact]
    public async Task ExpireReservations_WritesOutboxMessagePerExpiry()
    {
        var orgToken = await RegisterOrganizerTokenAsync("expiry.org2@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("expiry.ath2@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);
        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        await ExpireRegistrationInDbAsync(reg!.Id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        await repo.ExpireReservationsAsync();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outbox = await db.OutboxMessages
            .Where(o => o.Type == "ReservationExpiredEmail")
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Contains(reg.Id.ToString(), outbox[0].Payload);
        Assert.Null(outbox[0].ProcessedAt);
    }

    [Fact]
    public async Task ExpireReservations_BatchExpiry_ExpireAllOverdueReservations()
    {
        var orgToken = await RegisterOrganizerTokenAsync("expiry.org3@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 10);

        var registrationIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var athleteToken = await RegisterAthleteTokenAsync($"expiry.ath3.{i}@example.com", "Password123!");
            using var client = CreateAuthenticatedClient(athleteToken);
            var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
            var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
            registrationIds.Add(reg!.Id);
        }

        // Expire all 3
        foreach (var id in registrationIds)
            await ExpireRegistrationInDbAsync(id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var count = await repo.ExpireReservationsAsync();

        Assert.Equal(3, count);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxCount = await db.OutboxMessages
            .CountAsync(o => o.Type == "ReservationExpiredEmail");
        Assert.Equal(3, outboxCount);
    }

    [Fact]
    public async Task ExpireReservations_Idempotency_DoesNotDoubleExpire()
    {
        var orgToken = await RegisterOrganizerTokenAsync("expiry.org4@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("expiry.ath4@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);
        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        await ExpireRegistrationInDbAsync(reg!.Id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();

        var firstCount = await repo.ExpireReservationsAsync();
        var secondCount = await repo.ExpireReservationsAsync();

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxCount = await db.OutboxMessages
            .CountAsync(o => o.Type == "ReservationExpiredEmail");
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task ExpireReservations_CapacityRestoredAfterExpiry_AllowsNewRegistration()
    {
        var orgToken = await RegisterOrganizerTokenAsync("expiry.org5@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the single slot
        var ath1Token = await RegisterAthleteTokenAsync("expiry.ath5a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var regResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Verify capacity is full — second athlete should be rejected
        var ath2Token = await RegisterAthleteTokenAsync("expiry.ath5b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var rejectResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(System.Net.HttpStatusCode.Conflict, rejectResponse.StatusCode);

        // Expire the first registration
        await ExpireRegistrationInDbAsync(reg!.Id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        await repo.ExpireReservationsAsync();

        // Now the second athlete should be able to register
        var successResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(System.Net.HttpStatusCode.Created, successResponse.StatusCode);
    }

    [Fact]
    public async Task ExpireReservations_NonExpiredReservation_IsNotAffected()
    {
        var orgToken = await RegisterOrganizerTokenAsync("expiry.org6@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("expiry.ath6@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);
        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Do NOT expire this registration — ReservationExpiresAt is in the future
        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        var count = await repo.ExpireReservationsAsync();

        Assert.Equal(0, count);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Registrations.FindAsync(reg!.Id);
        Assert.Equal("Reserved", updated!.Status.ToString());
    }
}
