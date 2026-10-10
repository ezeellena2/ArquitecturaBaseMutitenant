using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Base y reloj privados; las cuentas y las pruebas se obtienen por HTTP y outbox real.</summary>
internal sealed class PendingMethodJourney : IAsyncDisposable
{
    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];
    private readonly Dictionary<HttpClient, Guid> owners = [];
    private readonly ProofGate proofGate = new();
    internal FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());
    internal WebApplicationFactory<Program> App { get; private set; } = null!;
    internal IServiceProvider Services => App.Services;

    internal async Task InitializeAsync(CancellationToken ct, Action<IWebHostBuilder>? configure = null)
    {
        await factory.InitializeDatabaseAsync(ct);
        App = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing")
                .UseSetting("Authentication:LoginCode:ResendCooldownSeconds", "60")
                .UseSetting("Authentication:LoginCode:MaxRequestsPerWindow", "100");
            configure?.Invoke(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                var original = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IExternalLoginLock));
                services.Remove(original);
                services.AddScoped<IExternalLoginLock>(provider => new SynchronizedProofLock(
                    (IExternalLoginLock)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), proofGate));
            });
        });
    }

    internal async Task<(HttpClient Client, AccountJourney Account)> AccountAsync(CancellationToken ct)
    {
        var client = App.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        clients.Add(client);
        var account = await AccountJourney.RegisterAsync(App, client, ct);
        owners.Add(client, account.UserId);
        return (client, account);
    }

    internal void NextEmission() => Clock.Advance(TimeSpan.FromSeconds(61));

    internal async Task<(Guid Id, string Code)> AddAsync(HttpClient client, object body,
        string destination, string channel, CancellationToken ct)
    {
        var cursor = await OutboxIdsAsync(ct);
        using var response = await AccountJourney.PostAsync(client, "/api/me/login-methods", body, ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
        var id = json!.RootElement.GetProperty("methodId").GetGuid();
        return (id, await ProofAsync(owners[client], destination, channel, cursor, ct));
    }

    private async Task<string> ProofAsync(Guid ownerId, string destination, string channel, Guid[] cursor, CancellationToken ct)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IPayloadProtector>();
        var payloads = await db.OutboxMessages.AsNoTracking()
            .Where(row => row.UserId == ownerId && row.Channel == channel && !cursor.Contains(row.Id))
            .Select(row => row.EncryptedPayload).ToArrayAsync(ct);
        var proofs = new List<string>();
        foreach (var encrypted in payloads.Where(payload => payload.Length > 0))
        {
            using var payload = JsonDocument.Parse(protector.Unprotect(encrypted));
            var root = payload.RootElement;
            if (root.GetProperty("To").GetString() != destination) continue;
            if (channel == "email")
            {
                var match = Regex.Match(root.GetProperty("TextBody").GetString()!, @"(?m)^(?<code>\d{6})\r?$");
                if (match.Success) proofs.Add(match.Groups["code"].Value);
            }
            else if (root.GetProperty("TemplateName").GetString() == "codigo_ingreso")
                proofs.Add(root.GetProperty("BodyParameters")[0].GetString()!);
        }
        Assert.True(proofs.Count == 1, "Expected exactly one proof in the committed destination outbox.");
        return proofs[0];
    }

    internal static Task<HttpResponseMessage> VerifyAsync(HttpClient client, Guid methodId,
        string code, CancellationToken ct) => AccountJourney.PostAsync(client,
            $"/api/me/login-methods/{methodId}/verify", new { code }, ct);

    internal Task<HttpResponseMessage[]> VerifyTogetherAsync(string destination,
        HttpClient firstClient, Guid firstId, string firstCode, HttpClient secondClient, Guid secondId,
        string secondCode, CancellationToken ct)
    {
        proofGate.Destination = destination;
        return Task.WhenAll(VerifyAsync(firstClient, firstId, firstCode, ct), VerifyAsync(secondClient, secondId, secondCode, ct));
    }

    internal async Task<Guid[]> OutboxIdsAsync(CancellationToken ct)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking()
            .OrderBy(row => row.Id).Select(row => row.Id).ToArrayAsync(ct);
    }

    internal async Task<LoginMethod[]> MethodsAsync(LoginMethodType type, string value, CancellationToken ct)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginMethods.AsNoTracking()
            .Where(row => row.Type == type && row.Value == value).OrderBy(row => row.Id).ToArrayAsync(ct);
    }

    internal async Task AssertNoChangeNoticeAsync(Guid[] before, CancellationToken ct) =>
        Assert.Equal(before, await OutboxIdsAsync(ct));

    internal async Task<string[]> SnapshotAsync(CancellationToken ct)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
        return [Hash(await db.LoginMethods.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(ct)),
            Hash(await db.LoginCodes.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(ct)),
            Hash(await db.Users.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(ct)),
            Hash(await db.SecurityEvents.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(ct)),
            Hash(await db.OutboxMessages.AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(ct))];
    }

    internal async Task AssertConsumedAsync(Guid ownerId, string destination, bool consumed, CancellationToken ct)
    {
        await using var scope = Services.CreateAsyncScope();
        var code = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().LoginCodes.AsNoTracking()
            .Where(row => row.RequestedByUserId == ownerId && row.Destination == destination
                && row.Purpose == LoginCodePurpose.VerifyDestination).OrderByDescending(row => row.CreatedAtUtc).FirstAsync(ct);
        Assert.Equal(consumed, code.ConsumedAtUtc is not null);
    }

    /// <summary>Estado histórico permitido: un verificado puede coexistir con reservas no verificadas.</summary>
    internal async Task SeedVerifiedOwnerAsync(Guid ownerId, LoginMethodType type, string destination, CancellationToken ct)
    {
        await using var scope = Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
        {
            var identities = services.GetRequiredService<IExternalLoginLock>();
            if (type == LoginMethodType.Phone) await identities.LockPhoneAsync(PhoneNumber.Create(destination).Value, token);
            else await identities.LockEmailAsync(Email.Create(destination).Value, token);
            var methods = services.GetRequiredService<ILoginMethodRepository>();
            await methods.LockUserAsync(ownerId, token);
            var method = type == LoginMethodType.Phone ? LoginMethod.CreatePhone(ownerId, PhoneNumber.Create(destination).Value)
                : LoginMethod.CreateEmail(ownerId, Email.Create(destination).Value);
            Assert.True(method.Verify(Clock.GetUtcNow().UtcDateTime).IsSuccess);
            methods.Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, ct);
        Assert.True(saved.IsSuccess);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in clients) client.Dispose();
        if (App is not null) await App.DisposeAsync();
        await factory.DisposeAsync();
    }

    // Las dos peticiones leen su hint antes de competir por el lock PostgreSQL real.
    private sealed class ProofGate
    {
        private int arrivals;
        private readonly TaskCompletionSource bothReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string? Destination { get; set; }
        internal async Task ArriveAsync(string value, CancellationToken ct)
        {
            if (Destination != value) return;
            if (Interlocked.Increment(ref arrivals) == 2) bothReached.TrySetResult();
            await bothReached.Task.WaitAsync(ct);
        }
    }
    private sealed class SynchronizedProofLock(IExternalLoginLock inner, ProofGate gate) : IExternalLoginLock
    {
        public async Task LockEmailAsync(Email email, CancellationToken ct)
        { await gate.ArriveAsync(email.Value, ct); await inner.LockEmailAsync(email, ct); }
        public async Task LockPhoneAsync(PhoneNumber phone, CancellationToken ct)
        { await gate.ArriveAsync(phone.Value, ct); await inner.LockPhoneAsync(phone, ct); }
        public Task AcquireAsync(string subject, Email? email, CancellationToken ct) => inner.AcquireAsync(subject, email, ct);
    }
}
