

namespace ArquitecturaBaseMultitenant.Application.Models.Engagement;

public sealed record ConsumerInteractionResponse(Guid Id, string SourceKey, Guid OrganizationId, string OrganizationName, string Summary, DateTime LastActivityAtUtc);
