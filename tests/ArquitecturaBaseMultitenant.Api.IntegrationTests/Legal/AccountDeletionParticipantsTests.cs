using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Legal;

/// <summary>
/// Comprueba que cada participante limpie únicamente los datos de la cuenta eliminada. Repetir la limpieza
/// no debe ampliar el alcance ni fallar por datos ya retirados.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountDeletionParticipantsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Current_participants_clean_only_the_account_data_and_can_run_twice()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AccountDeletionTest/1.0");
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        var work = services.GetRequiredService<IUnitOfWork>();
        var tenantScope = services.GetRequiredService<ITenantScope>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var business = Tenant.CreateBusiness("Empresa prueba baja", false);
        business.Activate();
        using (tenantScope.Enter(business.Id))
            await work.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [account.UserId]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        var ownedMessage = OutboxMessage.Enqueue("test", "owned", nowUtc, account.UserId);
        var otherMessage = OutboxMessage.Enqueue("test", "other", nowUtc, Guid.CreateVersion7());
        await work.ExecuteInTransactionAsync(ct =>
        {
            context.OutboxMessages.AddRange(ownedMessage, otherMessage);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var participants = services.GetServices<IAccountDeletionParticipant>().ToArray();
        Assert.Equal(4, participants.Length);
        Assert.All(await context.LegalAcceptances.AsNoTracking().Where(row => row.UserId == account.UserId)
            .ToArrayAsync(Ct), row => Assert.Equal("AccountDeletionTest/1.0", row.UserAgent));
        var global = new AccountDeletionContext(account.UserId, nowUtc);
        await work.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var participant in participants)
            {
                Assert.Empty(await participant.CheckAsync(global, ct));
                await participant.OnRequestedAsync(global, ct);
                await participant.OnCancelledAsync(global, ct);
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        Assert.Equal(OutboxStatus.Cancelled, ownedMessage.Status);
        Assert.Equal(OutboxStatus.Pending, otherMessage.Status);
        var targets = await services.GetRequiredService<IAccountDeletionTenantReader>().ListAsync(account.UserId, Ct);
        Assert.Equal(2, targets.Count);
        foreach (var target in targets)
        {
            using (tenantScope.Enter(target.TenantId))
            {
                for (var repeat = 0; repeat < 2; repeat++)
                    await work.ExecuteInTransactionAsync(async ct =>
                    {
                        foreach (var participant in participants)
                            await participant.ExecuteAsync(global with { TenantId = target.TenantId, TenantKind = target.Kind }, ct);
                        return Result.Success();
                    }, CommitPolicy.OnSuccess, Ct);
                if (target.Kind == TenantKind.Personal)
                {
                    Assert.Empty(await context.Members.AsNoTracking().ToArrayAsync(Ct));
                    Assert.Empty(await context.TenantSettings.AsNoTracking().ToArrayAsync(Ct));
                    Assert.Equal(TenantStatus.Closed, (await context.Tenants.SingleAsync(row => row.Id == target.TenantId, Ct)).Status);
                }
                else
                {
                    var member = await context.Members.AsNoTracking().SingleAsync(row => row.UserId == account.UserId, Ct);
                    Assert.Equal(MemberStatus.Removed, member.Status);
                    Assert.Equal(MemberRemovalReason.AccountDeleted, member.RemovalReason);
                    Assert.Single(await context.Set<AuditEntry>().AsNoTracking().Where(row => row.EntityId == member.Id
                        && row.Action == AuditAction.Custom).ToArrayAsync(Ct));
                    await work.ExecuteInTransactionAsync(async ct =>
                    {
                        // Aunque el alcance sea correcto, DELETE queda prohibido para una empresa.
                        Assert.Equal(0, await context.Database.ExecuteSqlInterpolatedAsync(
                            $"DELETE FROM tenant.\"Members\" WHERE \"UserId\" = {account.UserId}", ct));
                        Assert.Equal(0, await context.Database.ExecuteSqlRawAsync(
                            "DELETE FROM tenant.\"TenantSettings\"", ct));
                        return Result.Success();
                    }, CommitPolicy.OnSuccess, Ct);
                }
            }
        }
        await work.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var participant in participants) await participant.ExecuteAsync(global, ct);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        var acceptances = await context.LegalAcceptances.AsNoTracking().Where(row => row.UserId == account.UserId).ToArrayAsync(Ct);
        Assert.Equal(2, acceptances.Length);
        Assert.All(acceptances, row => { Assert.Null(row.IpAddress); Assert.Null(row.UserAgent); });
    }
}
