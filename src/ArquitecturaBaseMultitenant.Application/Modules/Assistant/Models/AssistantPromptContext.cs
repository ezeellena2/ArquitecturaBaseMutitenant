namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantPromptContext(string SystemInstructions, IReadOnlyList<AssistantUntrustedData> Data);
public sealed record AssistantUntrustedData(string Kind, string Text);
