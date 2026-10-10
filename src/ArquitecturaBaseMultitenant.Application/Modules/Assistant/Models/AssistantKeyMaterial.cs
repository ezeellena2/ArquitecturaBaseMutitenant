namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Material privado de infraestructura: nunca se devuelve desde un servicio HTTP ni se audita.</summary>
public sealed record AssistantKeyMaterial(byte[]? Ciphertext, string? Suffix, bool HasOverride)
{
    public override string ToString() => "AssistantKeyMaterial { [REDACTED] }";
}
