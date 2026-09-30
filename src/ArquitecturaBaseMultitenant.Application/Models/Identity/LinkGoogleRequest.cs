namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>
/// Lleva la cuenta esperada desde el state OAuth protegido y la cuenta de la sesión actual.
/// El servicio exige que coincidan antes de vincular Google.
/// </summary>
public sealed record LinkGoogleRequest(Guid ExpectedUserId, Guid SessionUserId);
