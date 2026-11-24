using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// ADR 0062 §4: cada tabla del núcleo con datos personales nombra quién la purga o por qué se conserva. Dato personal es,
/// para este control, una columna cuyo nombre dice correo, teléfono, destino o identificador de ingreso, IP o dirección,
/// navegador, nombre de una persona, contacto o destinatario, clave de un proveedor, el sujeto de un token, un contenido
/// protegido o el id de una cuenta (<see cref="PersonalColumns"/>). La autoría de una fila auditable (<c>CreatedBy</c>,
/// <c>ModifiedBy</c>) no cuenta: la tiene casi toda tabla y sigue a su fila. Las columnas de una propiedad compleja cuentan
/// como las demás. Una tabla nueva con una de esas columnas hace fallar el control hasta que se suma a
/// <see cref="Retention"/>, y la lista no guarda tablas que ya no existen o que ya no tienen esos datos. Las tablas de los
/// módulos (WhatsApp, el asistente) no entran, como en ArquitecturaBase: su retención la decide cada módulo, y uno que
/// quiera entrar suma sus tablas con el mismo criterio.
/// </summary>
public sealed class PersonalDataPurgeTests
{
    private const string IdentityRetention = "IdentityRetentionWorker";
    private const string AccountDeletion = "AccountDeletionWorker";

    private static readonly string[] PersonalColumns =
    [
        "Email", "Phone", "Destination", "Identifier", "IpAddress", "Address", "UserAgent", "UserName", "DisplayName",
        "FullName", "Contact", "Recipient", "ProviderKey", "Subject", "Protected", "UserId",
    ];

    /// <summary>Quién purga cada tabla, o por qué se conserva.</summary>
    private static readonly Dictionary<string, Retained> Retention = new(StringComparer.Ordinal)
    {
        ["identity.LoginCodes"] = Purged(IdentityRetention, "a los 30 días de creados, y los de la cuenta con su baja"),
        ["identity.LoginAudits"] = Purged(IdentityRetention, "a los 12 meses, salvo el último ingreso de cada cuenta"),
        ["platform.OpenIddictTokens"] = Purged(IdentityRetention, "con la poda de OpenIddict, a los 14 días de vencidos o revocados, y con la baja"),
        ["platform.OpenIddictAuthorizations"] = Purged(IdentityRetention, "con la poda de OpenIddict, cuando no les queda ningún token, y con la baja"),
        ["tenant.Invitations"] = Purged("InvitationRetentionWorker",
            "a los 30 días de cerradas, salvo la última de cada destino de cada persona invitada; nunca una pendiente"),
        ["identity.LoginInvitations"] = Purged("trigger sync_login_invitation", "la proyección de las invitaciones pendientes: sale cuando la invitación deja de estarlo"),
        ["platform.PendingTenantOwners"] = Purged("trigger forget_pending_tenant_owner", "sale cuando la invitación del Dueño deja de estar pendiente"),
        ["identity.NotificationReads"] = Purged("NotificationReadCleanupWorker", "a los 90 días de abiertos, y los de la cuenta con su baja"),
        ["platform.IdempotencyKeys"] = Purged("IdempotencyCleanupWorker", "al vencer la clave"),
        ["platform.OutboxMessages"] = Purged(AccountDeletion, "los de la cuenta con su baja (OutboxDeletionParticipant)"),
        ["identity.LoginMethods"] = Purged(AccountDeletion, "con la baja de la cuenta"),
        ["identity.AspNetUserClaims"] = Purged(AccountDeletion, "con la baja de la cuenta"),
        ["identity.AspNetUserLogins"] = Purged(AccountDeletion, "con la baja de la cuenta"),
        ["identity.AspNetUserTokens"] = Purged(AccountDeletion, "con la baja de la cuenta"),
        ["identity.AccountDeletionCancelTickets"] = Purged(AccountDeletion, "el comprobante para cancelar la baja: sale con ella"),
        ["platform.AccountRecoveryRequests"] = Purged(AccountDeletion, "los pedidos de la cuenta, con su baja"),
        ["identity.AspNetUsers"] = Kept("la cuenta: vive mientras exista, y la baja la deja anonimizada (ADR 0035)"),
        ["identity.LegalAcceptances"] = Kept("la prueba legal: la baja le retira solo la IP y el navegador"),
        ["identity.UserTenantAccesses"] = Kept("índice técnico de los accesos que mantienen los triggers de los miembros"),
        ["tenant.Members"] = Kept("quién es o fue miembro de la organización: lo nombra su auditoría; la baja lo retira de cada organización"),
        ["platform.Tenants"] = Kept("la organización, con la cuenta que la registró"),
        ["platform.SecurityEvents"] = Kept("la auditoría de seguridad es append-only (0015) y guarda la IP enmascarada"),
        ["platform.PlatformPermissionAssignments"] = Kept("los permisos de un operador de Plataforma, mientras lo sea"),
        ["platform.PlatformSettings"] = Kept("los correos de contacto de la plataforma, que no son de una persona"),
        ["public_site.PublicPages"] = Kept("los datos de contacto que la organización publica en su página"),
        ["platform.OpenIddictApplications"] = Kept("los clientes de OpenIddict, del seed: su nombre es el de la aplicación"),
        ["platform.OpenIddictScopes"] = Kept("los scopes de OpenIddict, del seed: su nombre es el del scope"),
        ["identity.PlatformAuthenticators"] = Kept("el segundo factor de un operador de Plataforma, mientras lo tenga"),
        ["identity.PlatformRecoveryCodes"] = Kept("los códigos de recuperación del operador, solo hashes, mientras tenga el segundo factor"),
        ["identity.PlatformFactorChallenges"] = Kept("el desafío del segundo factor del operador, ligado a su navegador"),
        ["identity.PlatformFactorOperations"] = Kept("la idempotencia de las operaciones del segundo factor del operador"),
        ["identity.PlatformSessions"] = Kept("la evidencia del segundo factor de una sesión de Plataforma"),
    };

    [Fact]
    public void Every_core_table_with_personal_data_names_who_purges_it_or_why_it_is_kept()
    {
        var personal = PersonalTables();

        // El detector ve las columnas que tiene que ver: si dejara de verlas, el control pasaría en silencio.
        Assert.Contains("Destination", personal["identity.LoginCodes"]);
        Assert.Contains("UserId", personal["identity.LoginAudits"]);
        Assert.Contains("Destination", personal["tenant.Invitations"]);
        Assert.Contains("Subject", personal["platform.OpenIddictTokens"]);
        Assert.Contains("IpAddress", personal["identity.LegalAcceptances"]);

        string[] unnamed = [.. personal.Keys.Except(Retention.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(table => $"{table}: {string.Join(", ", personal[table])}")];
        Assert.True(unnamed.Length == 0, string.Join(Environment.NewLine, unnamed));
        Assert.Empty(Retention.Keys.Except(personal.Keys, StringComparer.Ordinal));
        Assert.All(Retention.Values, retained => Assert.False(string.IsNullOrWhiteSpace(retained.Why)));

        // Quien purga existe: un trabajo de fondo de Infrastructure, por su nombre, o un trigger de la base.
        var workers = typeof(ApplicationDbContext).Assembly.GetTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        string[] missing = [.. Retention.Values.Select(retained => retained.PurgedBy).OfType<string>()
            .Where(purger => !purger.StartsWith("trigger ", StringComparison.Ordinal) && !workers.Contains(purger))];
        Assert.True(missing.Length == 0, string.Join(", ", missing));
    }

    /// <summary>El modelo del núcleo, sin lo que aportan los módulos.</summary>
    private static Dictionary<string, string[]> PersonalTables()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_guard");
        options.UseOpenIddict<Guid>();
        using var core = new ApplicationDbContext(options.Options, new NoTenantContext(), []);
        return core.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .GroupBy(entity => $"{entity.GetSchema()}.{entity.GetTableName()}", StringComparer.Ordinal)
            .Select(table => (Table: table.Key, Columns: table
                .SelectMany(ColumnsOf)
                .Where(name => PersonalColumns.Any(hint => name.Contains(hint, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()))
            .Where(table => table.Columns.Length > 0)
            .ToDictionary(table => table.Table, table => table.Columns, StringComparer.Ordinal);
    }

    // Las columnas de las propiedades comunes y de las complejas, también las anidadas, con su nombre en la tabla.
    private static IEnumerable<string> ColumnsOf(ITypeBase type) =>
        type.GetProperties().Select(property => property.GetColumnName())
            .Concat(type.GetComplexProperties().SelectMany(complex => ColumnsOf(complex.ComplexType)));

    private static Retained Purged(string purgedBy, string how) => new(purgedBy, how);

    private static Retained Kept(string why) => new(null, why);

    /// <param name="PurgedBy">Quién la purga, o null si se conserva.</param>
    /// <param name="Why">Qué borra y cuándo, o por qué se conserva.</param>
    private sealed record Retained(string? PurgedBy, string Why);

    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantKind? TenantKind => null;

        public Guid RequiredTenantId => throw new NotSupportedException();
    }
}
