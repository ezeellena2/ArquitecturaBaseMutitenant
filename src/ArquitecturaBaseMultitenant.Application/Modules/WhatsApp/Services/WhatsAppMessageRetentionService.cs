using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;
namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// La retención de WhatsApp. La copia global de la entrada (<c>identity.WhatsAppMessages</c>) vence a los 90 días fijos desde
/// que llegó: es técnica y además se purga al pasar a la organización. Los chats vencen con el plazo de su dueño (272): el de la
/// organización del alcance, recortado al máximo de la plataforma, o el del número de Plataforma. Un mensaje vencido queda como
/// hecho sin contenido; la vista previa y la copia vinculada vencen con él. Cada llamada es un lote en su transacción, y repetirla
/// no cambia lo ya borrado.
/// </summary>
internal sealed class WhatsAppMessageRetentionService(IWhatsAppMessageRepository messages, IUnitOfWork unitOfWork,
    TimeProvider clock, ILogger<WhatsAppMessageRetentionService> logger, IWhatsAppCodeQuotaRepository quotas,
    IWhatsAppChatRetentionRepository chats, WhatsAppSettingsResolver settings) : IWhatsAppMessageRetentionService
{
    internal const int RetentionDays = 90;
    internal const int BatchSize = 100;
    public Task<Result<int>> ClearExpiredTextsAsync(CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RetainWhatsAppMessages", () => unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            var expired = await messages.ListExpiredTextAsync(clock.GetUtcNow().UtcDateTime.AddDays(-RetentionDays), BatchSize, c);
            foreach (var message in expired) message.ClearText();
            var expiredIps = await quotas.ListExpiredIpsAsync(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), BatchSize, c);
            foreach (var ip in expiredIps) quotas.Remove(ip);
            return Result.Success(expired.Count + expiredIps.Count);
        }, CommitPolicy.OnSuccess, ct));

    public Task<Result<int>> ClearExpiredOrganizationChatsAsync(CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RetainWhatsAppOrganizationChats", () => unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            var beforeUtc = clock.GetUtcNow().UtcDateTime.AddDays(-await settings.OrganizationChatRetentionDaysAsync(c));
            var expired = await chats.ListExpiredMessagesAsync(beforeUtc, BatchSize, c);
            foreach (var message in expired) message.ClearContent();
            var previews = await chats.ListExpiredPreviewsAsync(beforeUtc, BatchSize, c);
            foreach (var chat in previews) chat.ClearPreview();
            var links = await chats.ListExpiredLinkPreviewsAsync(beforeUtc, BatchSize, c);
            foreach (var link in links) link.ClearPreview();
            var copies = await chats.ListExpiredLinkedMessagesAsync(beforeUtc, BatchSize, c);
            foreach (var copy in copies) copy.ClearText();
            return Result.Success(expired.Count + previews.Count + links.Count + copies.Count);
        }, CommitPolicy.OnSuccess, ct));

    public Task<Result<int>> ClearExpiredPlatformChatsAsync(CancellationToken ct) =>
        OperationLog.RunAsync(logger, clock, "RetainPlatformWhatsAppChats", () => unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            var beforeUtc = clock.GetUtcNow().UtcDateTime.AddDays(-(await settings.PlatformAsync(c)).ChatRetentionDays);
            var expired = await chats.ListExpiredPlatformMessagesAsync(beforeUtc, BatchSize, c);
            foreach (var message in expired) message.ClearContent();
            var previews = await chats.ListExpiredPlatformPreviewsAsync(beforeUtc, BatchSize, c);
            foreach (var chat in previews) chat.ClearPreview();
            return Result.Success(expired.Count + previews.Count);
        }, CommitPolicy.OnSuccess, ct));
}
