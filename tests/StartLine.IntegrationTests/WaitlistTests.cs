using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StartLine.Application.Auth;
using StartLine.Application.Events;
using StartLine.Application.Registrations;
using StartLine.Infrastructure.Persistence;

namespace StartLine.IntegrationTests;

public class WaitlistTests : IAsyncLifetime
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
        int capacity = 1)
    {
        using var org = CreateAuthenticatedClient(orgToken);

        var eventResponse = await org.PostAsJsonAsync("/events", new
        {
            name = "Waitlist Test Event",
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

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WhenRaceFull_Returns202WithWaitlistPosition()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org1@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the single slot
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath1a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var firstResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Second athlete should join the waitlist
        var ath2Token = await RegisterAthleteTokenAsync("wl.ath1b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var waitlistResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(HttpStatusCode.Accepted, waitlistResponse.StatusCode);

        var waitlistEntry = await waitlistResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(waitlistEntry);
        Assert.Equal("Waitlisted", waitlistEntry.Status);
        Assert.Equal(1, waitlistEntry.QueuePosition);
    }

    [Fact]
    public async Task GetRegistration_WaitlistEntry_ReturnsQueuePosition()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org2@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath2a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        // Join waitlist
        var ath2Token = await RegisterAthleteTokenAsync("wl.ath2b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var waitlistResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var waitlistEntry = await waitlistResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // GET /registrations/{id} should return the queue position
        var getResponse = await client2.GetAsync($"/registrations/{waitlistEntry!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var body = await getResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Waitlisted", body.Status);
        Assert.Equal(1, body.QueuePosition);
    }

    [Fact]
    public async Task QueuePosition_MultipleWaitlistEntries_AssignedInOrder()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org3@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath3a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        // Add three athletes to the waitlist
        var positions = new List<int?>();
        for (var i = 0; i < 3; i++)
        {
            var token = await RegisterAthleteTokenAsync($"wl.ath3b{i}@example.com", "Password123!");
            using var client = CreateAuthenticatedClient(token);
            var response = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
            var entry = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
            positions.Add(entry!.QueuePosition);
        }

        Assert.Equal(new List<int?> { 1, 2, 3 }, positions);
    }

    [Fact]
    public async Task PromoteFromWaitlist_OnExpiry_PromotesLowestPosition()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org4@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath4a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var firstRegResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var firstReg = await firstRegResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Add two athletes to the waitlist
        var ath2Token = await RegisterAthleteTokenAsync("wl.ath4b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var wl1Response = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wl1 = await wl1Response.Content.ReadFromJsonAsync<RegistrationResponse>();

        var ath3Token = await RegisterAthleteTokenAsync("wl.ath4c@example.com", "Password123!");
        using var client3 = CreateAuthenticatedClient(ath3Token);
        var wl2Response = await client3.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wl2 = await wl2Response.Content.ReadFromJsonAsync<RegistrationResponse>();

        Assert.Equal(1, wl1!.QueuePosition);
        Assert.Equal(2, wl2!.QueuePosition);

        // Expire the first registration
        await ExpireRegistrationInDbAsync(firstReg!.Id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        await repo.ExpireReservationsAsync();

        // First waitlist entry (position 1) should be promoted to Reserved
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var promotedEntry = await db.Registrations.FindAsync(wl1.Id);
        Assert.Equal("Reserved", promotedEntry!.Status.ToString());
        Assert.Null(promotedEntry.QueuePosition);
        Assert.True(promotedEntry.ReservationExpiresAt > DateTimeOffset.UtcNow);

        // Second waitlist entry (position 2) should still be Waitlisted
        var stillWaiting = await db.Registrations.FindAsync(wl2.Id);
        Assert.Equal("Waitlisted", stillWaiting!.Status.ToString());
        Assert.Equal(2, stillWaiting.QueuePosition);
    }

    [Fact]
    public async Task PromoteFromWaitlist_OnExpiry_WritesOutboxMessage()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org5@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath5a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var firstRegResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var firstReg = await firstRegResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Add to waitlist
        var ath2Token = await RegisterAthleteTokenAsync("wl.ath5b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var wlResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wlEntry = await wlResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Expire and promote
        await ExpireRegistrationInDbAsync(firstReg!.Id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        await repo.ExpireReservationsAsync();

        // Verify WaitlistPromotedEmail outbox message was written
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var promotionMsg = await db.OutboxMessages
            .FirstOrDefaultAsync(o => o.Type == "WaitlistPromotedEmail");

        Assert.NotNull(promotionMsg);
        Assert.Contains(wlEntry!.Id.ToString(), promotionMsg.Payload);
        Assert.Null(promotionMsg.ProcessedAt);
    }

    [Fact]
    public async Task NoPromotion_WhenWaitlistEmpty_AfterExpiry()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org6@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill and then expire the slot — no waitlist entries
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath6a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var regResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        await ExpireRegistrationInDbAsync(reg!.Id);

        using var scope = _factory.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRegistrationRepository>();
        await repo.ExpireReservationsAsync();

        // No WaitlistPromotedEmail should be written
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var promotionCount = await db.OutboxMessages
            .CountAsync(o => o.Type == "WaitlistPromotedEmail");
        Assert.Equal(0, promotionCount);

        // Capacity is now freed — a new athlete can register normally
        var ath2Token = await RegisterAthleteTokenAsync("wl.ath6b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var newRegResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(HttpStatusCode.Created, newRegResponse.StatusCode);
    }

    [Fact]
    public async Task PromoteFromWaitlist_OnCancellation_PromotesLowestPosition()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org7@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath7a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var firstRegResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var firstReg = await firstRegResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Add to waitlist
        var ath2Token = await RegisterAthleteTokenAsync("wl.ath7b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var wlResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wlEntry = await wlResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        Assert.Equal("Waitlisted", wlEntry!.Status);
        Assert.Equal(1, wlEntry.QueuePosition);

        // Cancel the first registration
        var cancelResponse = await client1.PostAsync($"/registrations/{firstReg!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Waitlist entry should be promoted to Reserved
        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var promotedEntry = await db.Registrations.FindAsync(wlEntry.Id);
        Assert.Equal("Reserved", promotedEntry!.Status.ToString());
        Assert.Null(promotedEntry.QueuePosition);
        Assert.True(promotedEntry.ReservationExpiresAt > DateTimeOffset.UtcNow);

        // WaitlistPromotedEmail outbox message written
        var outboxMsg = await db.OutboxMessages
            .FirstOrDefaultAsync(o => o.Type == "WaitlistPromotedEmail");
        Assert.NotNull(outboxMsg);
        Assert.Contains(wlEntry.Id.ToString(), outboxMsg.Payload);
    }

    [Fact]
    public async Task CancelWaitlistEntry_DoesNotTriggerPromotion()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org8@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("wl.ath8a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        // Add two athletes to the waitlist
        var ath2Token = await RegisterAthleteTokenAsync("wl.ath8b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var wl1Response = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wl1 = await wl1Response.Content.ReadFromJsonAsync<RegistrationResponse>();

        var ath3Token = await RegisterAthleteTokenAsync("wl.ath8c@example.com", "Password123!");
        using var client3 = CreateAuthenticatedClient(ath3Token);
        var wl2Response = await client3.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wl2 = await wl2Response.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Cancel the first waitlist entry — no capacity is freed, so no promotion
        var cancelResponse = await client2.PostAsync($"/registrations/{wl1!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Second waitlist entry should still be Waitlisted (not promoted)
        var stillWaiting = await db.Registrations.FindAsync(wl2!.Id);
        Assert.Equal("Waitlisted", stillWaiting!.Status.ToString());

        // No WaitlistPromotedEmail should be written
        var promotionCount = await db.OutboxMessages
            .CountAsync(o => o.Type == "WaitlistPromotedEmail");
        Assert.Equal(0, promotionCount);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelledRegistration_Returns409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("wl.org9@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        var athleteToken = await RegisterAthleteTokenAsync("wl.ath9@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(athleteToken);

        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Cancel once — succeeds
        var first = await client.PostAsync($"/registrations/{reg!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Cancel again — should fail
        var second = await client.PostAsync($"/registrations/{reg.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}
