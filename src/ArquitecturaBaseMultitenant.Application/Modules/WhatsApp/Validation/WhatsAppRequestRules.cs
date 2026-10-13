using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Validation;

/// <summary>Reglas de forma compartidas por los pedidos de atención; las de negocio viven en los servicios.</summary>
internal static class WhatsAppRequestRules
{
    internal static bool IsValidSchedule(WhatsAppBotScheduleInput? schedule) =>
        schedule is { TimeZoneId: { Length: > 0 }, Days: { } days } && days.Count <= 7
        && days.All(day => day is { Day: { } weekday, From: { } from, To: { } to } && Enum.IsDefined(weekday) && from < to)
        && days.Select(day => day.Day).Distinct().Count() == days.Count;

    internal static bool IsValidMenu(IReadOnlyList<WhatsAppBotMenuOptionInput>? menu) =>
        menu is not null && menu.Count <= WhatsAppFeature.MaxMenuOptions && menu.All(option =>
            option is { Label: { Length: > 0 and <= WhatsAppTextLimits.MenuLabel }, Action: { } action } && Enum.IsDefined(action)
            && (action == WhatsAppBotAction.Team
                ? option.TeamId is { } team && team != Guid.Empty && option.Reply is null
                : option.TeamId is null && option.Reply is { Length: > 0 and <= WhatsAppTextLimits.BotText }));

    internal static bool AreValidIds(IReadOnlyList<Guid>? ids) =>
        ids is not null && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count;

    internal static bool IsValidNotice(int? minutes) => minutes is null || WhatsAppFeature.NotifyAfterMinutesOptions.Contains(minutes.Value);

    internal static bool AreValidVariables(IReadOnlyList<string>? values) =>
        values is null || values.Count <= WhatsAppFeature.MaxTemplateVariables
        && values.All(value => value is { Length: > 0 and <= WhatsAppTextLimits.TemplateVariable });

    internal static bool AreValidTemplateVariables(IReadOnlyList<WhatsAppTemplateVariableInput>? values) =>
        values is null || values.Count <= WhatsAppFeature.MaxTemplateVariables && values.All(value =>
            value is { Label: { Length: > 0 and <= TextLimits.ShortName }, Example: { Length: > 0 and <= WhatsAppTextLimits.TemplateVariable } });

    internal static bool AreValidTags(IReadOnlyList<string>? tags) =>
        tags is null || tags.Count <= WhatsAppFeature.MaxContactTags && tags.All(tag => tag is { Length: > 0 and <= WhatsAppTextLimits.Tag })
        && tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() == tags.Count;

    internal static bool IsValidAudience(WhatsAppAudienceKind? kind, string? tag) => kind switch
    {
        WhatsAppAudienceKind.AllAccepting => tag is null,
        WhatsAppAudienceKind.Tag => tag is { Length: > 0 and <= WhatsAppTextLimits.Tag },
        _ => false,
    };
}
