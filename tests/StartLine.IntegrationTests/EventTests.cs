using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StartLine.Application.Auth;
using StartLine.Application.Events;
using StartLine.Infrastructure.Persistence;

namespace StartLine.IntegrationTests;

public class EventTests : IAsyncLifetime
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

    private async Task<string> RegisterOrganizerTokenAsync(string email, string password)
    {
        await _client.PostAsJsonAsync("/auth/register", new { email, password });

        // Elevate the user to Organizer role directly in the database
        using var scope = _factory.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"Users\" SET \"Role\" = 1 WHERE \"Email\" = {0}",
            email.ToLowerInvariant());

        // Login to obtain a fresh token that reflects the Organizer role
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

    private static object DefaultEventBody(string suffix = "") => new
    {
        name = $"City Marathon{suffix}",
        date = "2099-09-21",
        location = "Prague",
        description = "Annual city race"
    };

    private static object DefaultRaceBody(string name = "5K") => new
    {
        name,
        capacity = 100,
        basePrice = 50.00m,
        earlyBirdPrice = 35.00m,
        earlyBirdDeadline = "2099-08-01"
    };

    // ── POST /events ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_AsOrganizer_Returns201()
    {
        var token = await RegisterOrganizerTokenAsync("org.create@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/events", DefaultEventBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventDetailResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("City Marathon", body.Name);
    }

    [Fact]
    public async Task CreateEvent_AsAthlete_Returns403()
    {
        var registerResponse = await _client.PostAsJsonAsync("/auth/register",
            new { email = "athlete.create@example.com", password = "Password123!" });
        var authBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        using var client = CreateAuthenticatedClient(authBody!.AccessToken);

        var response = await client.PostAsJsonAsync("/events", DefaultEventBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_Unauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/events", DefaultEventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST /events/{id}/races ───────────────────────────────────────────────

    [Fact]
    public async Task AddRace_ToExistingEvent_Returns201()
    {
        var token = await RegisterOrganizerTokenAsync("org.race@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var eventResponse = await client.PostAsJsonAsync("/events", DefaultEventBody());
        var eventBody = await eventResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        var raceResponse = await client.PostAsJsonAsync($"/events/{eventBody!.Id}/races", DefaultRaceBody());

        Assert.Equal(HttpStatusCode.Created, raceResponse.StatusCode);
        var raceBody = await raceResponse.Content.ReadFromJsonAsync<RaceResponse>();
        Assert.NotNull(raceBody);
        Assert.Equal("5K", raceBody.Name);
        Assert.Equal(100, raceBody.Capacity);
        Assert.Equal(100, raceBody.AvailableCapacity);
        Assert.Equal(50.00m, raceBody.BasePrice);
        Assert.Equal(35.00m, raceBody.EarlyBirdPrice);
    }

    [Fact]
    public async Task AddRace_ToNonExistentEvent_Returns404()
    {
        var token = await RegisterOrganizerTokenAsync("org.race404@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync($"/events/{Guid.NewGuid()}/races", DefaultRaceBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /events ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListEvents_ReturnsUpcomingNonDeletedEvents()
    {
        var token = await RegisterOrganizerTokenAsync("org.list@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        // Create two events
        await client.PostAsJsonAsync("/events", new
        {
            name = "ListTest A",
            date = "2099-10-01",
            location = "Brno",
            description = (string?)null
        });
        await client.PostAsJsonAsync("/events", new
        {
            name = "ListTest B",
            date = "2099-11-01",
            location = "Brno",
            description = (string?)null
        });

        var response = await _client.GetAsync("/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(body);
        var items = body.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task ListEvents_ExcludesDeletedEvents()
    {
        var token = await RegisterOrganizerTokenAsync("org.listdel@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        // Create and soft-delete an event
        var createResponse = await client.PostAsJsonAsync("/events", new
        {
            name = "ToBeDeleted",
            date = "2099-12-01",
            location = "City",
            description = (string?)null
        });
        var created = await createResponse.Content.ReadFromJsonAsync<EventDetailResponse>();
        await client.DeleteAsync($"/events/{created!.Id}");

        // List must not contain the deleted event
        var listResponse = await _client.GetAsync("/events");
        var body = await listResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var items = body!.RootElement.GetProperty("items");

        var ids = Enumerable.Range(0, items.GetArrayLength())
            .Select(i => items[i].GetProperty("id").GetString())
            .ToList();

        Assert.DoesNotContain(created.Id.ToString(), ids);
    }

    // ── GET /events/{id} ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvent_ExistingEvent_ReturnsDetailWithRaces()
    {
        var token = await RegisterOrganizerTokenAsync("org.detail@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var eventResponse = await client.PostAsJsonAsync("/events", DefaultEventBody(".Detail"));
        var created = await eventResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        await client.PostAsJsonAsync($"/events/{created!.Id}/races", DefaultRaceBody("10K"));

        var response = await _client.GetAsync($"/events/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EventDetailResponse>();
        Assert.NotNull(body);
        Assert.Single(body.Races);
        Assert.Equal("10K", body.Races[0].Name);
        Assert.Equal(100, body.Races[0].AvailableCapacity);
    }

    [Fact]
    public async Task GetEvent_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /events/{id} ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEvent_AsOrganizer_Returns200WithUpdatedData()
    {
        var token = await RegisterOrganizerTokenAsync("org.update@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var createResponse = await client.PostAsJsonAsync("/events", DefaultEventBody(".Update"));
        var created = await createResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/events/{created!.Id}", new
        {
            name = "Updated Name",
            date = "2099-09-22",
            location = "Brno",
            description = "Updated description"
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadFromJsonAsync<EventDetailResponse>();
        Assert.NotNull(body);
        Assert.Equal("Updated Name", body.Name);
        Assert.Equal("Brno", body.Location);
    }

    // ── DELETE /events/{id} ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEvent_AsOrganizer_Returns204()
    {
        var token = await RegisterOrganizerTokenAsync("org.delete@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var createResponse = await client.PostAsJsonAsync("/events", DefaultEventBody(".Delete"));
        var created = await createResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        var deleteResponse = await client.DeleteAsync($"/events/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_SoftDeletedEvent_NoLongerAccessibleViaGet()
    {
        var token = await RegisterOrganizerTokenAsync("org.softdel@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var createResponse = await client.PostAsJsonAsync("/events", DefaultEventBody(".SoftDel"));
        var created = await createResponse.Content.ReadFromJsonAsync<EventDetailResponse>();

        await client.DeleteAsync($"/events/{created!.Id}");

        var getResponse = await _client.GetAsync($"/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_NonExistent_Returns404()
    {
        var token = await RegisterOrganizerTokenAsync("org.del404@example.com", "Password123!");
        using var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync($"/events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
