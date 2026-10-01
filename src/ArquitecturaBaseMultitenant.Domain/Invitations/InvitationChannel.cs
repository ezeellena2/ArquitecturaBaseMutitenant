namespace ArquitecturaBaseMultitenant.Domain.Invitations;

/// <summary>Valores de canal; los adaptadores registrados deciden cuáles están disponibles.</summary>
public static class InvitationChannel
{
    public const string Email = "email";

    public static bool IsKnown(string? channel, IReadOnlySet<string> registeredChannels) =>
        channel is not null && registeredChannels.Contains(channel);
}
