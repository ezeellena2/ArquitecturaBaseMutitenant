using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Retention;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// ADR 0062: la retención de los datos globales del ingreso (identidad.md). Cada método es un lote o un tramo en su
/// propia transacción corta, en el scope global, sin una organización ni un rol que saltee el aislamiento. Borrar es
/// idempotente: dos copias que corren a la vez borran a lo sumo lo mismo, y un apagado a mitad deshace solo ese lote.
/// </summary>
internal sealed class IdentityRetentionService(
    IIdentityRetentionRepository retention,
    IOpenIddictPruner sessions,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<IdentityRetentionService> logger) : IIdentityRetentionService
{
    public Task<Result<int>> PurgeLoginCodesAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "PurgeLoginCodes", () =>
            unitOfWork.ExecuteInTransactionAsync(async ct => Result.Success(await retention.DeleteLoginCodesAsync(
                    IdentityRetentionPolicy.LoginCodesBeforeUtc(NowUtc(), IdentityRetentionPeriods.Default),
                    IdentityRetentionPolicy.BatchSize, ct)),
                CommitPolicy.OnSuccess, cancellationToken));

    public Task<Result<RetentionBatch>> PurgeLoginAuditsAsync(RetentionCursor? after, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "PurgeLoginAudits", () =>
            unitOfWork.ExecuteInTransactionAsync(async ct => Result.Success(await retention.DeleteLoginAuditsAsync(
                    IdentityRetentionPolicy.LoginAuditsBeforeUtc(NowUtc(), IdentityRetentionPeriods.Default), after,
                    IdentityRetentionPolicy.BatchSize, ct)),
                CommitPolicy.OnSuccess, cancellationToken));

    public Task<Result<OpenIddictPruneTranche>> PruneSessionsAsync(DateTime? fromUtc,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "PruneSessions", () =>
            unitOfWork.ExecuteInTransactionAsync(async ct => Result.Success(await sessions.PruneTrancheAsync(
                    IdentityRetentionPolicy.SessionsExpiredBeforeUtc(NowUtc()), fromUtc, ct)),
                CommitPolicy.OnSuccess, cancellationToken));

    public Task<Result<int>> PurgeMessageQuotaBucketsAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "PurgeMessageQuotaBuckets", () =>
            unitOfWork.ExecuteInTransactionAsync(async ct => Result.Success(await retention.DeleteMessageQuotaBucketsAsync(
                    MessageQuotaWindow.PurgeBefore(NowUtc()), IdentityRetentionPolicy.BatchSize, ct)),
                CommitPolicy.OnSuccess, cancellationToken));

    private DateTime NowUtc() => timeProvider.GetUtcNow().UtcDateTime;
}
