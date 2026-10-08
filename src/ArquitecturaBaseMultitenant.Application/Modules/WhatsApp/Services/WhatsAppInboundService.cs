using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;
internal sealed class WhatsAppInboundService(IWhatsAppMessageRepository messages, WhatsAppInboundGuard guard,
    IWhatsAppInboundSettings settings, IEnumerable<IWhatsAppInboundConsumer> consumers, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<WhatsAppInboundService> logger) : IWhatsAppInboundService
{
    public Task<Result<WhatsAppInboundClaim?>> ClaimAsync(CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ClaimWhatsAppInbound", () =>
            unitOfWork.ExecuteInTransactionAsync(async c =>
            {
                if (!settings.IsEnabled || !consumers.Any()) return Result.Success<WhatsAppInboundClaim?>(null);
                return Result.Success(await messages.ClaimNextAsync(clock.GetUtcNow().UtcDateTime, settings.LeaseLifetime, settings.MaxAttempts, c));
            }, CommitPolicy.OnSuccess, ct));
    public Task<Result> ProcessAsync(WhatsAppInboundClaim claim, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "ProcessWhatsAppInbound", () =>
            unitOfWork.ExecuteInTransactionAsync(async c =>
            {
                var consumer = consumers.SingleOrDefault();
                if (!settings.IsEnabled || consumer is null) return Result.Success();
                var context = await guard.LockAsync(claim, c);
                if (context is null) return Result.Success();
                await consumer.ConsumeAsync(context, c);
                // La fila sigue bloqueada, pero el consumidor también puede agotar el lease.
                if (!context.Message.Complete(claim.LeaseId, clock.GetUtcNow().UtcDateTime))
                    return Result.Failure(Domain.WhatsApp.WhatsAppErrors.LeaseExpired);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct));
    public Task<Result> FailAsync(WhatsAppInboundClaim claim, CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "FailWhatsAppInbound", () =>
            unitOfWork.ExecuteInTransactionAsync(async c =>
            {
                var message = await messages.GetForUpdateAsync(claim.MessageId, c);
                message?.Fail(claim.LeaseId, clock.GetUtcNow().UtcDateTime, settings.RetryDelay, settings.MaxAttempts);
                return Result.Success();
            }, CommitPolicy.OnSuccess, ct));
}
