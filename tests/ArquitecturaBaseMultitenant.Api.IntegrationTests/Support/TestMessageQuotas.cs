namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Los topes de correo y de códigos por WhatsApp de la ADR 0063, altos para los hosts de prueba, el E2E y el recorrido:
/// mandan muchos códigos e invitaciones desde la misma dirección y en el mismo día. Los tests de los topes fijan los suyos.
/// </summary>
internal static class TestMessageQuotas
{
    private const string High = "1000000";

    internal static IEnumerable<KeyValuePair<string, string?>> Settings =>
    [
        new("Email:Quotas:DailyLimit", High),
        new("Email:Quotas:DailyInvitationReserve", "0"),
        new("Email:Quotas:DailyCodeLimitPerAddress", High),
        new("Email:Quotas:DailyCodeLimitPerAccount", High),
        new("Email:Quotas:DailyInvitationLimitPerInviter", High),
        new("Email:Quotas:DailyInvitationLimitPerOrganization", High),
        new("WhatsApp:DailyAccountCodeLimit", "10000"),
    ];
}
