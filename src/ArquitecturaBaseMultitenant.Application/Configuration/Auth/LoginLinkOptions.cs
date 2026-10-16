using System.ComponentModel.DataAnnotations;
namespace ArquitecturaBaseMultitenant.Application.Configuration.Auth;
public sealed class LoginLinkOptions
{
    public const string SectionName = "Authentication:LoginLink";
    [Range(1, 86400)] public int ResendCooldownSeconds { get; set; } = 60;
    [Range(1, 1440)] public int RequestWindowMinutes { get; set; } = 15;
    [Range(1, 100)] public int MaxRequestsPerWindow { get; set; } = 5;
    /// <summary>Vida del enlace de ingreso (E9). Los textos que la anuncian dicen «10 minutos»: cambiarla exige revisarlos.</summary>
    [Range(1, 60)] public int LifetimeMinutes { get; set; } = 10;
}
