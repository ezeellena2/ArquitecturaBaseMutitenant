using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>
/// El aviso del tope del asistente (D23) para quien lo recibe: el Dueño si la organización usa su clave, el correo de
/// soporte de la plataforma si usa la de la plataforma. Lleva la organización, el uso, el mes civil del tope y el enlace;
/// nunca chats ni claves.
/// </summary>
public sealed record AssistantBudgetAlertNotice(string Recipient, string? RecipientName, AssistantKeyFunding Funding,
    int Threshold, Money Used, Money Budget, string OrganizationName, DateOnly Month, string ActionUrl, string Culture,
    string TimeZoneId)
{
    /// <summary>El tope alcanzado con la clave propia lleva a la configuración, donde el Dueño lo sube; los demás, al uso.</summary>
    public static bool LeadsToSettings(AssistantKeyFunding funding, int threshold) =>
        funding == AssistantKeyFunding.OwnKey && threshold >= AssistantBudgetAlert.ReachedThreshold;

    public override string ToString() => "AssistantBudgetAlertNotice [redacted]";
}
