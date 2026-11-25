using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// 0064 §9: la hora del pedido en la zona del perfil de quien pidió, con su nombre, que va al final de su mensaje, como en el canal
/// (0054 §1), sin tocar las instrucciones. La zona de la organización corta solo el cupo y el mes (271).
/// </summary>
internal sealed class AssistantRequestTimeBuilder(DisplayFormatter formatter)
{
    internal async Task<string> BuildAsync(DateTime askedAtUtc, AssistantRequestPlace requester, CancellationToken ct)
    {
        var context = await formatter.CreateAsync(requester.Culture, requester.TimeZoneId, ct);
        var zone = await formatter.FormatTimeZoneAsync(requester.TimeZoneId, context, ct) ?? requester.TimeZoneId;
        return AssistantTexts.Format("Runtime.RequestTime", context.Profile, formatter.FormatInstant(askedAtUtc, context), zone);
    }
}
