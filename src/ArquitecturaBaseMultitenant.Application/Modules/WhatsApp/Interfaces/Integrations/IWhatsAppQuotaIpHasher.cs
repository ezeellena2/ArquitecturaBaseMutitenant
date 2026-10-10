namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>Identificador pseudónimo estable entre réplicas y limitado al número de plataforma y día UTC.</summary>
public interface IWhatsAppQuotaIpHasher
{
    string Hash(string? ipAddress, string phoneNumberId, DateOnly dayUtc);
}
