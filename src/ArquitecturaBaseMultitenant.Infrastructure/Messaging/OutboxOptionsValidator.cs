using ArquitecturaBaseMultitenant.Application.Configuration.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging;

/// <summary>
/// N8 y ADR 0066 §5: la entrega más lenta de un canal y su confirmación caben en el apagado del host, así apagar la Api no
/// deja a medias un envío que ya salió; y la reserva dura más que las dos, así una viva nunca se toma por interrumpida.
/// </summary>
internal sealed class OutboxOptionsValidator(IOptions<HostOptions> host) : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(string? name, OutboxOptions options)
    {
        var failures = new List<string>();
        var completion = TimeSpan.FromSeconds(options.CompletionTimeoutSeconds);
        var shutdown = host.Value.ShutdownTimeout;
        foreach (var (channel, own) in options.Channels)
        {
            if (own is null) { failures.Add($"{OutboxOptions.SectionName}:Channels:{channel} is empty."); continue; }
            if (own.DeliveryTimeoutSeconds is < 5 or > 60)
                failures.Add($"{OutboxOptions.SectionName}:Channels:{channel}:DeliveryTimeoutSeconds must be between 5 and 60.");
            if (own.MaxAttempts is < 1 or > 20)
                failures.Add($"{OutboxOptions.SectionName}:Channels:{channel}:MaxAttempts must be between 1 and 20.");
        }
        var slowest = options.Channels.Keys.Select(options.DeliveryTimeoutFor)
            .Append(TimeSpan.FromSeconds(options.DeliveryTimeoutSeconds)).Max();
        if (slowest + completion >= shutdown)
            failures.Add($"{OutboxOptions.SectionName}: the slowest DeliveryTimeoutSeconds plus CompletionTimeoutSeconds must fit "
                + "in the host shutdown timeout (HostOptions.ShutdownTimeout).");
        if (TimeSpan.FromSeconds(options.LeaseSeconds) <= slowest + completion)
            failures.Add($"{OutboxOptions.SectionName}:LeaseSeconds must be longer than the slowest delivery plus CompletionTimeoutSeconds.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
