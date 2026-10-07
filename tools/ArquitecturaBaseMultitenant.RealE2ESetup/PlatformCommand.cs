using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>Prepara estados iniciales exclusivos de PostgreSQL efímero; el recorrido opera después por la UI.</summary>
internal static class PlatformCommand
{
    internal static string Phase { get; private set; } = "not-started";
    public static async Task TryExecuteAsync(IServiceProvider services, string readyFile, Guid businessId,
        Guid ownerId, CancellationToken ct)
    {
        var commandFile = Path.ChangeExtension(readyFile, ".platform.json");
        var completedFile = commandFile + ".done";
        if (!File.Exists(commandFile) || File.Exists(completedFile)) return;
        using var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandFile, ct));
        if (command.RootElement.GetProperty("kind").GetString() != "prepare")
            throw new InvalidOperationException("Unsupported isolated platform command.");
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        // Cada recorrido tiene su cuenta real: la cuota por destino no debe depender
        // de los códigos consumidos por otro caso del mismo E2E completo.
        foreach (var key in new[] { "reduced", "a2", "5a", "a1", "a3", "a8", "5b", "5c", "5d" })
        {
            Phase = $"operator-{key}";
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var limited = key is "reduced" or "a2";
                var users = provider.GetRequiredService<IUserRepository>();
                var user = await users.CreateAsync(limited ? "Operador reducido E2E" : $"Operador E2E {key}",
                    "es-AR", "America/Argentina/Buenos_Aires", token);
                var email = Email.Create(key == "reduced" ? "e2e-reduced@example.test"
                    : $"e2e-{key}-operator@example.test").Value;
                var method = LoginMethod.CreateEmail(user.Id, email);
                method.Verify(nowUtc);
                method.MakePrimary();
                provider.GetRequiredService<ILoginMethodRepository>().Add(method);
                await users.SetPrimaryEmailAsync(user.Id, email, token);
                await users.SetPlatformOperatorAsync(user.Id, true, token);
                provider.GetRequiredService<IPlatformPermissionAssignmentRepository>().Add(
                    PlatformPermissionAssignment.Create(user.Id,
                        limited ? [PlatformPermissions.Tenants.Read] : PlatformPermissions.All).Value);
                var legal = provider.GetRequiredService<ILegalRepository>();
                foreach (var kind in Enum.GetValues<LegalDocumentKind>())
                {
                    var current = await legal.GetCurrentDocumentAsync(kind, nowUtc, token)
                        ?? throw new InvalidOperationException("Current legal documents are required.");
                    legal.AddAcceptance(LegalAcceptance.Create(user.Id, current, nowUtc, null, null));
                }
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        var tenantScope = provider.GetRequiredService<ITenantScope>();
        foreach (var pending in new[] { true, false })
        {
            Phase = pending ? "pending-organization" : "interrupted-organization";
            var tenant = Tenant.CreateBusiness(pending ? "Pendiente T25" : "Alta interrumpida T25", pending);
            using var lease = tenantScope.Enter(tenant.Id);
            await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                tenant.RecordRegistration(ownerId, TenantRegistrationSource.SelfService, nowUtc);
                var company = Company.Create("Empresa inicial T25", null, "UTC").Value;
                provider.GetRequiredService<ICompanyRepository>().Add(company);
                return await provider.GetRequiredService<TenantProvisioner>().StageAsync(tenant,
                    TenantSettings.Create("es-AR", "UTC", "ARS"), [ownerId], ownerId, token, company.Id);
            }, CommitPolicy.OnSuccess, ct);
        }
        // La inserción de datos públicos exige Business clasificado: Enter es un alcance técnico sin Kind.
        await using (var businessScope = services.CreateAsyncScope())
        {
            Phase = "public-page-domain";
            var businessProvider = businessScope.ServiceProvider;
            businessProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(businessId, TenantKind.Business);
            await businessProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async token =>
            {
                var business = await businessProvider.GetRequiredService<ITenantRepository>().GetByIdAsync(businessId, token)
                    ?? throw new InvalidOperationException("The isolated business is required.");
                business.SetSlug(TenantSlug.Create("empresa-t25").Value);
                var page = PublicPage.Create();
                page.UpdateContent("Empresa E2E", null, null, null, null, null);
                page.Publish(nowUtc);
                businessProvider.GetRequiredService<IPublicPageRepository>().Add(page);
                var domain = TenantDomain.Create(businessId, OrganizationEmailDomain.Create("corp.example.test").Value,
                    "isolatedT25", nowUtc).Value;
                domain.Verify(nowUtc);
                businessProvider.GetRequiredService<ITenantDomainRepository>().Add(domain);
                var managed = LoginMethod.CreateEmail(ownerId, Email.Create("e2e-managed@corp.example.test").Value);
                managed.Verify(nowUtc);
                managed.AssignManagedByOrganization(businessId);
                businessProvider.GetRequiredService<ILoginMethodRepository>().Add(managed);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct);
        }
        await File.WriteAllTextAsync(completedFile, JsonSerializer.Serialize(new { prepared = true }), ct);
        Phase = "complete";
    }
}
