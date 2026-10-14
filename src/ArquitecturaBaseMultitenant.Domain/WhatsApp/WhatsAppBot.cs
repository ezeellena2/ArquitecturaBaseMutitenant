using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// El bot de un número de la organización, con «Además»: preguntar por promociones al terminar su parte de la primera
/// conversación, ofrecer vincular la cuenta (D2) y pasar a los empleados directo a su equipo, sin menú.
/// </summary>
public sealed class WhatsAppBot : WhatsAppBotBase, ITenantOwned
{
    private WhatsAppBot() { }

    public Guid TenantId { get; private set; }
    public Guid ChannelId { get; private set; }
    public bool AskPromotions { get; private set; }
    public bool OfferAccountLink { get; private set; }
    public Guid? EmployeesTeamId { get; private set; }

    public static WhatsAppBot Create(Guid channelId)
    {
        if (channelId == Guid.Empty) throw new ArgumentException("A channel is required.", nameof(channelId));
        return new WhatsAppBot { ChannelId = channelId };
    }

    public Result Update(string welcome, string outOfHours, string timeZoneId, IEnumerable<WhatsAppBotHours> days,
        IEnumerable<WhatsAppBotOption> menu, bool askPromotions, bool offerAccountLink, Guid? employeesTeamId)
    {
        if (employeesTeamId == Guid.Empty) throw new ArgumentException("Empty team.", nameof(employeesTeamId));
        var applied = Apply(welcome, outOfHours, timeZoneId, days, menu);
        if (applied.IsFailure) return applied;
        AskPromotions = askPromotions; OfferAccountLink = offerAccountLink; EmployeesTeamId = employeesTeamId;
        return Result.Success();
    }

    public override bool LeaveTeam(Guid teamId)
    {
        var changed = base.LeaveTeam(teamId);
        if (EmployeesTeamId != teamId) return changed;
        EmployeesTeamId = null;
        return true;
    }
}
