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

    public static class WhatsApp
    {
        public const string Manage = "platform.whatsapp.manage";
        public const string Attend = "platform.whatsapp.attend";
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

    /// <summary>
    /// Permisos de gestión: una escritura de Plataforma que exige alguno pide además una prueba reciente del segundo
    /// factor (E9). Atender chats, usar el Asistente y consultar no la piden.
    /// </summary>
    public static IReadOnlyCollection<string> RequiringRecentProof { get; } =
    [Tenants.Manage, Accounts.Manage, Recoveries.Manage, Legal.Manage, Operators.Manage, Settings.Manage, Assistant.Configure, WhatsApp.Manage];

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
        WhatsApp.Manage,
        WhatsApp.Attend,
    ];

    /// <summary>
    /// Lo que incluye cada permiso de un operador que no sirve sin el «Ver» de su misma área (decisión 260): se administra
    /// desde el listado y la ficha, y los topes del asistente se fijan mirando su uso, como «Configurar el asistente» de la
    /// organización, que también lo consulta. Nunca de otra área.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Includes { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [Tenants.Manage] = [Tenants.Read],
            [Accounts.Manage] = [Accounts.Read],
            [Assistant.Configure] = [Assistant.Read],
        }.AsReadOnly();

    /// <summary>El catálogo de Plataforma con lo que incluye cada permiso; lo efectivo es <see cref="PermissionInclusions.Effective"/>.</summary>
    public static PermissionInclusions Inclusions { get; } = PermissionInclusions.Compose([All], Includes);
}
