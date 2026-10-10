using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>Preparación E7 independiente de los bindings WhatsApp del host privado.</summary>
internal static class E7Preparation
{
    public static async Task<(Guid BusinessId, Guid UserId)> PrepareAsync(IServiceProvider rootServices,
        Email email, CancellationToken cancellationToken)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var personalSpaces = services.GetRequiredService<IPersonalSpaceProvisioner>();
        var draft = await personalSpaces.PrepareAsync(null, null, cancellationToken);
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var users = services.GetRequiredService<IUserRepository>();
        var methods = services.GetRequiredService<ILoginMethodRepository>();
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        var platformOperator = await services.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync("e2e-operator@example.test");
        if (platformOperator is null || !platformOperator.IsPlatformOperator)
            throw new InvalidOperationException("The isolated platform operator has not been seeded.");

        var created = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var user = await users.CreateAsync("Cuenta E2E", draft.Culture, draft.TimeZoneId, ct);
            var method = LoginMethod.CreateEmail(user.Id, email);
            method.Verify(nowUtc);
            if (method.MakePrimary().IsFailure)
                throw new InvalidOperationException("The E2E login method could not become primary.");
            methods.Add(method);
            await users.SetPrimaryEmailAsync(user.Id, email, ct);
            var legal = services.GetRequiredService<ILegalRepository>();
            foreach (var kind in Enum.GetValues<LegalDocumentKind>())
            {
                var document = await legal.GetCurrentDocumentAsync(kind, nowUtc, ct)
                    ?? throw new InvalidOperationException("The E2E legal documents have not been seeded.");
                legal.AddAcceptance(LegalAcceptance.Create(user.Id, document, nowUtc, null, null));
                // El recorrido del operador comienza con los términos vigentes ya aceptados.
                // Los recorridos de identidad prueban por pantalla los documentos y sus nuevas versiones.
                if (!await legal.HasAcceptedAsync(platformOperator.Id, document.Id, ct))
                    legal.AddAcceptance(LegalAcceptance.Create(platformOperator.Id, document, nowUtc, null, null));
            }
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, cancellationToken);

        var tenantScope = services.GetRequiredService<ITenantScope>();
        using (tenantScope.Enter(draft.Tenant.Id))
        {
            await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                personalSpaces.Stage(draft, created.Value);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, cancellationToken);
        }

        var business = Tenant.CreateBusiness("Empresa E2E", requiresApproval: false);
        if (business.Activate().IsFailure)
            throw new InvalidOperationException("The E2E organization could not be activated.");
        using (tenantScope.Enter(business.Id))
        {
            await unitOfWork.ExecuteInTransactionAsync(ct =>
            {
                services.GetRequiredService<TenantSpaceProvisioner>().Stage(business,
                    TenantSettings.Create(draft.Settings.DefaultCulture,
                        draft.Settings.DefaultTimeZoneId, draft.Settings.DefaultCurrency), [created.Value]);
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, cancellationToken);
        }

        await PublicSiteSetup.PrepareAsync(rootServices, cancellationToken);
        return (business.Id, created.Value);
    }
}
