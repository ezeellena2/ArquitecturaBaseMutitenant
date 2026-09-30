using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record RequestReauthRequest(ReauthAction Action, Guid? TargetMethodId);
