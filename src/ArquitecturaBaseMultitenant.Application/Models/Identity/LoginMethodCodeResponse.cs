namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record LoginMethodCodeResponse(Guid MethodId, int ResendAfterSeconds);
