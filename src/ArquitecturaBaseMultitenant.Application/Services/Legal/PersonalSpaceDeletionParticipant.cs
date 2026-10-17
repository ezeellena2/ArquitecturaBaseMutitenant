using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Cierra el espacio Personal y retira sus ajustes y membresías durante la baja de la cuenta. Solo actúa dentro del alcance de ese tenant y de la transacción del worker.</summary>
internal sealed class PersonalSpaceDeletionParticipant(IAccountDeletionCleanupRepository cleanup, ITenantContext tenantContext)
    : IAccountDeletionParticipant
{
    public Task<IReadOnlyList<Error>> CheckAsync(AccountDeletionContext context, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Error>>([]);
    public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;

    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantKind != TenantKind.Personal || context.TenantId is not { } tenantId) return;
        cleanup.RequireTransaction();
        if (tenantContext.RequiredTenantId != tenantId) throw new InvalidOperationException("The personal deletion needs its tenant scope.");
        var tenant = await cleanup.GetPersonalSpaceAsync(tenantId, ct);
        if (tenant.Status != TenantStatus.Closed && tenant.Close().IsFailure)
            throw new InvalidOperationException("The personal space could not be closed.");
        await cleanup.RemovePersonalDataAsync(ct);
    }
}
