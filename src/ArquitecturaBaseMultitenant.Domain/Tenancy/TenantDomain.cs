using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Reserva un dominio de correo global y conserva su desafío TXT hasta verificarlo.</summary>
public sealed class TenantDomain : Entity, IVersioned
{
    public const string TxtRecordPrefix = "ab-verificacion=";
    public const int MaxVerificationTokenLength = 64;

    private TenantDomain()
    {
        Domain = string.Empty;
        VerificationToken = string.Empty;
    }

    public Guid OrganizationId { get; private set; }
    public string Domain { get; private set; }
    public string VerificationToken { get; private set; }
    public TenantDomainStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }
    public uint Version { get; private set; }
    public string TxtRecordValue => TxtRecordPrefix + VerificationToken;

    public static Result<TenantDomain> Create(Guid organizationId, OrganizationEmailDomain domain,
        string verificationToken, DateTime createdAtUtc)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("An organization is required.", nameof(organizationId));
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationToken);
        if (verificationToken.Length > MaxVerificationTokenLength
            || verificationToken.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            throw new ArgumentException("The verification token has an invalid format.", nameof(verificationToken));
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The creation instant must be UTC.", nameof(createdAtUtc));

        return new TenantDomain
        {
            OrganizationId = organizationId,
            Domain = domain.Value,
            VerificationToken = verificationToken,
            Status = TenantDomainStatus.Pending,
            CreatedAtUtc = createdAtUtc,
        };
    }

    public Result Verify(DateTime verifiedAtUtc)
    {
        if (Status != TenantDomainStatus.Pending) return TenantDomainErrors.AlreadyVerified;
        if (verifiedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The verification instant must be UTC.", nameof(verifiedAtUtc));

        Status = TenantDomainStatus.Verified;
        VerifiedAtUtc = verifiedAtUtc;
        return Result.Success();
    }

    public Result Remove()
    {
        if (Status == TenantDomainStatus.Removed) return TenantDomainErrors.AlreadyRemoved;
        Status = TenantDomainStatus.Removed;
        VerificationToken = string.Empty;
        return Result.Success();
    }
}
