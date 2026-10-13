using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantOptionsValidator(IHostEnvironment environment) : IValidateOptions<AssistantOptions>
{
    public ValidateOptionsResult Validate(string? name, AssistantOptions options) =>
        options.UseFakeChatClient && !(environment.IsEnvironment("Testing") || environment.IsEnvironment("E2E") && options.IsolatedE2E)
            ? ValidateOptionsResult.Fail("Assistant:UseFakeChatClient requires Testing or an explicitly isolated E2E environment.")
            : Durations(options).Any(value => value <= TimeSpan.Zero || value.TotalMilliseconds > uint.MaxValue - 1)
                ? ValidateOptionsResult.Fail("Assistant durations must be positive and supported by timers.")
                : options.LeaseHeartbeatInterval >= options.LeaseDuration
                    ? ValidateOptionsResult.Fail("Assistant lease heartbeat must precede lease expiration.")
                    : ValidateOptionsResult.Success;

    private static IEnumerable<TimeSpan> Durations(AssistantOptions options) =>
        [options.TurnTimeout, options.LeaseDuration, options.WorkerInterval, options.ExecutionCheckInterval,
            options.LeaseHeartbeatInterval, options.FinalizationTimeout, options.ProviderTimeout,
            options.StreamHeartbeatInterval, options.StreamPollInterval, options.RetentionWorkerInterval,
            options.SnapshotRetentionWorkerInterval, options.BudgetAlertWorkerInterval, options.UnconfirmedFileGrace];
}
