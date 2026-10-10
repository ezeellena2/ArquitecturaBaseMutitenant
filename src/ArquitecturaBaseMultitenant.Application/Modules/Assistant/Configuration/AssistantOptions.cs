namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;

public sealed class AssistantOptions
{
    public const string SectionName = "Assistant";
    public AssistantPlatformOptions Platform { get; set; } = new();
    public bool UseFakeChatClient { get; set; }
    public bool IsolatedE2E { get; set; }
    public string? ProviderReferenceFile { get; set; }
    public override string ToString() => "AssistantOptions { [REDACTED] }";
}

public sealed class AssistantPlatformOptions
{
    public string? ApiKey { get; set; }
    public override string ToString() => "AssistantPlatformOptions { [REDACTED] }";
}
