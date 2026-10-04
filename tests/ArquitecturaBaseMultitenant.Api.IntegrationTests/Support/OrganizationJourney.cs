using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Arma una organización con su Dueño y le suma miembros, empresas e invitaciones por la API real.</summary>
internal static class OrganizationJourney
{
    internal static async Task<Guid> StageAsync(ApiFactory factory, AccountJourney owner, string name,
        CancellationToken ct)
    {
        var tenant = Tenant.CreateBusiness(name, requiresApproval: false);
        Assert.True(tenant.Activate().IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant.Id);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<TenantSpaceProvisioner>().Stage(tenant,
                TenantSettings.Create("es-AR", "America/Argentina/Buenos_Aires", "ARS"), [owner.UserId], owner.UserId);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, ct);
        return tenant.Id;
    }

    /// <summary>Suma una cuenta como miembro activo, sin roles.</summary>
    internal static async Task<Guid> AddActiveMemberAsync(ApiFactory factory, Guid tenantId, Guid userId,
        CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var member = Member.Invite(userId);
        Assert.True(member.Activate(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<IMemberRepository>().Add(member);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, ct);
        return member.Id;
    }

    internal static async Task<Guid> CreateCompanyAsync(HttpClient client, string name, CancellationToken ct)
    {
        using var created = await AccountJourney.PostAsync(client, "/api/companies", new
        {
            name,
            taxIdNumber = (string?)null,
            timeZoneId = (string?)null,
        }, ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(ct));
        return await created.Content.ReadFromJsonAsync<Guid>(ct);
    }

    /// <summary>Invita un correo sin roles y deja la invitación vencida hace dos días.</summary>
    internal static async Task InviteExpiredAsync(ApiFactory factory, HttpClient client, Guid tenantId, string email,
        CancellationToken ct)
    {
        using var issued = await AccountJourney.PostAsync(client, "/api/users/invitations", new
        {
            email,
            roleIds = Array.Empty<Guid>(),
            companies = Array.Empty<object>(),
        }, ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(ct));
        using var json = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(ct));
        var invitationId = json.RootElement.GetProperty("invitationId").GetGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var expiredAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddDays(-2);
        var updated = await services.GetRequiredService<ApplicationDbContext>().Invitations
            .Where(invitation => invitation.Id == invitationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invitation => invitation.ExpiresAtUtc, expiredAtUtc), ct);
        Assert.Equal(1, updated);
    }

    internal static async Task RegisterDomainAsync(HttpClient client, string domain, CancellationToken ct)
    {
        using var registered = await AccountJourney.PostAsync(client, "/api/organization/settings/domains",
            new { domain }, ct);
        Assert.True(registered.StatusCode == HttpStatusCode.Created, await registered.Content.ReadAsStringAsync(ct));
    }

    internal static async Task<JsonElement> HomeAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync("/api/home", ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.Clone();
    }
}
