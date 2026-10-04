using ArquitecturaBaseMultitenant.Domain.Common;
using System.Net;
using System.Net.Sockets;

namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>Evento de seguridad global y append-only, sin direcciones ni secretos.</summary>
public sealed class SecurityEvent : Entity
{
    private SecurityEvent() { }

    public SecurityEventType Type { get; private set; }
    public AuditActorKind ActorKind { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid? TargetTenantId { get; private set; }
    public Guid? TargetAccountId { get; private set; }
    public string Changes { get; private set; } = "{}";
    public string? MaskedIpAddress { get; private set; }
    public string? Reason { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static SecurityEvent ForAccount(SecurityEventType type, Guid actorId, DateTime occurredAtUtc)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("The actor id cannot be empty.", nameof(actorId));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The occurrence instant must be UTC.", nameof(occurredAtUtc));
        }

        return new SecurityEvent
        {
            Type = type,
            ActorKind = AuditActorKind.User,
            ActorId = actorId,
            TargetAccountId = actorId,
            OccurredAtUtc = occurredAtUtc,
        };
    }

    public static SecurityEvent ForOrganizationMembershipEnded(Guid actorId, Guid tenantId, Guid accountId,
        DateTime occurredAtUtc)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("The actor id cannot be empty.", nameof(actorId));
        if (tenantId == Guid.Empty) throw new ArgumentException("The tenant id cannot be empty.", nameof(tenantId));
        if (accountId == Guid.Empty) throw new ArgumentException("The account id cannot be empty.", nameof(accountId));
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The occurrence instant must be UTC.", nameof(occurredAtUtc));

        return new SecurityEvent
        {
            Type = SecurityEventType.OrganizationMembershipEnded,
            ActorKind = AuditActorKind.User,
            ActorId = actorId,
            TargetTenantId = tenantId,
            TargetAccountId = accountId,
            OccurredAtUtc = occurredAtUtc,
        };
    }

    public static SecurityEvent ForPlatformSettings(AuditActorKind actorKind, Guid actorId,
        string reason, DateTime occurredAtUtc) =>
        ForPlatformAction(SecurityEventType.PlatformSettingsChanged, actorKind, actorId,
            reason, occurredAtUtc);

    public static SecurityEvent ForPlatformOperatorGrant(string reason, DateTime occurredAtUtc, Guid? accountId = null)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("The account id cannot be empty.", nameof(accountId));
        var ev = ForPlatformAction(SecurityEventType.PlatformOperatorGranted, AuditActorKind.System,
            Guid.Empty, reason, occurredAtUtc);
        ev.TargetAccountId = accountId;
        return ev;
    }

    public SecurityEvent WithEvidence(SecurityEventChanges changes, string? ipAddress)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Changes = changes.ToJson();
        return WithRequestIp(ipAddress);
    }

    public SecurityEvent WithRequestIp(string? ipAddress)
    {
        MaskedIpAddress = MaskIp(ipAddress);
        return this;
    }

    private static string? MaskIp(string? value)
    {
        if (!IPAddress.TryParse(value, out var address)) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork
            ? FormattableString.Invariant($"{bytes[0]}.{bytes[1]}.{bytes[2]}.*")
            : FormattableString.Invariant($"{(bytes[0] << 8) | bytes[1]:x}:{(bytes[2] << 8) | bytes[3]:x}:{(bytes[4] << 8) | bytes[5]:x}:*");
    }

    public static SecurityEvent ForTenantAdministration(SecurityEventType type, Guid operatorId,
        Guid tenantId, string reason, DateTime occurredAtUtc)
    {
        if (type is not (SecurityEventType.OrganizationRejected or SecurityEventType.OrganizationSuspended
            or SecurityEventType.OrganizationReactivated or SecurityEventType.OrganizationClosed
            or SecurityEventType.OrganizationApproved or SecurityEventType.OrganizationCreated
            or SecurityEventType.OrganizationProvisioningRetried
            or SecurityEventType.PublicPageUnpublished or SecurityEventType.PublicPagePublicationAllowed
            or SecurityEventType.OrganizationDomainVerified or SecurityEventType.OrganizationDomainRemoved))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (tenantId == Guid.Empty) throw new ArgumentException("A target organization is required.", nameof(tenantId));
        var ev = ForPlatformAction(type, AuditActorKind.PlatformOperator, operatorId, reason, occurredAtUtc);
        ev.TargetTenantId = tenantId;
        return ev;
    }

    /// <summary>Acción de un operador sobre una cuenta: suspender, reactivar, cerrar sus sesiones o iniciar la baja.</summary>
    public static SecurityEvent ForAccountAdministration(SecurityEventType type, Guid operatorId,
        Guid accountId, string reason, DateTime occurredAtUtc)
    {
        if (type is not (SecurityEventType.AccountSuspended or SecurityEventType.AccountReactivated
            or SecurityEventType.AccountSessionsRevoked or SecurityEventType.AccountDeletionRequested))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (accountId == Guid.Empty) throw new ArgumentException("A target account is required.", nameof(accountId));
        var ev = ForPlatformAction(type, AuditActorKind.PlatformOperator, operatorId, reason, occurredAtUtc);
        ev.TargetAccountId = accountId;
        return ev;
    }

    public static SecurityEvent ForPlatformTenantAccess(Guid operatorId, Guid tenantId, string reason, DateTime occurredAtUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A target organization is required.", nameof(tenantId));
        var ev = ForPlatformAction(SecurityEventType.PlatformTenantEntered, AuditActorKind.PlatformOperator,
            operatorId, reason, occurredAtUtc);
        ev.TargetTenantId = tenantId;
        return ev;
    }

    public static SecurityEvent ForTenantFeatures(AuditActorKind actorKind, Guid actorId,
        Guid tenantId, string reason, DateTime occurredAtUtc)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A target space is required.", nameof(tenantId));
        var ev = ForPlatformAction(SecurityEventType.TenantFeaturesChanged, actorKind, actorId, reason, occurredAtUtc);
        ev.TargetTenantId = tenantId;
        return ev;
    }

    private static SecurityEvent ForPlatformAction(SecurityEventType type, AuditActorKind actorKind,
        Guid actorId, string reason, DateTime occurredAtUtc)
    {
        if (actorKind is not (AuditActorKind.System or AuditActorKind.PlatformOperator))
        {
            throw new ArgumentOutOfRangeException(nameof(actorKind));
        }

        if (actorKind == AuditActorKind.PlatformOperator && actorId == Guid.Empty)
        {
            throw new ArgumentException("The operator id cannot be empty.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > TextLimits.Description)
        {
            throw new ArgumentException("A reason is required within the description limit.", nameof(reason));
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The occurrence instant must be UTC.", nameof(occurredAtUtc));
        }

        return new SecurityEvent
        {
            Type = type,
            ActorKind = actorKind,
            ActorId = actorId,
            Reason = reason.Trim(),
            OccurredAtUtc = occurredAtUtc,
        };
    }
}
