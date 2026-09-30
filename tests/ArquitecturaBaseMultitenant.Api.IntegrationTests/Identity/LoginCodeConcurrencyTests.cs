using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

[Collection(ApiTestGroup.Name)]
public sealed class LoginCodeConcurrencyTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_login_or_signup_verification_consumes_code_once(bool signup)
    {
        var address = $"concurrent-code-{Guid.NewGuid():N}@example.test";
        var gate = new LoginCodeReadGate(address, signup ? LoginCodePurpose.Signup : LoginCodePurpose.Login);
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var descriptor = Assert.Single(services, item =>
                item.ServiceType == typeof(ILoginCodeRepository));
            services.Remove(descriptor);
            services.AddScoped<ILoginCodeRepository>(provider => new PausedLoginCodeRepository(
                (ILoginCodeRepository)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!),
                gate));
            var lookup = Assert.Single(services, item => item.ServiceType == typeof(IUserLookup));
            services.Remove(lookup);
            services.AddScoped<IUserLookup>(provider => new ObservedUserLookup(
                (IUserLookup)ActivatorUtilities.CreateInstance(provider, lookup.ImplementationType!), gate));
        }));
        using var firstClient = host.CreateClient();
        using var secondClient = host.CreateClient();
        await host.Services.SeedDatabaseAsync(Ct);
        if (!signup) await CreateVerifiedEmailAccountAsync(host.Services, address);

        var requested = await firstClient.PostAsJsonAsync(signup ? "/test/auth/signup" : "/test/auth/request-code",
            new { email = address, acceptedTerms = true }, Ct);
        Assert.Equal(signup ? HttpStatusCode.Accepted : HttpStatusCode.OK, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, address, Ct);
        gate.Arm();

        var path = signup ? "/test/auth/signup/verify" : "/test/auth/verify-code";
        var body = new { email = address, code, acceptedTerms = true,
            returnUrl = "/connect/authorize?client_id=web" };
        var first = firstClient.PostAsJsonAsync(path, body, Ct);
        await gate.FirstRead.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        var second = secondClient.PostAsJsonAsync(path, body, Ct);
        try
        {
            await gate.SecondVerificationStarted.Task.WaitAsync(TimeSpan.FromSeconds(15), Ct);
            await Assert.ThrowsAsync<TimeoutException>(() =>
                gate.SecondRead.Task.WaitAsync(TimeSpan.FromMilliseconds(300), Ct));
        }
        finally
        {
            gate.ReleaseFirst.TrySetResult();
        }

        var responses = await Task.WhenAll(first, second);
        Assert.Equal(signup ? HttpStatusCode.NoContent : HttpStatusCode.OK, responses[0].StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, responses[1].StatusCode);
        var problem = await responses[1].Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal(LoginCodeErrors.AlreadyUsed.Code, problem.GetProperty("code").GetString());
        Assert.Equal(1, await CountSucceededAuditsAsync(address));
    }

    private static async Task CreateVerifiedEmailAccountAsync(IServiceProvider provider, string address)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var email = Email.Create(address).Value;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var users = services.GetRequiredService<IUserRepository>();
            var user = await users.CreateAsync(null, "es-AR", "America/Argentina/Buenos_Aires", ct);
            var method = LoginMethod.CreateEmail(user.Id, email);
            method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            method.MakePrimary();
            await users.SetPrimaryEmailAsync(user.Id, email, ct);
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task<long> CountSucceededAuditsAsync(string address)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*) FROM identity."LoginAudits" audit
            JOIN identity."LoginMethods" method ON method."UserId" = audit."UserId"
            WHERE method."Value" = @email AND audit."Succeeded"
            """;
        command.Parameters.AddWithValue("email", address);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private sealed class LoginCodeReadGate(string address, LoginCodePurpose purpose)
    {
        private object? _firstReader;
        private bool _armed;

        public TaskCompletionSource FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondVerificationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm() => _armed = true;

        public void BeforeLock(object repository, LoginCodeDestination destination)
        {
            if (_armed && destination.Value == address && FirstRead.Task.IsCompleted &&
                !ReferenceEquals(Volatile.Read(ref _firstReader), repository))
                SecondVerificationStarted.TrySetResult();
        }

        public void AfterUserLookup(string value)
        {
            if (_armed && purpose == LoginCodePurpose.Login && value == address && FirstRead.Task.IsCompleted)
                SecondVerificationStarted.TrySetResult();
        }

        public async Task AfterReadAsync(object repository, LoginCodeDestination destination,
            LoginCodePurpose readPurpose, LoginCode? code, CancellationToken cancellationToken)
        {
            if (!_armed || code is null || destination.Value != address || readPurpose != purpose) return;
            if (Interlocked.CompareExchange(ref _firstReader, repository, null) is null)
            {
                FirstRead.TrySetResult();
                await ReleaseFirst.Task.WaitAsync(cancellationToken);
            }
            else if (!ReferenceEquals(Volatile.Read(ref _firstReader), repository))
            {
                SecondRead.TrySetResult();
            }
        }
    }

    private sealed class ObservedUserLookup(IUserLookup inner, LoginCodeReadGate gate) : IUserLookup
    {
        public async Task<Guid?> FindVerifiedUserIdAsync(LoginMethodType type, string value, CancellationToken ct)
        {
            var userId = await inner.FindVerifiedUserIdAsync(type, value, ct);
            gate.AfterUserLookup(value);
            return userId;
        }

        public Task<LoginMethodLookup?> FindMethodAsync(LoginMethodType type, string value, CancellationToken ct) =>
            inner.FindMethodAsync(type, value, ct);
    }

    private sealed class PausedLoginCodeRepository(ILoginCodeRepository inner, LoginCodeReadGate gate)
        : ILoginCodeRepository
    {
        public Task LockDestinationAsync(LoginCodeDestination destination, CancellationToken cancellationToken)
        {
            gate.BeforeLock(this, destination);
            return inner.LockDestinationAsync(destination, cancellationToken);
        }

        public async Task<LoginCode?> GetLatestAsync(LoginCodeDestination destination, LoginCodePurpose purpose,
            Guid? requestedByUserId, CancellationToken cancellationToken)
        {
            var code = await inner.GetLatestAsync(destination, purpose, requestedByUserId, cancellationToken);
            await gate.AfterReadAsync(this, destination, purpose, code, cancellationToken);
            return code;
        }

        public Task<IReadOnlyList<LoginCode>> ListActiveAsync(LoginCodeDestination destination,
            LoginCodePurpose purpose, Guid? requestedByUserId, DateTime nowUtc,
            CancellationToken cancellationToken) =>
            inner.ListActiveAsync(destination, purpose, requestedByUserId, nowUtc, cancellationToken);

        public Task<IReadOnlyList<DateTime>> ListRequestTimesSinceAsync(LoginCodeDestination destination,
            DateTime sinceUtc, CancellationToken cancellationToken) =>
            inner.ListRequestTimesSinceAsync(destination, sinceUtc, cancellationToken);

        public void Add(LoginCode code) => inner.Add(code);
    }
}
