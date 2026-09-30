namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record ReauthCodeResponse(Guid SourceMethodId, string Destination, int ResendAfterSeconds);
