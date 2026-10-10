namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Permisos del backoffice de la plataforma.</summary>
public static class PlatformPermissions
{
    public static class Assistant
    {
        public const string Use = "platform.assistant.use";
        public const string Configure = "platform.assistant.configure";
        public const string Read = "platform.assistant.read";
    }

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

    /// <summary>Permisos iniciales anteriores a módulos opcionales; solo para reparar indicadores de operador legados.</summary>
    public static IReadOnlyCollection<string> LegacyBootstrap { get; } =
    [Tenants.Read, Tenants.Manage, Accounts.Read, Accounts.Manage, Recoveries.Manage, Legal.Manage, Operators.Manage, Audit.Read, Settings.Manage];

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
        Assistant.Use,
        Assistant.Configure,
        Assistant.Read,
    ];
}
