namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>
/// El nombre de la aplicación, el del remitente y el logo de la configuración de arranque (<c>Email:AppName</c>,
/// <c>Email:Smtp:FromName</c> y <c>Email:LogoUrl</c>): el respaldo de «Nombre y correos» de Plataforma mientras no tenga uno
/// guardado (decisión 273).
/// </summary>
public interface IEmailBrandingDefaults
{
    string AppName { get; }

    string SenderName { get; }

    /// <summary>El logo de arranque; null si no hay.</summary>
    string? LogoUrl { get; }

    /// <summary>La dirección del remitente (<c>Email:Smtp:FromAddress</c>), que fija la instalación; null si no hay.</summary>
    string? SenderAddress { get; }
}
