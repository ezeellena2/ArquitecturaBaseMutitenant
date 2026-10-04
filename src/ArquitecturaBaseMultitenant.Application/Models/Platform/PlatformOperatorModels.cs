using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>Invitar un operador: un correo sin cuenta y sus permisos, elegidos uno por uno.</summary>
public sealed record InvitePlatformOperatorRequest(Email? Email, IReadOnlyList<string>? Permissions)
{
    public override string ToString() => nameof(InvitePlatformOperatorRequest);
}

/// <summary>La cuenta del operador invitado: aparece enseguida en Cuentas.</summary>
public sealed record PlatformOperatorInvitationResponse(Guid Id);

/// <summary>Cambiar los permisos de un operador: versión de su asignación, motivo y los permisos nuevos.</summary>
public sealed record ChangePlatformOperatorRequest(uint? Version, string? Reason, IReadOnlyList<string>? Permissions)
{
    public override string ToString() => nameof(ChangePlatformOperatorRequest);
}
