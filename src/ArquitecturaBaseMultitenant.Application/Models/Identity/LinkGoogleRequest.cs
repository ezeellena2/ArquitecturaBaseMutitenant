namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Identidades extraídas del state OAuth protegido y de la cookie Identity validada.</summary>
public sealed record LinkGoogleRequest(Guid ExpectedUserId, Guid SessionUserId);
