using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Bandeja Recuperaciones: búsqueda por cuenta, correo o teléfono y filtro por estado.</summary>
public sealed record ListPlatformRecoveriesRequest : PagedRequest
{
    public static readonly IReadOnlyCollection<string> SortableFields = ["createdAt"];
    public AccountRecoveryStatus? Status { get; init; }
}

/// <summary>Fila de la bandeja: los métodos van enmascarados, como en el tablero; el motivo es interno.</summary>
public sealed record PlatformRecoveryResponse(Guid Id, DateTime CreatedAtUtc, Guid? AccountId, string? AccountName,
    LoginMethodType PreviousMethodType, string PreviousMethod, LoginMethodType NewMethodType, string NewMethod,
    AccountRecoveryStatus Status, DateTime? DecidedAtUtc, string? DecidedBy, string? Reason, uint Version);

public sealed record PlatformRecoveryStatusCount(AccountRecoveryStatus Status, int Count);

/// <summary>Identidad inmutable del pedido, proyectada antes de los locks sin cargar la entidad mutable.</summary>
public sealed record AccountRecoveryIdentityHint(Guid? UserId, LoginMethodType NewMethodType, string NewMethodValue)
{
    public override string ToString() => nameof(AccountRecoveryIdentityHint);
}
public sealed record PlatformRecoveryFilterCounts(int Total, IReadOnlyList<PlatformRecoveryStatusCount> Statuses);

/// <summary>Proyección del reader con los valores completos; el servicio los enmascara antes de responder.</summary>
public sealed record PlatformRecoveryRow(Guid Id, DateTime CreatedAtUtc, Guid? AccountId, string? AccountName,
    LoginMethodType PreviousMethodType, string PreviousMethodValue, LoginMethodType NewMethodType,
    string NewMethodValue, AccountRecoveryStatus Status, DateTime? DecidedAtUtc, string? DecidedBy, string? Reason,
    uint Version)
{
    public override string ToString() => nameof(PlatformRecoveryRow);
}
