using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>
/// Lee los equipos de la organización del claim o los de Plataforma, y resuelve miembros de la organización con su cuenta.
/// Los nombres de las personas salen del lector de nombres del núcleo.
/// </summary>
public interface IWhatsAppTeamReader
{
    /// <summary>Los equipos de la organización; con bandeja, solo los de esa bandeja. Orden por nombre.</summary>
    Task<PagedResult<WhatsAppTeamRow>> ListOrganizationAsync(WhatsAppInbox? inbox, PagedRequest request, CancellationToken ct);
    Task<WhatsAppTeamRow?> FindOrganizationAsync(Guid id, CancellationToken ct);
    Task<PagedResult<WhatsAppTeamRow>> ListPlatformAsync(PagedRequest request, CancellationToken ct);
    Task<WhatsAppTeamRow?> FindPlatformAsync(Guid id, CancellationToken ct);
    /// <summary>Los equipos que avisan por correo (D6): de la organización del claim o de Plataforma.</summary>
    Task<IReadOnlyList<WhatsAppTeamRow>> ListNotifyingAsync(bool platform, CancellationToken ct);
    /// <summary>Los miembros activos de la organización con esos identificadores, con su cuenta.</summary>
    Task<IReadOnlyList<WhatsAppTeamMemberRow>> ListActiveMembersAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct);
    /// <summary>El miembro activo de cada cuenta en la organización del claim.</summary>
    Task<IReadOnlyList<WhatsAppTeamMemberRow>> ListMembersOfAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);
}

/// <summary>Un equipo; en Plataforma la bandeja es Customers (sus consultas) y los integrantes son cuentas de operadores.</summary>
public sealed record WhatsAppTeamRow(Guid Id, string Name, WhatsAppInbox Inbox, IReadOnlyList<Guid> MemberIds, int? NotifyAfterMinutes, uint Version);

public sealed record WhatsAppTeamMemberRow(Guid MemberId, Guid UserId);
