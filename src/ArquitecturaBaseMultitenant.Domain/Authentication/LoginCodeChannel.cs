namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Claves de canal. Cada módulo aporta su implementación y el registro decide cuáles están disponibles.</summary>
public static class LoginCodeChannel
{
    public const string Email = "email";

    public static bool IsKnown(string? channel, IReadOnlySet<string> registeredChannels) =>
        channel is not null && registeredChannels.Contains(channel);
}
