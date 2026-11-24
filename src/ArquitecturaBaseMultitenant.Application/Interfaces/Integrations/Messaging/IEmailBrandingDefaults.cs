namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>
/// El nombre de la aplicación y el del remitente de la configuración de arranque (<c>Email:AppName</c> y
/// <c>Email:Smtp:FromName</c>): el respaldo de «Nombre y correos» de Plataforma mientras no tenga uno guardado (decisión 273).
/// </summary>
public interface IEmailBrandingDefaults
{
    string AppName { get; }

    string SenderName { get; }
}
