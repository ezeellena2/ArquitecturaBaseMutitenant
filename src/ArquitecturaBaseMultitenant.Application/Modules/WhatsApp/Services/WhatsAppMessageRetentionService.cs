using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

internal sealed class WhatsAppMessageRetentionService(IWhatsAppMessageRepository messages, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<WhatsAppMessageRetentionService> logger) : IWhatsAppMessageRetentionService
{
    internal const int RetentionDays = 90;
    internal const int BatchSize = 100;
    public Task<Result<int>> ClearExpiredTextsAsync(CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RetainWhatsAppMessages", () => unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            var expired = await messages.ListExpiredTextAsync(clock.GetUtcNow().UtcDateTime.AddDays(-RetentionDays), BatchSize, c);
            foreach (var message in expired) message.ClearText();
            return Result.Success(expired.Count);
        }, CommitPolicy.OnSuccess, ct));
}
