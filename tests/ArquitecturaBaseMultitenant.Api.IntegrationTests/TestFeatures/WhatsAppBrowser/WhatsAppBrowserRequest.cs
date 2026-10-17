using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Protocolo privado de la fixture, sin controles publicados por la API.</summary>
public sealed record WhatsAppBrowserRequest(string Kind, string Scenario, string Phone, string? Ticket = null,
    Guid? RecoveryId = null, string? Email = null, Guid? MethodId = null, Guid? TenantId = null, string? Text = null,
    WhatsAppBrowserReply? Reply = null)
{
    /// <summary>El PhoneNumberId de un número de organización lleva el escenario y no pasa del tope de Meta (60).</summary>
    public const int MaxOrganizationScenario = 40;

    public bool IsValid() => Kind is "begin" or "code" or "operatorCode" or "inbound" or "bot" or "events" or "expireTicket" or "recoveryNotice" or "methodEmailCode"
            or "orgNumber" or "orgInbound" or "orgProcess" or "orgOutbound"
        && Scenario is { Length: > 0 and <= 80 } && Scenario.All(value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_')
        && Phone is { Length: 14 } && Phone.StartsWith("+54911345678", StringComparison.Ordinal) && Phone[1..].All(char.IsAsciiDigit)
        && (Kind != "operatorCode" || Phone == WhatsAppBrowserControl.OperatorPhone)
        && (Kind != "recoveryNotice" || RecoveryId is { } recoveryId && recoveryId != Guid.Empty)
        && (Kind != "methodEmailCode" || MethodId is { } methodId && methodId != Guid.Empty
            && (Scenario == "backup-email-1440" && Email == "e8-backup-1440@example.test"
                || Scenario == "backup-email-390" && Email == "e8-backup-390@example.test"))
        && (Kind != "expireTicket" || ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security.ISecureTokenGenerator.HasTokenFormat(Ticket))
        && (Kind is "begin" || !Kind.StartsWith("org", StringComparison.Ordinal) || Scenario.Length <= MaxOrganizationScenario)
        && (Kind == "orgNumber" ? TenantId is { } tenantId && tenantId != Guid.Empty : TenantId is null)
        && (Kind == "orgInbound" ? (Text is null) != (Reply is null) && (Text is null || IsPlainText(Text)) && (Reply is null || Reply.IsValid())
            : Text is null && Reply is null);

    // Texto plano de hasta 200 caracteres: sin controles ni saltos, como lo escribe una persona en una línea.
    private static bool IsPlainText(string text) => text.Length is > 0 and <= 200 && !string.IsNullOrWhiteSpace(text) && !text.Any(char.IsControl);

    public override string ToString() => nameof(WhatsAppBrowserRequest);
}

/// <summary>La respuesta que toca la persona: una fila de la lista del menú o un botón que ofreció el bot.</summary>
public sealed record WhatsAppBrowserReply(string Type, string Id)
{
    public bool IsValid() => Type switch
    {
        "list" => BotButtons.ParseOption(Id) is not null,
        "button" => BotButtons.IsKnown(Id) && BotButtons.ParseOption(Id) is null,
        _ => false,
    };

    public override string ToString() => nameof(WhatsAppBrowserReply);
}
