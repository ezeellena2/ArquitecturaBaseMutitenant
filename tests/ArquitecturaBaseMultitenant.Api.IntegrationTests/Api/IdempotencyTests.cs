using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Api;

[Collection(ApiTestGroup.Name)]
public sealed class IdempotencyTests(ApiFactory factory)
{
    [Fact]
    public async Task Parallel_requests_with_the_same_key_execute_once_and_replay_the_response()
    {
        using var client = factory.CreateClient();
        var probe = IdempotencyProbe.Create(block: true);
        var key = Guid.NewGuid();
        var body = new IdempotencyTestRequest(probe.Id, "uno");

        var firstTask = SendAsync(client, "create", body, key);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);

        try
        {
            using var concurrent = await SendAsync(client, "create", body, key)
                .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, concurrent.StatusCode);
            Assert.Equal("Request.InProgress", await ErrorCodeAsync(concurrent));
        }
        finally
        {
            probe.Release.TrySetResult();
        }

        using var first = await firstTask;
        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var replay = await SendAsync(client, "create", body, key);
        Assert.Equal(first.StatusCode, replay.StatusCode);
        Assert.Equal(firstBody, await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(1, probe.ExecutionCount);
    }

    [Fact]
    public async Task Disconnect_after_action_does_not_leave_the_reservation_in_progress()
    {
        using var client = factory.CreateClient();
        var probe = IdempotencyProbe.Create();
        var key = Guid.NewGuid();
        var body = new IdempotencyTestRequest(probe.Id, "uno");

        try
        {
            using var disconnected = await SendAsync(client, "create-abort-after-result", body, key);
        }
        catch (HttpRequestException)
        {
            // El servidor abortó la conexión después de serializar la respuesta capturada.
        }
        catch (TaskCanceledException)
        {
            // TestServer puede traducir el aborto de la conexión a cancelación del cliente.
        }

        using var replay = await SendAsync(client, "create-abort-after-result", body, key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(1, probe.ExecutionCount);
    }

    [Fact]
    public async Task Same_key_with_another_body_or_route_returns_422()
    {
        using var client = factory.CreateClient();
        var probe = IdempotencyProbe.Create();
        var key = Guid.NewGuid();
        var body = new IdempotencyTestRequest(probe.Id, "uno");

        using var first = await SendAsync(client, "create", body, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var otherBody = await SendAsync(client, "create", body with { Name = "dos" }, key);
        Assert.Equal((HttpStatusCode)422, otherBody.StatusCode);
        Assert.Equal("Request.IdempotencyKeyReused", await ErrorCodeAsync(otherBody));

        using var otherRoute = await SendAsync(client, "alternate", body, key);
        Assert.Equal((HttpStatusCode)422, otherRoute.StatusCode);
        Assert.Equal("Request.IdempotencyKeyReused", await ErrorCodeAsync(otherRoute));
        Assert.Equal(1, probe.ExecutionCount);
    }

    [Fact]
    public async Task Missing_or_invalid_key_returns_400_without_executing()
    {
        using var client = factory.CreateClient();
        var probe = IdempotencyProbe.Create();
        var body = new IdempotencyTestRequest(probe.Id, "uno");

        using var missing = await SendAsync(client, "create", body);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("Request.IdempotencyKeyRequired", await ErrorCodeAsync(missing));

        using var invalid = await SendAsync(client, "create", body, Guid.Empty);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("Request.IdempotencyKeyRequired", await ErrorCodeAsync(invalid));
        Assert.Equal(0, probe.ExecutionCount);
    }

    [Fact]
    public async Task Fourxx_response_is_replayed_without_executing_again()
    {
        using var client = factory.CreateClient();
        var probe = IdempotencyProbe.Create();
        var key = Guid.NewGuid();
        var body = new IdempotencyTestRequest(probe.Id, "inválido", BadRequest: true);

        using var first = await SendAsync(client, "create", body, key);
        using var replay = await SendAsync(client, "create", body, key);

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(first.StatusCode, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await replay.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("true", replay.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal(1, probe.ExecutionCount);
    }

    [Fact]
    public async Task Five_xx_response_releases_the_reservation()
    {
        using var client = factory.CreateClient();
        var probe = IdempotencyProbe.Create(failFirst: true);
        var key = Guid.NewGuid();
        var body = new IdempotencyTestRequest(probe.Id, "uno");

        using var first = await SendAsync(client, "create", body, key);
        using var retried = await SendAsync(client, "create", body, key);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        Assert.False(retried.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(2, probe.ExecutionCount);
    }

    [Fact]
    public async Task Unhandled_action_exception_releases_the_reservation()
    {
        using var client = factory.CreateClient();
        var probe = IdempotencyProbe.Create(throwFirst: true);
        var key = Guid.NewGuid();
        var body = new IdempotencyTestRequest(probe.Id, "uno");

        using var first = await SendAsync(client, "create", body, key);
        using var retried = await SendAsync(client, "create", body, key);

        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        Assert.False(retried.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(2, probe.ExecutionCount);
    }

    [Fact]
    public async Task Cleanup_expires_keys_after_24_hours_and_the_same_key_can_be_reserved_again()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new IdempotencyStore(factory.ConnectionString, clock);
        var key = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var first = await store.ReserveAsync(null, Guid.Empty, key, "hash", "/test/idempotency/create", ct);
        Assert.Equal(IdempotencyReservationState.Reserved, first.State);

        clock.Advance(TimeSpan.FromHours(24));
        var worker = new IdempotencyCleanupWorker(store, clock,
            NullLogger<IdempotencyCleanupWorker>.Instance);
        Assert.True(await worker.CleanupOnceAsync(ct) >= 1);

        var second = await store.ReserveAsync(null, Guid.Empty, key, "hash", "/test/idempotency/create", ct);
        Assert.Equal(IdempotencyReservationState.Reserved, second.State);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Same_key_is_independent_per_user_and_tenant()
    {
        var store = new IdempotencyStore(factory.ConnectionString,
            new FakeTimeProvider(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var key = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var anonymousA = await store.ReserveAsync(null, userA, key, "hash", "/route", ct);
        var anonymousB = await store.ReserveAsync(null, Guid.NewGuid(), key, "hash", "/route", ct);
        var tenantA = await store.ReserveAsync(Guid.NewGuid(), userA, key, "hash", "/route", ct);

        Assert.All(new[] { anonymousA, anonymousB, tenantA }, reservation =>
            Assert.Equal(IdempotencyReservationState.Reserved, reservation.State));
        Assert.Equal(3, new[] { anonymousA.Id, anonymousB.Id, tenantA.Id }.Distinct().Count());
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string route,
        IdempotencyTestRequest body,
        Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/test/idempotency/{route}")
        {
            Content = JsonContent.Create(body),
        };
        if (key is { } value)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", value.ToString("D"));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("code").GetString()!;
    }
}
