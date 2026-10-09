namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.WhatsAppBrowser;

/// <summary>Protocolo privado de la fixture, sin controles publicados por la API.</summary>
public sealed record WhatsAppBrowserRequest(string Kind, string Scenario, string Phone, string? Ticket = null)
{
    public bool IsValid() => Kind is "begin" or "code" or "operatorCode" or "inbound" or "bot" or "events" or "expireTicket"
        && Scenario is { Length: > 0 and <= 80 } && Scenario.All(value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_')
        && Phone is { Length: 14 } && Phone.StartsWith("+54911345678", StringComparison.Ordinal) && Phone[1..].All(char.IsAsciiDigit)
        && (Kind != "operatorCode" || Phone == WhatsAppBrowserControl.OperatorPhone)
        && (Kind != "expireTicket" || ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security.ISecureTokenGenerator.HasTokenFormat(Ticket));
    public override string ToString() => nameof(WhatsAppBrowserRequest);
}
