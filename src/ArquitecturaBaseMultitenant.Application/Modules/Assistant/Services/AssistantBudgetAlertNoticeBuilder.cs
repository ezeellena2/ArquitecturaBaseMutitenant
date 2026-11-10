using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// A quién va el aviso del tope y con qué datos (D23): con la clave propia, a cada Dueño activo con correo verificado, en
/// su cultura y zona; con la de la plataforma, al correo de soporte, en la cultura predeterminada. Suma el nombre de la
/// organización, el mes del tope y el enlace al uso o, con el tope propio alcanzado, a la configuración.
/// </summary>
internal sealed class AssistantBudgetAlertNoticeBuilder(ITenantAdminReader owners, IPlatformSettingsReader settings,
    IUserRepository users, UserCultureResolver cultures, ITenantReader tenants, IAssistantFinanceRepository finance,
    IPublicOrigin origin)
{
    private const string OrganizationUsage = "org/asistente/uso";
    private const string OrganizationSettings = "org/asistente/configuracion";
    private const string PlatformUsage = "plataforma/asistente/uso";

    /// <summary>Sin destinatarios devuelve una lista vacía y el aviso queda pendiente.</summary>
    internal async Task<IReadOnlyList<(Guid? UserId, AssistantBudgetAlertNotice Notice)>> BuildAsync(AssistantBudgetAlert alert,
        CancellationToken ct)
    {
        var recipients = new List<(Guid? UserId, string Address, string? Name, string Culture, string Zone)>();
        if (alert.Funding == AssistantKeyFunding.OwnKey)
        {
            foreach (var recipient in await owners.ListActiveVerifiedEmailRecipientsAsync(alert.OrganizationId, ct))
                if (await users.GetByIdAsync(recipient.UserId, ct) is { } account)
                    recipients.Add((recipient.UserId, recipient.Email.Value, account.DisplayName, account.Culture, account.TimeZoneId));
        }
        else if ((await settings.FindAsync(ct))?.SupportEmail is { } support)
        {
            var profile = await cultures.ResolveAsync(null, null, ct);
            recipients.Add((null, support.Value, null, profile.Culture.Name, TimeZoneInfo.Utc.Id));
        }
        if (recipients.Count == 0) return [];
        var month = await finance.GetMonthAsync(alert.MonthId, ct)
            ?? throw new InvalidOperationException("The budget alert has no month.");
        var organization = await tenants.FindByIdAsync(alert.OrganizationId, ct)
            ?? throw new InvalidOperationException("The budget alert has no organization.");
        var url = origin.Absolute(AssistantBudgetAlertNotice.LeadsToSettings(alert.Funding, alert.Threshold) ? OrganizationSettings
            : alert.Funding == AssistantKeyFunding.OwnKey ? OrganizationUsage : PlatformUsage);
        return [.. recipients.Select(recipient => (recipient.UserId, new AssistantBudgetAlertNotice(recipient.Address, recipient.Name,
            alert.Funding, alert.Threshold, alert.Used, alert.Budget, organization.Name, month.Month, url, recipient.Culture,
            recipient.Zone)))];
    }
}
