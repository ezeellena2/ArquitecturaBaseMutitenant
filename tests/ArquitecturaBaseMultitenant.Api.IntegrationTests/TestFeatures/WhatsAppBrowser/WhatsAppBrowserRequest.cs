namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Protocolo privado de la fixture, sin controles publicados por la API.</summary>
public sealed record WhatsAppBrowserRequest(string Kind, string Scenario, string Phone, string? Ticket = null,
    Guid? RecoveryId = null, string? Email = null, Guid? MethodId = null)
{
    public bool IsValid() => Kind is "begin" or "code" or "operatorCode" or "inbound" or "bot" or "events" or "expireTicket" or "recoveryNotice" or "methodEmailCode"
        && Scenario is { Length: > 0 and <= 80 } && Scenario.All(value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_')
        && Phone is { Length: 14 } && Phone.StartsWith("+54911345678", StringComparison.Ordinal) && Phone[1..].All(char.IsAsciiDigit)
        && (Kind != "operatorCode" || Phone == WhatsAppBrowserControl.OperatorPhone)
        && (Kind != "recoveryNotice" || RecoveryId is { } recoveryId && recoveryId != Guid.Empty)
        && (Kind != "methodEmailCode" || MethodId is { } methodId && methodId != Guid.Empty
            && (Scenario == "backup-email-1440" && Email == "e8-backup-1440@example.test"
                || Scenario == "backup-email-390" && Email == "e8-backup-390@example.test"))
        && (Kind != "expireTicket" || ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security.ISecureTokenGenerator.HasTokenFormat(Ticket));
    public override string ToString() => nameof(WhatsAppBrowserRequest);
}
