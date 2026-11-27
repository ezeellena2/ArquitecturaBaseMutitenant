using System.Reflection;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// capas-y-flujo.md: lo que devuelve un lector de <c>Interfaces/Persistence</c> se llama <c>Row</c>, <c>Response</c> o
/// <c>Counts</c>, también adentro de una página o una colección. Los escalares, los enums y los value objects de Domain
/// (<c>Money</c>) no cuentan. Una excepción va en <see cref="Allowed"/>, con su motivo.
/// </summary>
public sealed class ReaderRowNamesTests
{
    private static readonly Assembly Application = Assembly.Load("ArquitecturaBaseMultitenant.Application");

    private static readonly string[] Suffixes = ["Row", "Response", "Counts"];

    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        // Anteriores a este test (E12): cada área los renombra cuando vuelve a tocar su lector.
        "IAccountDeletionTenantReader.ListAsync: AccountDeletionTenant",
        "IAssistantSettingsReader.FindOrganizationKeyAsync: AssistantKeyMaterial",
        "IAssistantSettingsReader.FindPlatformKeyAsync: AssistantKeyMaterial",
        "IAssistantUsageReader.ListOrganizationAsync: AssistantUsageReadPage",
        "IAssistantUsageReader.ListPlatformAsync: AssistantUsageReadPage",
        "IConsumerDataRetentionReader.ListExpiredAsync: ConsumerDataRetentionWork",
        "IPlatformSettingsReader.FindRateLimitsAsync: PlatformRateLimits",
        "IPlatformSettingsReader.FindRetentionPeriodsAsync: IdentityRetentionPeriods",
        "ITenantAdminReader.ListActiveVerifiedEmailRecipientsAsync: ActiveTenantAdminEmail",
        "ITenantProvisioningReader.FindAsync: TenantProvisioningSnapshot",
        "IWhatsAppBotReader.FindAsync: WhatsAppBot",
        "IWhatsAppBotReader.FindPlatformAsync: PlatformWhatsAppBot",
        "IWhatsAppBroadcastReader.FindNextDueAsync: WhatsAppDueBroadcast",
        "IWhatsAppChannelReader.FindInboundAsync: WhatsAppInboundChannel",
        "IWhatsAppContactReader.ListAudienceAsync: WhatsAppAudienceMember",
        "IWhatsAppTemplateReader.ListPlatformStatesAsync: WhatsAppPlatformTemplateStates",
        // WhatsApp lee para el puerto neutral IStructuredContactSource (C12 de la E12) y devuelve sus modelos, que no son de
        // persistencia: copiarlos a un Row solo les cambiaría el nombre.
        "IWhatsAppContactReader.FindStructuredAsync: StructuredContact",
        "IWhatsAppContactReader.ListStructuredAsync: StructuredContactPage",
        "IWhatsAppContactReader.ListStructuredPageAsync: StructuredContact",
        "IWhatsAppContactReader.ListTagCountsAsync: StructuredContactTag",
    };

    [Fact]
    public void Reader_results_are_rows_responses_or_counts()
    {
        var found = Application.GetTypes()
            .Where(type => type.IsInterface && type.Namespace is { } ns && ns.EndsWith(".Interfaces.Persistence", StringComparison.Ordinal)
                && type.Name.EndsWith("Reader", StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods().SelectMany(method => Named(method.ReturnType)
                .Where(result => !Suffixes.Any(suffix => result.Name.EndsWith(suffix, StringComparison.Ordinal)))
                .Select(result => $"{type.Name}.{method.Name}: {result.Name}")))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = found.Where(item => !Allowed.Contains(item)).Order(StringComparer.Ordinal).ToArray();
        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
        // Una excepción que ya no hace falta se saca de la lista.
        Assert.Empty(Allowed.Except(found, StringComparer.Ordinal));
    }

    // Los tipos propios que devuelve: se abren Task, las páginas y las colecciones.
    private static IEnumerable<Type> Named(Type type)
    {
        if (type.IsGenericType)
            return type.GetGenericArguments().SelectMany(Named).Concat(IsOwn(type) && !IsWrapper(type) ? [type] : []);
        if (type.IsArray) return Named(type.GetElementType()!);
        return IsOwn(type) && !type.IsEnum && type.Namespace?.EndsWith(".Domain.ValueObjects", StringComparison.Ordinal) != true
            ? [type] : [];
    }

    private static bool IsOwn(Type type) =>
        type.Assembly == Application || type.Assembly.GetName().Name == "ArquitecturaBaseMultitenant.Domain";

    private static bool IsWrapper(Type type) => type.GetGenericTypeDefinition().Name is "PagedResult`1" or "CursorResult`1";
}
