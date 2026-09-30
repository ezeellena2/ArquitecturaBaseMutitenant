namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Informa a qué método de respaldo se envió el código, con el destino enmascarado y el tiempo hasta un nuevo pedido.</summary>
public sealed record ReauthCodeResponse(Guid SourceMethodId, string Destination, int ResendAfterSeconds);
