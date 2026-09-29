using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed técnico global en un límite; Development agrega un límite por espacio tenant.
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
        await SeedCoreAsync(snapshot, owner, cancellationToken);
        if (!environment.IsDevelopment()) return;

        await SeedBusinessAsync(snapshot, cancellationToken);
        await SeedPersonalAsync(DevelopmentPerson.Kevin, cancellationToken);
        await SeedPersonalAsync(DevelopmentPerson.Carla, cancellationToken);
    }

    private async Task SeedBusinessAsync(ReferenceDataSeedSnapshot snapshot, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var candidate = await development.PrepareBusinessAsync(cancellationToken);
            using var scope = tenantScope.Enter(candidate.Tenant.Id);
            try
            {
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Seed], ct);
                    await development.StageBusinessAsync(candidate, snapshot, ct);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
                return;
            }
            catch (DevelopmentSeedScopeChangedException) when (attempt == 0)
            {
                // La UoW ya hizo rollback; el próximo intento toma el Id confirmado.
            }
        }

        throw new InvalidOperationException("The sample organization changed while seeding.");
    }

    private async Task SeedPersonalAsync(DevelopmentPerson person, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var candidate = await development.PreparePersonalAsync(person, cancellationToken);
            if (candidate is null) return;

            using var scope = tenantScope.Enter(candidate.Draft.Tenant.Id);
            try
            {
                await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    await context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.Seed], ct);
                    await development.StagePersonalAsync(candidate, ct);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
                return;
            }
            catch (DevelopmentSeedScopeChangedException) when (attempt == 0)
            {
                // La UoW hizo rollback; al reintentar se observa el Personal confirmado.
            }
        }

        throw new InvalidOperationException("The sample personal space changed while seeding.");
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
