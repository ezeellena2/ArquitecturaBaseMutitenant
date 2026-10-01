using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Invitations;

public sealed class InvitationOptions
{
    public const string SectionName = "Authentication:Invitations";

    [Range(1, 30)]
    public int LifetimeDays { get; set; } = 7;
}
