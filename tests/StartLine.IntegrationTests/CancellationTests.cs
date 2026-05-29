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

public class CancellationTests : IAsyncLifetime
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
            name = "Cancellation Test Event",
            date = "2099-06-01",
            location = "Brno",
            description = (string?)null
        });
        var eventBody = await eventResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        var raceResponse = await org.PostAsJsonAsync($"/events/{eventBody!.Id}/races", new
        {
            name = "10K Run",
            capacity,
            basePrice = 60.00m,
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
        firstName = "Alice",
        lastName = "Runner",
        email = "alice.runner@example.com",
        dateOfBirth = "1992-03-20",
        gender = 1,   // Female
        club = (string?)null,
        phone = (string?)null
    };

    // ── DELETE /registrations/{id} ────────────────────────────────────────────

    [Fact]
    public async Task CancelReserved_ViaDelete_Returns200WithCancelledStatus()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org1@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath1@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);

        // Register (Reserved)
        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.Equal("Reserved", reg!.Status);

        // Cancel via DELETE
        var cancelResponse = await client.DeleteAsync($"/registrations/{reg.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var body = await cancelResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.Equal(reg.Id, body.Id);
        Assert.Equal("Cancelled", body.Status);
    }

    [Fact]
    public async Task CancelPaid_ViaDelete_Returns200WithCancelledStatus()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org2@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath2@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);

        // Register and pay
        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        var payResponse = await client.PostAsync($"/registrations/{reg!.Id}/pay", null);
        Assert.Equal(HttpStatusCode.OK, payResponse.StatusCode);

        // Cancel via DELETE
        var cancelResponse = await client.DeleteAsync($"/registrations/{reg.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var body = await cancelResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Cancelled", body.Status);
    }

    [Fact]
    public async Task CancelReserved_TriggersWaitlistPromotion()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org3@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("cancel.ath3a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var firstRegResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var firstReg = await firstRegResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Add to waitlist
        var ath2Token = await RegisterAthleteTokenAsync("cancel.ath3b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var wlResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wlEntry = await wlResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.Equal("Waitlisted", wlEntry!.Status);

        // Cancel the first registration via DELETE
        var cancelResponse = await client1.DeleteAsync($"/registrations/{firstReg!.Id}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Waitlist entry should be promoted to Reserved
        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var promotedEntry = await db.Registrations.FindAsync(wlEntry.Id);
        Assert.Equal("Reserved", promotedEntry!.Status.ToString());
        Assert.Null(promotedEntry.QueuePosition);
        Assert.True(promotedEntry.ReservationExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CancelAnotherAthletesRegistration_ViaDelete_Returns403()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org4@example.com", "Password123!");
        var ath1Token = await RegisterAthleteTokenAsync("cancel.ath4a@example.com", "Password123!");
        var ath2Token = await RegisterAthleteTokenAsync("cancel.ath4b@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        // Athlete 1 registers
        using var ath1 = CreateAuthenticatedClient(ath1Token);
        var regResponse = await ath1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Athlete 2 tries to cancel athlete 1's registration
        using var ath2 = CreateAuthenticatedClient(ath2Token);
        var cancelResponse = await ath2.DeleteAsync($"/registrations/{reg!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task CancelAlreadyCancelled_ViaDelete_Returns409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org5@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath5@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var client = CreateAuthenticatedClient(athleteToken);

        var regResponse = await client.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Cancel once — succeeds
        var first = await client.DeleteAsync($"/registrations/{reg!.Id}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Cancel again — 409
        var second = await client.DeleteAsync($"/registrations/{reg.Id}");
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CancelNonExistentRegistration_ViaDelete_Returns404()
    {
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath6@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(athleteToken);

        var cancelResponse = await client.DeleteAsync($"/registrations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, cancelResponse.StatusCode);
    }

    // ── PATCH /organizer/registrations/{id}/status ────────────────────────────

    [Fact]
    public async Task OrganizerForceCancel_ReservedRegistration_Returns200WithCancelledStatus()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org7@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath7@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var athlete = CreateAuthenticatedClient(athleteToken);
        var regResponse = await athlete.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.Equal("Reserved", reg!.Status);

        // Organizer force-cancels
        using var org = CreateAuthenticatedClient(orgToken);
        var patchResponse = await org.PatchAsJsonAsync(
            $"/organizer/registrations/{reg.Id}/status",
            new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var body = await patchResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(body);
        Assert.Equal(reg.Id, body.Id);
        Assert.Equal("Cancelled", body.Status);
    }

    [Fact]
    public async Task OrganizerForceCancel_PaidRegistration_Returns200WithCancelledStatus()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org8@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath8@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var athlete = CreateAuthenticatedClient(athleteToken);
        var regResponse = await athlete.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        await athlete.PostAsync($"/registrations/{reg!.Id}/pay", null);

        // Organizer force-cancels a Paid registration
        using var org = CreateAuthenticatedClient(orgToken);
        var patchResponse = await org.PatchAsJsonAsync(
            $"/organizer/registrations/{reg.Id}/status",
            new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var body = await patchResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.Equal("Cancelled", body!.Status);
    }

    [Fact]
    public async Task OrganizerForceCancel_AlreadyCancelled_Returns409()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org9@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath9@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var athlete = CreateAuthenticatedClient(athleteToken);
        var regResponse = await athlete.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // First cancel succeeds
        using var org = CreateAuthenticatedClient(orgToken);
        var first = await org.PatchAsJsonAsync(
            $"/organizer/registrations/{reg!.Id}/status",
            new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Second cancel returns 409
        var second = await org.PatchAsJsonAsync(
            $"/organizer/registrations/{reg.Id}/status",
            new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task OrganizerForceCancel_NonExistentRegistration_Returns404()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org10@example.com", "Password123!");
        using var org = CreateAuthenticatedClient(orgToken);

        var patchResponse = await org.PatchAsJsonAsync(
            $"/organizer/registrations/{Guid.NewGuid()}/status",
            new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);
    }

    [Fact]
    public async Task OrganizerForceCancel_AsAthlete_Returns403()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org11@example.com", "Password123!");
        var athleteToken = await RegisterAthleteTokenAsync("cancel.ath11@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken);

        using var athlete = CreateAuthenticatedClient(athleteToken);
        var regResponse = await athlete.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var reg = await regResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Athlete tries to use the organizer endpoint
        var patchResponse = await athlete.PatchAsJsonAsync(
            $"/organizer/registrations/{reg!.Id}/status",
            new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.Forbidden, patchResponse.StatusCode);
    }

    [Fact]
    public async Task OrganizerForceCancel_TriggersWaitlistPromotion()
    {
        var orgToken = await RegisterOrganizerTokenAsync("cancel.org12@example.com", "Password123!");
        var (_, raceId) = await CreateEventWithRaceAsync(orgToken, capacity: 1);

        // Fill the slot
        var ath1Token = await RegisterAthleteTokenAsync("cancel.ath12a@example.com", "Password123!");
        using var client1 = CreateAuthenticatedClient(ath1Token);
        var firstRegResponse = await client1.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var firstReg = await firstRegResponse.Content.ReadFromJsonAsync<RegistrationResponse>();

        // Add to waitlist
        var ath2Token = await RegisterAthleteTokenAsync("cancel.ath12b@example.com", "Password123!");
        using var client2 = CreateAuthenticatedClient(ath2Token);
        var wlResponse = await client2.PostAsJsonAsync("/registrations", DefaultRegistrationBody(raceId));
        var wlEntry = await wlResponse.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.Equal("Waitlisted", wlEntry!.Status);

        // Organizer force-cancels the reserved registration
        using var org = CreateAuthenticatedClient(orgToken);
        var patchResponse = await org.PatchAsJsonAsync(
            $"/organizer/registrations/{firstReg!.Id}/status",
            new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        // Waitlist entry should be promoted
        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var promotedEntry = await db.Registrations.FindAsync(wlEntry.Id);
        Assert.Equal("Reserved", promotedEntry!.Status.ToString());
        Assert.Null(promotedEntry.QueuePosition);
    }
}
