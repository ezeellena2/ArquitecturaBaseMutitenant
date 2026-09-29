using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed técnico global en un solo límite y detrás de un lock común para todas las réplicas.
/// Es la excepción nominal, junto a ReferenceDataSeeder, al límite de servicios Application.
/// </summary>
internal sealed class DatabaseSeeder(
    IUnitOfWork unitOfWork,
    ApplicationDbContext context,
    JsonReferenceDataCatalog referenceSource,
    ReferenceDataSeeder referenceData,
    ReferenceDataCache referenceCache,
    OpenIddictSeeder openIddict,
    PlatformSeeder platform,
    LegalDocumentSeeder legal,
    DevelopmentSeeder development,
    ITenantScope tenantScope,
    IHostEnvironment environment)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Cargar los cinco archivos antes de ocupar el lock de la base.
        var snapshot = await ReferenceDataSeedSnapshot.LoadAsync(referenceSource, cancellationToken);
        var owner = platform.ReadOwner();
        if (!environment.IsDevelopment())
        {
            await SeedCoreAsync(snapshot, owner, null, cancellationToken);
            return;
        }

        // El scope debe preceder a la UoW. Si otra réplica ganó el lock con otro Id,
        // descartamos el candidato y releemos la organización antes de reintentar.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var candidate = await development.PrepareAsync(cancellationToken);
            using var scope = tenantScope.Enter(candidate.Tenant.Id);
            try
            {
                await SeedCoreAsync(snapshot, owner, candidate, cancellationToken);
                return;
            }
            catch (DevelopmentSeedScopeChangedException) when (attempt == 0)
            {
                // La UoW ya hizo rollback; el próximo intento toma el Id confirmado.
            }
        }

        throw new InvalidOperationException("The sample organization changed while seeding.");
    }

    private async Task SeedCoreAsync(ReferenceDataSeedSnapshot snapshot, PlatformOwnerSeed? owner,
        DevelopmentSeedCandidate? candidate, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Seed], ct);
            var referencesChanged = await referenceData.StageAsync(snapshot, ct);
            await openIddict.SeedAsync(ct);
            await platform.SeedAsync(owner, snapshot, ct);
            await legal.SeedAsync(snapshot.Cultures, ct);
            if (candidate is not null)
            {
                await development.SeedAsync(candidate, snapshot, ct);
            }

            return Result.Success(referencesChanged);
        }, CommitPolicy.OnSuccess, cancellationToken);

        if (result.Value)
        {
            await referenceCache.InvalidateAsync(cancellationToken);
        }
    }
}
