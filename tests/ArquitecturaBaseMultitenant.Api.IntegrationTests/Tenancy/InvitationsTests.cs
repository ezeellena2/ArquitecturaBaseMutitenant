using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>Prueba invitaciones y membresías sin cuenta sobre PostgreSQL aislado, filtros EF y RLS reales.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class InvitationsTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Unbound_member_is_private_and_enters_the_access_index_only_after_binding()
    {
        using var client = factory.CreateClient();
        var (tenantId, userId) = await CreateOrganizationAsync();
        var member = Member.Invite();
        var invitation = Invitation.Issue(member.Id, userId, Email.Create("recipient@example.test").Value,
            InvitationChannel.Email, "private-hash", new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromDays(7));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<IMemberRepository>().Add(member);
                services.GetRequiredService<IInvitationRepository>().Add(invitation);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess);
        }

        Assert.Equal(0, await CountAccessesAsync(userId, tenantId));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            Assert.NotNull(await services.GetRequiredService<IInvitationReader>().FindByIdAsync(invitation.Id, Ct));
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var stored = await services.GetRequiredService<IMemberRepository>().GetByIdAsync(member.Id, ct);
                Assert.NotNull(stored);
                Assert.Null(stored.UserId);
                Assert.True(stored.AssignUser(userId).IsSuccess);
                Assert.True(stored.Activate(new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc)).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess);
        }

        Assert.Equal(1, await CountAccessesAsync(userId, tenantId));
    }

    [Fact]
    public async Task Another_organization_cannot_read_the_invitation_through_reader_or_runtime_sql()
    {
        using var client = factory.CreateClient();
        var (firstTenant, inviter) = await CreateOrganizationAsync();
        var (otherTenant, _) = await CreateOrganizationAsync();
        var member = Member.Invite();
        var invitation = Invitation.Issue(member.Id, inviter, Email.Create("private@example.test").Value,
            InvitationChannel.Email, "isolation-hash", new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromDays(7));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(firstTenant);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<IMemberRepository>().Add(member);
                services.GetRequiredService<IInvitationRepository>().Add(invitation);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(otherTenant);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IInvitationReader>()
                .FindByIdAsync(invitation.Id, Ct));
        }
        await using var connection = await RuntimeRoleConnection.OpenAsync(factory.RuntimeConnectionString, otherTenant, Ct);
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM tenant.\"Invitations\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", invitation.Id);
        Assert.Equal(0L, await command.ExecuteScalarAsync(Ct));
    }

    private async Task<(Guid TenantId, Guid UserId)> CreateOrganizationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var tenant = Tenant.CreateBusiness("Invitation test", requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        var userId = Guid.Empty;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            userId = (await services.GetRequiredService<IUserRepository>()
                .CreateAsync("Inviter", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id;
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct);
        return (tenant.Id, userId);
    }

    private async Task<long> CountAccessesAsync(Guid userId, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(factory.RuntimeConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*) FROM identity."UserTenantAccesses" WHERE "UserId" = @user_id AND "TenantId" = @tenant_id
            """, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
