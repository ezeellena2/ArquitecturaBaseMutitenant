using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Señala el fallo provocado por el decorador transaccional del test. Permite distinguir esa falla esperada
/// de una excepción ajena a la comprobación del rollback.
/// </summary>
internal sealed class ExpectedCommitFailure : Exception;

/// <summary>
/// Observa el estado de la transacción y del ChangeTracker alrededor de una falla de commit provocada por el test.
/// </summary>
internal sealed class CommitFailureProbe
{
    public Func<ApplicationDbContext, CancellationToken, Task>? BeforeFailing { get; init; }

    public bool RolledBackBeforeLeaving { get; set; }
}

/// <summary>Fuerza una falla después de guardar para comprobar el rollback de la unidad de trabajo real.</summary>
internal sealed class FailingCommitUnitOfWork(
    IUnitOfWork inner,
    ApplicationDbContext db,
    CommitFailureProbe probe) : IUnitOfWork
{
    public static void Replace(IServiceCollection services, CommitFailureProbe probe)
    {
        var registration = services.LastOrDefault(service => service.ServiceType == typeof(IUnitOfWork))
            ?? throw new InvalidOperationException("IUnitOfWork must be registered before the test decorator.");
        services.RemoveAll<IUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => new FailingCommitUnitOfWork(
            CreateInner(provider, registration), provider.GetRequiredService<ApplicationDbContext>(), probe));
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CommitPolicy policy,
        CancellationToken cancellationToken)
        where TResult : Result
    {
        try
        {
            return await inner.ExecuteInTransactionAsync(async ct =>
            {
                var result = await work(ct);
                if (policy.Commits(result))
                {
                    Assert.NotNull(db.Database.CurrentTransaction);
                    await db.SaveChangesAsync(ct);
                    if (probe.BeforeFailing is { } check)
                    {
                        await check(db, ct);
                    }

                    throw new ExpectedCommitFailure();
                }

                return result;
            }, policy, cancellationToken);
        }
        catch (ExpectedCommitFailure)
        {
            probe.RolledBackBeforeLeaving = db.Database.CurrentTransaction is null &&
                !db.ChangeTracker.Entries().Any();
            throw;
        }
    }

    private static IUnitOfWork CreateInner(IServiceProvider provider, ServiceDescriptor registration)
    {
        if (registration.ImplementationFactory is { } factory)
        {
            return (IUnitOfWork)factory(provider);
        }

        if (registration.ImplementationType is { } type)
        {
            return (IUnitOfWork)ActivatorUtilities.CreateInstance(provider, type);
        }

        return registration.ImplementationInstance as IUnitOfWork
            ?? throw new InvalidOperationException("Unsupported IUnitOfWork registration.");
    }
}
