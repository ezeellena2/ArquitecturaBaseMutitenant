namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Catálogo de permisos de organización y empresa; las rutas declaran permisos, nunca roles.</summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Assistant
    {
        public const string Use = "assistant.use";
        public const string Configure = "assistant.configure";
    }

    public static class WhatsApp
    {
        public const string Customers = "whatsapp.customers";
        public const string Internal = "whatsapp.internal";
        public const string Broadcasts = "whatsapp.broadcasts";
        public const string Manage = "whatsapp.manage";
    }

    public static class Users
    {
        public const string Read = "users.read";
        public const string Invite = "users.invite";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string Read = "roles.read";
        public const string Manage = "roles.manage";
        public const string Assign = "roles.assign";
    }

    public static class Companies
    {
        public const string Read = "companies.read";
        public const string Manage = "companies.manage";
    }

    public static class Settings
    {
        public const string Read = "settings.read";
        public const string Manage = "settings.manage";
    }

    public static class Audit
    {
        public const string Read = "audit.read";
    }

    public static class PublicPage
    {
        public const string Manage = "publicpage.manage";
    }

    public static class Company
    {
        public static class Members
        {
            public const string Read = "company.members.read";
            public const string Manage = "company.members.manage";
        }
    }

    public static IReadOnlyCollection<string> OrganizationScoped { get; } =
    [
        Users.Read,
        Users.Invite,
        Users.Manage,
        Roles.Read,
        Roles.Manage,
        Roles.Assign,
        Companies.Read,
        Companies.Manage,
        Settings.Read,
        Settings.Manage,
        Audit.Read,
        PublicPage.Manage,
        Assistant.Use,
        Assistant.Configure,
        WhatsApp.Customers,
        WhatsApp.Internal,
        WhatsApp.Broadcasts,
        WhatsApp.Manage,
    ];

    public static IReadOnlyCollection<string> CompanyScoped { get; } =
    [Company.Members.Read, Company.Members.Manage];

    /// <summary>Permisos de organización que una asignación empresarial activa puede aportar.</summary>
    public static IReadOnlyCollection<string> OrganizationProjectedFromCompany { get; } =
    [Assistant.Use];

    public static IReadOnlyCollection<string> All { get; } =
    [.. OrganizationScoped, .. CompanyScoped];
}
