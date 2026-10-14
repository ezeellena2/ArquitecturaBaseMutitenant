using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// Las plantillas de la organización del claim o las «Para consultas» de Plataforma, con lo que Meta dice de ellas en las
/// cuentas de sus números vigentes: rechazada si alguna la rechazó, pausada si alguna la pausó, aprobada si todas la
/// aprobaron y, si no, pendiente. La categoría es la de Meta si ya la informó.
/// </summary>
public interface IWhatsAppTemplateReader
{
    Task<PagedResult<WhatsAppTemplateRow>> ListAsync(bool platform, WhatsAppTemplateListRequest request, CancellationToken ct);
    Task<int> CountAsync(bool platform, WhatsAppTemplateStatus? status, CancellationToken ct);
    Task<WhatsAppTemplateRow?> FindAsync(bool platform, Guid id, CancellationToken ct);
    /// <summary>Si ya hay una plantilla con ese nombre para Meta.</summary>
    Task<bool> NameTakenAsync(bool platform, string metaName, CancellationToken ct);
    /// <summary>Lo que Meta dice de esos nombres en la cuenta del número vigente de Plataforma (las plantillas del sistema).</summary>
    Task<WhatsAppPlatformTemplateStates> ListPlatformStatesAsync(IReadOnlyCollection<string> metaNames, CancellationToken ct);
}

/// <summary>Los estados de Meta en la cuenta de Plataforma y desde cuándo está conectado su número (null sin número).</summary>
public sealed record WhatsAppPlatformTemplateStates(DateTime? ConnectedAtUtc, IReadOnlyList<WhatsAppTemplateStateRow> States);

public sealed record WhatsAppTemplateRow(Guid Id, string Name, string MetaName, string Language, string MetaLanguage, WhatsAppTemplateCategory Category,
    string Body, IReadOnlyList<WhatsAppTemplateVariable> Variables, WhatsAppTemplateStatus Status, string? RejectionReason, DateTime UpdatedAtUtc,
    uint Version);

public sealed record WhatsAppTemplateStateRow(string MetaName, string MetaLanguage, WhatsAppTemplateStatus Status, WhatsAppTemplateCategory Category,
    string? RejectionReason, DateTime UpdatedAtUtc);
