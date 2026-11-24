using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// ADR 0062: la retención de los datos globales del ingreso. Los códigos se borran a los 30 días; los ingresos, a los 12
/// meses, salvo el último de cada cuenta; y las autorizaciones y los tokens de OpenIddict, a los 14 días de vencidos o
/// revocados. Corre por lotes, en el scope global, y una segunda corrida no encuentra nada.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class IdentityRetentionTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_codes_are_removed_thirty_days_after_they_were_issued()
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var old = await IssueCodeAsync(nowUtc.AddDays(-30).AddMinutes(-1));
        var recent = await IssueCodeAsync(nowUtc.AddDays(-29));

        await RunAsync();

        Assert.False(await ExistsAsync<LoginCode>(old));
        Assert.True(await ExistsAsync<LoginCode>(recent));
    }

    [Fact]
    public async Task Sign_ins_are_removed_after_twelve_months_except_the_last_one_of_each_account()
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var dormant = await CreateUserAsync();
        var active = await CreateUserAsync();
        var oldest = await AuditAsync(LoginAudit.Success(dormant, LoginAuditMethod.Code, nowUtc.AddMonths(-14)));
        var lastOfDormant = await AuditAsync(LoginAudit.Success(dormant, LoginAuditMethod.Code, nowUtc.AddMonths(-13)));
        var failedOfDormant = await AuditAsync(LoginAudit.Failure(dormant, LoginAuditMethod.Code, LoginCodeErrors.Expired,
            nowUtc.AddMonths(-12).AddDays(-1)));
        var oldOfActive = await AuditAsync(LoginAudit.Success(active, LoginAuditMethod.Code, nowUtc.AddMonths(-13)));
        var lastOfActive = await AuditAsync(LoginAudit.Success(active, LoginAuditMethod.Code, nowUtc.AddMonths(-11)));
        var oldAnonymous = await AuditAsync(LoginAudit.Failure(null, LoginAuditMethod.Code, LoginCodeErrors.Expired,
            nowUtc.AddMonths(-13)));
        var recentAnonymous = await AuditAsync(LoginAudit.Failure(null, LoginAuditMethod.Code, LoginCodeErrors.Expired,
            nowUtc.AddMonths(-11)));

        await RunAsync();
        await RunAsync();

        // «Último ingreso» de Cuentas sigue mostrando la fecha de quien no entra hace más de un año.
        Assert.False(await ExistsAsync<LoginAudit>(oldest.Id));
        Assert.True(await ExistsAsync<LoginAudit>(lastOfDormant.Id));
        Assert.False(await ExistsAsync<LoginAudit>(failedOfDormant.Id));
        Assert.False(await ExistsAsync<LoginAudit>(oldOfActive.Id));
        Assert.True(await ExistsAsync<LoginAudit>(lastOfActive.Id));
        Assert.False(await ExistsAsync<LoginAudit>(oldAnonymous.Id));
        Assert.True(await ExistsAsync<LoginAudit>(recentAnonymous.Id));
        await using var scope = factory.Services.CreateAsyncScope();
        var lastSignIn = await scope.ServiceProvider.GetRequiredService<ILoginAuditRepository>()
            .FindLastSuccessAtUtcAsync(dormant, Ct);
        Assert.NotNull(lastSignIn);
        Assert.Equal(lastOfDormant.OccurredAtUtc, lastSignIn.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Sessions_are_pruned_fourteen_days_after_they_expired_or_were_revoked()
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var subject = Guid.NewGuid().ToString("D");
        var expiredLongAgo = await TokenAsync(subject, createdAtUtc: nowUtc.AddDays(-75), expiresAtUtc: nowUtc.AddDays(-45));
        var expiredRecently = await TokenAsync(subject, createdAtUtc: nowUtc.AddDays(-20), expiresAtUtc: nowUtc.AddDays(-5));
        var stillValid = await TokenAsync(subject, createdAtUtc: nowUtc.AddDays(-1), expiresAtUtc: nowUtc.AddDays(29));
        var revoked = await AuthorizationAsync(subject, nowUtc.AddDays(-75), Statuses.Revoked);
        var current = await AuthorizationAsync(subject, nowUtc.AddDays(-1), Statuses.Valid);

        await RunAsync();

        Assert.False(await ExistsAsync<OpenIddictEntityFrameworkCoreToken<Guid>>(expiredLongAgo));
        Assert.True(await ExistsAsync<OpenIddictEntityFrameworkCoreToken<Guid>>(expiredRecently));
        Assert.True(await ExistsAsync<OpenIddictEntityFrameworkCoreToken<Guid>>(stillValid));
        Assert.False(await ExistsAsync<OpenIddictEntityFrameworkCoreAuthorization<Guid>>(revoked));
        Assert.True(await ExistsAsync<OpenIddictEntityFrameworkCoreAuthorization<Guid>>(current));
    }

    [Fact]
    public async Task Message_quota_counters_are_removed_once_they_left_their_24_hour_window()
    {
        using var client = factory.CreateClient();
        var nowUtc = Now();
        var key = "retention-" + Guid.NewGuid().ToString("N");
        var old = nowUtc.AddHours(-26);
        var counting = MessageQuotaWindow.FirstHour(nowUtc);
        old = new DateTime(old.Year, old.Month, old.Day, old.Hour, 0, 0, DateTimeKind.Utc);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database;
            foreach (var hour in new[] { old, counting })
                await database.ExecuteSqlAsync($"""
                    INSERT INTO identity."MessageQuotaBuckets" ("Scope", "KeyHash", "HourUtc", "Count")
                    VALUES ('Global', {key}, {hour}, 3)
                    """, Ct);
        }

        await RunAsync();

        await using var read = factory.Services.CreateAsyncScope();
        var left = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().MessageQuotaBuckets.AsNoTracking()
            .Where(bucket => bucket.KeyHash == key).Select(bucket => bucket.HourUtc).ToListAsync(Ct);
        Assert.Equal([counting], left);
    }

    private async Task RunAsync() =>
        await ActivatorUtilities.CreateInstance<IdentityRetentionWorker>(factory.Services).RunOnceAsync(Ct);

    private DateTime Now() => factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

    private async Task<Guid> IssueCodeAsync(DateTime issuedAtUtc)
    {
        var destination = LoginCodeDestination.ForEmail(Email.Create($"retention-{Guid.NewGuid():N}@example.test").Value);
        var code = LoginCode.Issue(destination, LoginCodePurpose.Login, null, "hash", issuedAtUtc,
            TimeSpan.FromMinutes(10), maxAttempts: 3);
        await WriteAsync(services => services.GetRequiredService<ILoginCodeRepository>().Add(code));
        return code.Id;
    }

    private async Task<LoginAudit> AuditAsync(LoginAudit audit)
    {
        await WriteAsync(services => services.GetRequiredService<ILoginAuditRepository>().Add(audit));
        return audit;
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userId = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async work =>
        {
            userId = (await services.GetRequiredService<IUserRepository>().CreateAsync("Retención", "es-AR",
                "America/Argentina/Buenos_Aires", work)).Id;
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return userId;
    }

    private async Task<Guid> TokenAsync(string subject, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        var token = new OpenIddictEntityFrameworkCoreToken<Guid>
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            Type = TokenTypeIdentifiers.RefreshToken,
            Status = Statuses.Valid,
            CreationDate = createdAtUtc,
            ExpirationDate = expiresAtUtc,
        };
        await WriteAsync(services => services.GetRequiredService<ApplicationDbContext>().Add(token));
        return token.Id;
    }

    private async Task<Guid> AuthorizationAsync(string subject, DateTime createdAtUtc, string status)
    {
        var authorization = new OpenIddictEntityFrameworkCoreAuthorization<Guid>
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            Type = AuthorizationTypes.AdHoc,
            Status = status,
            CreationDate = createdAtUtc,
        };
        await WriteAsync(services => services.GetRequiredService<ApplicationDbContext>().Add(authorization));
        return authorization.Id;
    }

    private async Task WriteAsync(Action<IServiceProvider> write)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            write(services);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private async Task<bool> ExistsAsync<T>(Guid id) where T : class
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<T>()
            .AnyAsync(row => EF.Property<Guid>(row, "Id") == id, Ct);
    }
}
