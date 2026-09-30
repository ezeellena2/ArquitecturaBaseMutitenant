using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed técnico global en un límite, igual en todos los ambientes.
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
    LegalDocumentSeeder legal)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Cargar los cinco archivos antes de ocupar el lock de la base.
        var snapshot = await ReferenceDataSeedSnapshot.LoadAsync(referenceSource, cancellationToken);
        var owner = platform.ReadOwner();
        await SeedCoreAsync(snapshot, owner, cancellationToken);
    }

    private async Task SeedCoreAsync(ReferenceDataSeedSnapshot snapshot, PlatformOwnerSeed? owner,
        CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Seed], ct);
            var referencesChanged = await referenceData.StageAsync(snapshot, ct);
            await openIddict.SeedAsync(ct);
            await platform.SeedAsync(owner, snapshot, ct);
            await legal.SeedAsync(snapshot.Cultures, ct);
            return Result.Success(referencesChanged);
        }, CommitPolicy.OnSuccess, cancellationToken);

        if (result.Value)
        {
            await referenceCache.InvalidateAsync(cancellationToken);
        }
    }
}
