namespace ArquitecturaBaseMultitenant.Application.Models.Invitations;

public sealed record AcceptInvitationResponse(Guid OrganizationId, bool CreatedAccount, bool NeedsPersonalLoginMethod);
