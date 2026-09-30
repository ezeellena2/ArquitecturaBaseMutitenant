using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

public sealed record RequestReauthHttpRequest(ReauthAction? Action, Guid? TargetMethodId);
