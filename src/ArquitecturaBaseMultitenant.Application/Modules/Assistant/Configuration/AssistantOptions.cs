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
    public AssistantUploadOptions Uploads { get; set; } = new();
    public override string ToString() => "AssistantOptions { [REDACTED] }";
}

/// <summary>
/// El lugar de las subidas del Inicio en cada copia (ADR 0064 §4, <c>Assistant:Uploads:*</c>): además del lugar de cada
/// persona, las que corren a la vez, las que esperan y cuántas de esas puede ocupar una organización, y el plazo del cuerpo.
/// Valores de piloto.
/// </summary>
public sealed class AssistantUploadOptions
{
    public int ConcurrencyLimit { get; set; } = 4;
    public int QueueLimit { get; set; } = 8;
    public int QueuePerOrganization { get; set; } = 2;
    public TimeSpan BodyTimeout { get; set; } = TimeSpan.FromSeconds(120);
}

public sealed class AssistantPlatformOptions
{
    public string? ApiKey { get; set; }
    public override string ToString() => "AssistantPlatformOptions { [REDACTED] }";
}
