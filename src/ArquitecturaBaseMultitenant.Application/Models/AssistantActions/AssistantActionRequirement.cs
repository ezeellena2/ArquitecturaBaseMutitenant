namespace ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

/// <summary>
/// El requisito de una acción, que copia el atributo de la ruta que guarda (ADR 0068 §3). Una acción de empresa nombra su
/// campo de empresa: el permiso se mira en esa empresa, nunca «en alguna».
/// </summary>
public sealed record AssistantActionRequirement(
    AssistantActionRequirementKind Kind, string? Permission, string? OrganizationPermission, string? CompanyField)
{
    /// <summary>Una persona, sin permiso (<c>[Access(Consumer)]</c>).</summary>
    public static AssistantActionRequirement Consumer() => new(AssistantActionRequirementKind.Consumer, null, null, null);

    /// <summary>La organización, con su permiso (<c>[HasPermission]</c>).</summary>
    public static AssistantActionRequirement Organization(string permission) =>
        new(AssistantActionRequirementKind.Organization, permission, null, null);

    /// <summary>Una empresa, con su permiso, el de organización que también alcanza y su campo (<c>[HasCompanyPermission]</c>).</summary>
    public static AssistantActionRequirement Company(string permission, string companyField, string? organizationPermission = null) =>
        new(AssistantActionRequirementKind.Company, permission, organizationPermission, companyField);

    /// <summary>Plataforma, con su permiso (<c>[HasPlatformPermission]</c>).</summary>
    public static AssistantActionRequirement Platform(string permission) =>
        new(AssistantActionRequirementKind.Platform, permission, null, null);
}

public enum AssistantActionRequirementKind
{
    Consumer,
    Organization,
    Company,
    Platform,
}
