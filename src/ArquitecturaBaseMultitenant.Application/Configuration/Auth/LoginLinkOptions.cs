using System.ComponentModel.DataAnnotations;
namespace ArquitecturaBaseMultitenant.Application.Configuration.Auth;
public sealed class LoginLinkOptions
{
    public const string SectionName = "Authentication:LoginLink";
    [Range(1, 86400)] public int ResendCooldownSeconds { get; set; } = 60;
    [Range(1, 1440)] public int RequestWindowMinutes { get; set; } = 15;
    [Range(1, 100)] public int MaxRequestsPerWindow { get; set; } = 5;
}
