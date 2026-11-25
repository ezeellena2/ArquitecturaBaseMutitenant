using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence;

/// <summary>De qué esquema es el contenido: decide el propósito con que se cifra, nunca la fila.</summary>
internal enum WhatsAppChatContentScope
{
    /// <summary><c>tenant.*</c>: la organización de la base que lee o guarda.</summary>
    Organization,

    /// <summary><c>platform.*</c>: las consultas al número de Plataforma.</summary>
    Platform,

    /// <summary><c>engagement.*</c>: la copia vinculada, que la persona lee desde su espacio personal.</summary>
    Linked,
}

/// <summary>
/// ADR 0065: el contenido del chat se guarda cifrado con Data Protection, con la clave de la instalación y el propósito atado a
/// la organización. La organización sale de la base que está leyendo o guardando (<see cref="PersistenceOperation"/>), la misma
/// que fija <c>app.tenant_id</c> para la RLS (la petición, <c>ITenantScope.Enter</c> o <c>TenantJobRunner</c>), y se resuelve en
/// cada conversión: EF arma el modelo una vez por proceso. Leer o guardar contenido de <c>tenant.*</c> sin organización es un bug
/// y falla. Data Protection nunca cifra igual dos veces, así que el contenido no se busca ni se compara en SQL, solo se pregunta si
/// es nulo. Lo que el anillo no abre (claves perdidas, una base restaurada sin el certificado o un cifrado copiado a la fila de
/// otra organización) se lee vacío, sin romper la carga de su fila.
/// </summary>
internal sealed class WhatsAppChatContentConverter : ValueConverter<string?, byte[]?>
{
    internal const string ChatPurpose = "ArquitecturaBaseMultitenant.WhatsApp.ChatContent.v1";
    internal const string LinkedPurpose = "ArquitecturaBaseMultitenant.WhatsApp.LinkedContent.v1";
    internal const string PlatformSubpurpose = "platform";

    private WhatsAppChatContentConverter(Expression<Func<string?, byte[]?>> protect, Expression<Func<byte[]?, string?>> unprotect)
        : base(protect, unprotect)
    {
    }

    public static WhatsAppChatContentConverter Organization { get; } = new(
        value => Protect(WhatsAppChatContentScope.Organization, value), value => Unprotect(WhatsAppChatContentScope.Organization, value));

    public static WhatsAppChatContentConverter Platform { get; } = new(
        value => Protect(WhatsAppChatContentScope.Platform, value), value => Unprotect(WhatsAppChatContentScope.Platform, value));

    public static WhatsAppChatContentConverter Linked { get; } = new(
        value => Protect(WhatsAppChatContentScope.Linked, value), value => Unprotect(WhatsAppChatContentScope.Linked, value));

    // Un nulo queda nulo en la columna: «sin contenido» se pregunta en SQL.
    private static byte[]? Protect(WhatsAppChatContentScope scope, string? value) =>
        value is null ? null : Protector(scope).Protect(Encoding.UTF8.GetBytes(value));

    private static string? Unprotect(WhatsAppChatContentScope scope, byte[]? value)
    {
        if (value is null) return null;
        var protector = Protector(scope);
        try
        {
            return Encoding.UTF8.GetString(protector.Unprotect(value));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static IDataProtector Protector(WhatsAppChatContentScope scope)
    {
        var context = PersistenceOperation.Current
            ?? throw new InvalidOperationException("The WhatsApp chat content was converted outside a query or a save of its database.");
        var provider = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider
            ?.GetService<IDataProtectionProvider>()
            ?? throw new InvalidOperationException("The WhatsApp chat content needs the data protection of the application.");
        return scope switch
        {
            WhatsAppChatContentScope.Organization => provider.CreateProtector(ChatPurpose, (context.ActiveTenantId
                ?? throw new InvalidOperationException("The chat content of an organization was converted without its organization."))
                .ToString("N")),
            WhatsAppChatContentScope.Platform => provider.CreateProtector(ChatPurpose, PlatformSubpurpose),
            WhatsAppChatContentScope.Linked => provider.CreateProtector(LinkedPurpose),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown chat content scope."),
        };
    }
}
