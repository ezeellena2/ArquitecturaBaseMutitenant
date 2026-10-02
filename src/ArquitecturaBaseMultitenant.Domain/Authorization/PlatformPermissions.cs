namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Permisos del backoffice de la plataforma.</summary>
public static class PlatformPermissions
{
    public static class Tenants
    {
        public const string Read = "platform.tenants.read";
        public const string Manage = "platform.tenants.manage";
    }

    public static class Accounts
    {
        public const string Read = "platform.accounts.read";
        public const string Manage = "platform.accounts.manage";
    }

    public static class Recoveries
    {
        public const string Manage = "platform.recoveries.manage";
    }

    public static class Legal
    {
        public const string Manage = "platform.legal.manage";
    }

    public static class Operators
    {
        public const string Manage = "platform.operators.manage";
    }

    public static class Audit
    {
        public const string Read = "platform.audit.read";
    }

    public static class Settings
    {
        public const string Manage = "platform.settings.manage";
    }

    public static IReadOnlyCollection<string> All { get; } =
    [
        Tenants.Read,
        Tenants.Manage,
        Accounts.Read,
        Accounts.Manage,
        Recoveries.Manage,
        Legal.Manage,
        Operators.Manage,
        Audit.Read,
        Settings.Manage,
    ];
}
