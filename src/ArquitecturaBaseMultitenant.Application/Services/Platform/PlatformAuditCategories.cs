using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>Los tipos del filtro de Auditoría del tablero y a cuál pertenece cada evento.</summary>
internal static class PlatformAuditCategories
{
    public const string SignIns = "signins";
    public const string Organizations = "organizations";
    public const string Accounts = "accounts";
    public const string Operators = "operators";
    public const string Settings = "settings";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> Filterable = [SignIns, Organizations, Accounts, Operators, Settings];

    public static string Of(SecurityEventType type) => type switch
    {
        SecurityEventType.OrganizationRejected or SecurityEventType.OrganizationSuspended
            or SecurityEventType.OrganizationReactivated or SecurityEventType.OrganizationClosed
            or SecurityEventType.OrganizationApproved or SecurityEventType.OrganizationCreated
            or SecurityEventType.OrganizationProvisioningRetried or SecurityEventType.PlatformTenantEntered
            or SecurityEventType.TenantFeaturesChanged or SecurityEventType.PublicPageUnpublished
            or SecurityEventType.PublicPagePublicationAllowed or SecurityEventType.OrganizationDomainVerified
            or SecurityEventType.OrganizationDomainRemoved or SecurityEventType.OrganizationMembershipEnded => Organizations,
        SecurityEventType.LoginMethodChanged or SecurityEventType.AccountDeletionRequested
            or SecurityEventType.AccountDeletionCancelled or SecurityEventType.AccountDeleted
            or SecurityEventType.AccountSuspended or SecurityEventType.AccountReactivated
            or SecurityEventType.AccountSessionsRevoked or SecurityEventType.AccountRecoveryApproved
            or SecurityEventType.AccountRecoveryRejected => Accounts,
        SecurityEventType.PlatformOperatorGranted or SecurityEventType.PlatformOperatorInvited
            or SecurityEventType.PlatformOperatorInvitationResent or SecurityEventType.PlatformOperatorInvitationRevoked
            or SecurityEventType.PlatformOperatorPermissionsChanged or SecurityEventType.PlatformOperatorRemoved => Operators,
        SecurityEventType.PlatformSettingsChanged or SecurityEventType.LegalDocumentPublished => Settings,
        _ => Other,
    };

    /// <summary>Los tipos de evento de una categoría; los ingresos no son eventos y salen de LoginAudits.</summary>
    public static IReadOnlyCollection<SecurityEventType> TypesOf(string category) =>
        [.. Enum.GetValues<SecurityEventType>().Where(type => Of(type) == category)];
}
