namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";
    public AssistantPlatformOptions Platform { get; set; } = new();
    public bool TurnWorkerEnabled { get; set; } = true;
    public bool UseFakeChatClient { get; set; }
    public bool IsolatedE2E { get; set; }
    public TimeSpan TurnTimeout { get; set; } = TimeSpan.FromMinutes(3);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(45);
    public TimeSpan WorkerInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan ExecutionCheckInterval { get; set; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan LeaseHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan FinalizationTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan StreamHeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan StreamPollInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan RetentionWorkerInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan SnapshotRetentionWorkerInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan BudgetAlertWorkerInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan UnconfirmedFileGrace { get; set; } = TimeSpan.FromHours(24);
    public override string ToString() => "AssistantOptions { [REDACTED] }";
}

public sealed class AssistantPlatformOptions
{
    public string? ApiKey { get; set; }
    public override string ToString() => "AssistantPlatformOptions { [REDACTED] }";
}
