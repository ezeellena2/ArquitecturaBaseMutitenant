using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services;

/// <summary>La configuración de WhatsApp de la organización del token: el plazo de sus chats (B18).</summary>
public interface IWhatsAppSettingsService
{
    Task<Result<WhatsAppOrganizationSettingsResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result<VersionResponse>> UpdateAsync(UpdateWhatsAppOrganizationSettingsRequest request, CancellationToken cancellationToken);
}

/// <summary>La configuración de WhatsApp de la plataforma: los países que reciben códigos y los plazos de los chats (B18).</summary>
public interface IPlatformWhatsAppSettingsService
{
    Task<Result<PlatformWhatsAppSettingsResponse>> GetAsync(CancellationToken cancellationToken);

    Task<Result<VersionResponse>> UpdateAsync(UpdatePlatformWhatsAppSettingsRequest request, CancellationToken cancellationToken);
}
