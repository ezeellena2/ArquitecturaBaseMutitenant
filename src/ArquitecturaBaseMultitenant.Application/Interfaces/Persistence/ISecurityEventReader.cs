using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Lee la auditoría de seguridad global: platform.SecurityEvents y los ingresos de identity.LoginAudits, del más
/// nuevo al más viejo con un solo cursor. Los filtros se aplican en SQL.
/// </summary>
public interface ISecurityEventReader
{
    /// <summary>
    /// Una página de eventos e ingresos. <paramref name="eventTypes"/> null incluye todos los tipos y vacío, ninguno;
    /// <paramref name="includeSignIns"/> suma los ingresos. <paramref name="sinceUtc"/> acota el período.
    /// </summary>
    Task<CursorResult<PlatformAuditRow>> ListAsync(ListPlatformAuditRequest request, DateTime? sinceUtc,
        IReadOnlyCollection<SecurityEventType>? eventTypes, bool includeSignIns, CancellationToken ct);

    Task<PlatformAuditRow?> FindAsync(Guid id, CancellationToken ct);

    /// <summary>Organizaciones distintas de los eventos filtrados, sin aplicar TenantId ni la página del pedido.</summary>
    Task<IReadOnlyList<PlatformAuditOrganizationResponse>> ListOrganizationsAsync(ListPlatformAuditRequest request,
        DateTime? sinceUtc, IReadOnlyCollection<SecurityEventType>? eventTypes, CancellationToken ct);
}
