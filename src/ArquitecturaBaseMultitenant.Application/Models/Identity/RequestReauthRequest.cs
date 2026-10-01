using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>
/// Indica qué acción sensible requiere demostrar la titularidad y, cuando corresponde, qué método de
/// ingreso se quiere cambiar. El servicio elige cómo emitir la prueba.
/// </summary>
public sealed record RequestReauthRequest(ReauthAction Action, Guid? TargetMethodId);
