using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Models.Request;

/// <summary>Binding técnico de una operación del servidor; no concede permisos ni sustituye la autorización vigente.</summary>
public sealed record ExecutionActor(Guid OwnerUserId, Access Access, Guid? TenantId, string Culture, string TimeZoneId)
{
    /// <summary>Clase leída de la organización real por la fábrica confiable, nunca inferida de un argumento del modelo.</summary>
    public TenantKind? TenantKind { get; init; }

    public DateTime? SessionStartedAtUtc { get; init; }
}
