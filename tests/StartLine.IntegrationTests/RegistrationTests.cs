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

public class RegistrationTests : IAsyncLifetime
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

    /// <summary>Creates an event with a race and returns the race ID.</summary>
    private async Task<(Guid eventId, Guid raceId)> CreateEventWithRaceAsync(
        string orgToken,
        int capacity = 5,
        int? minAge = null,
        int? maxAge = null,
        string? allowedGender = null)
    {
        using var org = CreateAuthenticatedClient(orgToken);

        var eventResponse = await org.PostAsJsonAsync("/events", new
        {
            name = "Test Event",
            date = "2099-06-01",
            location = "Prague",
            description = (string?)null
        });
        var eventBody = await eventResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        var raceBody = new
        {
            name = "5K Run",
            capacity,
            basePrice = 50.00m,
            earlyBirdPrice = (decimal?)null,
            earlyBirdDeadline = (string?)null,
            minAge,
            maxAge,
            allowedGender
        };
        var raceResponse = await org.PostAsJsonAsync($"/events/{eventBody!.Id}/races", raceBody);
        var race = await raceResponse.Content.ReadFromJsonAsync<RaceResponse>();

        return (eventBody.Id, race!.Id);
    }

    private static object DefaultRegistrationBody(Guid raceId) => new
    {
        raceId,
        firstName = "John",
        lastName = "Doe",
        email = "john.doe@example.com",
        dateOfBirth = "1990-05-15",
        gender = 0,   // Male
        club = "City Runners",
        phone = "+420123456789"
    };

    // ── POST /registrations ───────────────────────────────────────────────────

    [Fact]
    public async Task Register_AsAthlete_Returns201WithReservedStatus()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org1@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath1@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);
        var response = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(raceId, body.RaceId);
        Assert.Equal("Reserved", body.Status);
        Assert.True(body.ReservationExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal("John", body.FirstName);
        Assert.Equal("Doe", body.LastName);
        Assert.Equal("john.doe@example.com", body.Email);
        Assert.Equal("City Runners", body.Club);
    }

    [Fact]
    public async Task Register_Unauthenticated_Returns401()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org2@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        var response = await _client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_AsOrganizer_Returns403()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org3@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(orgToken);
        var response = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_NonExistentRace_Returns404()
    {
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath4@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(athleteToken);

        var response = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenCapacityIsZero_Returns409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org5@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath5@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 0);

        using var client = CreateAuthenticatedClient(athleteToken);
        var response = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenCapacityExceeded_Returns409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org6@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Register the only spot
        var ath1Token = await RegisterAthleteTokenAsync("reg.ath6a@example.com", "Password123!");
        using var ath1 = CreateAuthenticatedClient(ath1Token);
        var first = await ath1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Second athlete should get 409
        var ath2Token = await RegisterAthleteTokenAsync("reg.ath6b@example.com", "Password123!");
        using var ath2 = CreateAuthenticatedClient(ath2Token);
        var second = await ath2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_AgeTooLow_Returns422()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org7@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath7@example.com", "Password123!");
        // Race requires minimum age 30; athlete born in 2010 will be ~89 years old in 2099
        // Let's use minAge: 50 and a recent birth date to ensure they are too young
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, minAge: 50);

        using var client = CreateAuthenticatedClient(athleteToken);

        // Athlete born in 2090 – only 9 years old on event date (2099-06-01)
        var body = new
        {
            raceId,
            firstName = "Young",
            lastName = "Athlete",
            email = "young@example.com",
            dateOfBirth = "2090-01-01",
            gender = 0,
            club = (string?)null,
            phone = (string?)null
        };
        var response = await client.PostAsJsonAsync("/registrations", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_AgeTooHigh_Returns422()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org8@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath8@example.com", "Password123!");
        // Race allows max age 20; athlete born in 1950 will be ~149 years old – over limit
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, maxAge: 20);

        using var client = CreateAuthenticatedClient(athleteToken);

        var body = new
        {
            raceId,
            firstName = "Old",
            lastName = "Athlete",
            email = "old@example.com",
            dateOfBirth = "1950-01-01",
            gender = 0,
            club = (string?)null,
            phone = (string?)null
        };
        var response = await client.PostAsJsonAsync("/registrations", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_GenderMismatch_Returns422()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org9@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath9@example.com", "Password123!");
        // Race is females-only (1 = Female)
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, allowedGender: "Female");

        using var client = CreateAuthenticatedClient(athleteToken);

        // Register as Male (0)
        var body = new
        {
            raceId,
            firstName = "Male",
            lastName = "Athlete",
            email = "male@example.com",
            dateOfBirth = "1990-01-01",
            gender = 0,   // Male
            club = (string?)null,
            phone = (string?)null
        };
        var response = await client.PostAsJsonAsync("/registrations", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── GET /registrations/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task GetRegistration_ByOwner_Returns200()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org10@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath10@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);
        var createResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var created = await createResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        var getResponse = await client.GetAsync($"/registrations/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal("Reserved", body.Status);
    }

    [Fact]
    public async Task GetRegistration_ByDifferentAthlete_Returns404()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org11@example.com", "Password123!");
        var ath1Token = await RegisterAthleteTokenAsync("reg.ath11a@example.com", "Password123!");
        var ath2Token = await RegisterAthleteTokenAsync("reg.ath11b@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var ath1 = CreateAuthenticatedClient(ath1Token);
        var createResponse = await ath1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var created = await createResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Different athlete tries to get ath1's registration
        using var ath2 = CreateAuthenticatedClient(ath2Token);
        var getResponse = await ath2.GetAsync($"/registrations/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetRegistration_NonExistent_Returns404()
    {
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath12@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(athleteToken);

        var response = await client.GetAsync($"/registrations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Concurrent reservation test ───────────────────────────────────────────

    [Fact]
    public async Task ConcurrentRegister_LastSpot_ExactlyOneSucceedsAndOneGets409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org13@example.com", "Password123!");
        var ath1Token = await RegisterAthleteTokenAsync("reg.ath13a@example.com", "Password123!");
        var ath2Token = await RegisterAthleteTokenAsync("reg.ath13b@example.com", "Password123!");

        // Race with exactly 1 spot
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        using var ath1 = CreateAuthenticatedClient(ath1Token);
        using var ath2 = CreateAuthenticatedClient(ath2Token);

        var body = DefaultRegistrationBody(raceId);

        // Fire both requests simultaneously
        var task1 = ath1.PostAsJsonAsync("/registrations", body);
        var task2 = ath2.PostAsJsonAsync("/registrations", body);

        await Task.WhenAll(task1, task2);

        var status1 = task1.Result.StatusCode;
        var status2 = task2.Result.StatusCode;

        // Exactly one must be 201 Created and one must be 409 Conflict
        Assert.True(
            (status1 == HttpStatusCode.Created && status2 == HttpStatusCode.Conflict) ||
            (status1 == HttpStatusCode.Conflict && status2 == HttpStatusCode.Created),
            $"Expected one 201 and one 409, got {status1} and {status2}");
    }

    // ── AvailableCapacity reflects active registrations ───────────────────────

    [Fact]
    public async Task AvailableCapacity_DecreasesAfterRegistration()
    {
        var orgToken = await RegisterOrganizerTokenAsync("reg.org14@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("reg.ath14@example.com", "Password123!");
        var (eventId, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 5);

        // Before registration
        var before = await _client.GetAsync($"/events/{eventId}");
        var beforeBody = await before.Content.ReadFromJsonAsync<EventDetailResponse>();
        var beforeCapacity = beforeBody!.Races.Single(r => r.Id == raceId).AvailableCapacity;
        Assert.Equal(5, beforeCapacity);

        // Register
        using var client = CreateAuthenticatedClient(athleteToken);
        await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));

        // After registration
        var after = await _client.GetAsync($"/events/{eventId}");
        var afterBody = await after.Content.ReadFromJsonAsync<EventDetailResponse>();
        var afterCapacity = afterBody!.Races.Single(r => r.Id == raceId).AvailableCapacity;
        Assert.Equal(4, afterCapacity);
    }

    // ── POST /registrations/{id}/pay ─────────────────────────────────────────

    [Fact]
    public async Task Pay_ReservedRegistration_Returns200WithPaidStatus()
    {
        var orgToken = await RegisterOrganizerTokenAsync("pay.org1@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("pay.ath1@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);

        // Register
        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Pay
        var payResponse = await client.PostAsync($"/registrations/{reg!.Id}/pay", null);
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);

        var body = await payResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.Equal(reg.Id, body.Id);
        Assert.Equal("Paid", body.Status);
    }

    [Fact]
    public async Task Pay_AlreadyPaidRegistration_Returns409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("pay.org2@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("pay.ath2@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);

        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // First payment succeeds
        var firstPay = await client.PostAsync($"/registrations/{reg!.Id}/pay", null);
        Assert.Equal(HttpStatusCode.OK, firstPay.StatusCode);

        // Second payment should fail: not in Reserved status
        var secondPay = await client.PostAsync($"/registrations/{reg.Id}/pay", null);
        Assert.Equal(HttpStatusCode.Conflict, secondPay.StatusCode);
    }

    [Fact]
    public async Task Pay_ExpiredReservation_Returns409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("pay.org3@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("pay.ath3@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);

        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Expire the reservation in the database
        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Registrations\" SET \"ReservationExpiresAt\" = {0} WHERE \"Id\" = {1}",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            reg!.Id);

        var payResponse = await client.PostAsync($"/registrations/{reg.Id}/pay", null);
        Assert.Equal(HttpStatusCode.Conflict, payResponse.StatusCode);
    }

    [Fact]
    public async Task Pay_NonExistentRegistration_Returns404()
    {
        var athleteToken = await RegisterAthleteTokenAsync("pay.ath4@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(athleteToken);

        var payResponse = await client.PostAsync($"/registrations/{Guid.NewGuid()}/pay", null);
        Assert.Equal(HttpStatusCode.NotFound, payResponse.StatusCode);
    }

    [Fact]
    public async Task Pay_OtherAthletesRegistration_Returns404()
    {
        var orgToken = await RegisterOrganizerTokenAsync("pay.org5@example.com", "Password123!");
        var ath1Token = await RegisterAthleteTokenAsync("pay.ath5a@example.com", "Password123!");
        var ath2Token = await RegisterAthleteTokenAsync("pay.ath5b@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var ath1 = CreateAuthenticatedClient(ath1Token);
        var regResponse = await ath1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Different athlete tries to pay
        using var ath2 = CreateAuthenticatedClient(ath2Token);
        var payResponse = await ath2.PostAsync($"/registrations/{reg!.Id}/pay", null);
        Assert.Equal(HttpStatusCode.NotFound, payResponse.StatusCode);
    }

    [Fact]
    public async Task Pay_WritesOutboxMessageInSameTransaction()
    {
        var orgToken = await RegisterOrganizerTokenAsync("pay.org6@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("pay.ath6@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);

        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        var payResponse = await client.PostAsync($"/registrations/{reg!.Id}/pay", null);
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);

        // Verify outbox message was created
        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxMessage = await db.OutboxMessages
            .FirstOrDefaultAsync(o => o.Type == "PaymentConfirmedEmail");

        Assert.NotNull(outboxMessage);
        Assert.Null(outboxMessage.ProcessedAt);
        Assert.Contains(reg.Id.ToString(), outboxMessage.Payload);
    }
}
