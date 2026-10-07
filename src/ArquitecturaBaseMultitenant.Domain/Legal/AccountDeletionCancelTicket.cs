using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Legal;

/// <summary>Prueba breve de ingreso para cancelar una baja: cuenta, retorno autorizado y hash de un solo uso.</summary>
public sealed class AccountDeletionCancelTicket : Entity
{
    private AccountDeletionCancelTicket() { }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public string ReturnUrl { get; private set; } = string.Empty;

    public static AccountDeletionCancelTicket Issue(Guid userId, string tokenHash, DateTime issuedAtUtc, string returnUrl)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnUrl);
        RequireUtc(issuedAtUtc);
        return new AccountDeletionCancelTicket
        {
            UserId = userId, TokenHash = tokenHash, IssuedAtUtc = issuedAtUtc,
            ExpiresAtUtc = issuedAtUtc.AddMinutes(5), ReturnUrl = returnUrl,
        };
    }

    public Result Consume(Guid userId, DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        if (ConsumedAtUtc is not null || UserId != userId || nowUtc < IssuedAtUtc || nowUtc >= ExpiresAtUtc)
            return AccountDeletionErrors.CancelProofInvalid;
        ConsumedAtUtc = nowUtc;
        return Result.Success();
    }

    public override string ToString() => nameof(AccountDeletionCancelTicket);
    private static void RequireUtc(DateTime instantUtc)
    {
        if (instantUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The instant must be UTC.", nameof(instantUtc));
    }
}
