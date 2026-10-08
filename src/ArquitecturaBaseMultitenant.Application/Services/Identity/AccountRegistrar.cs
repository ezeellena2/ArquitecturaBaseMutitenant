using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Crea la identidad global. Método, espacio personal y consentimiento son pasos explícitos del llamador.</summary>
internal sealed class AccountRegistrar(IUserRepository users, IPersonalSpaceProvisioner personalSpaces)
{
    internal async Task<UserAccountRow> CreateAsync(string culture, string timeZoneId, CancellationToken ct,
        DateTime? operatorInvitationExpiresAtUtc = null)
    {
        var account = await users.CreateAsync(null, culture, timeZoneId, ct);
        if (operatorInvitationExpiresAtUtc is { } expiresAtUtc)
        {
            await users.InvitePlatformOperatorAsync(account.Id, expiresAtUtc, ct);
            return await users.GetByIdAsync(account.Id, ct)
                ?? throw new InvalidOperationException("The invited operator disappeared within its UoW.");
        }
        return account;
    }

    internal async Task StagePersonalAsync(Guid userId, PersonalSpaceDraft draft, CancellationToken ct)
    {
        var account = await users.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("The account must exist before its personal space.");
        if (account.IsPlatformOperator) throw new InvalidOperationException("An operator cannot have a personal space.");
        personalSpaces.Stage(draft, userId);
    }
}
