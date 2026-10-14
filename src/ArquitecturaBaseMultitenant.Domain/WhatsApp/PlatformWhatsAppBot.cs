using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// El bot del número de Plataforma cuando atiende consultas (ADR 0038, precisión del 03/10): sin «Además» y con la opción
/// del sistema «Entrar a mi cuenta», que manda el enlace de la Etapa 8. Esa opción no se guarda ni se borra, pero ocupa
/// un lugar de la lista. Con las consultas apagadas responde el bot de ingreso de la Etapa 8.
/// </summary>
public sealed class PlatformWhatsAppBot : WhatsAppBotBase, IGlobalSecurityAudited
{
    SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.PlatformSettingsChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => null;

    public static readonly Guid SingletonId = new("2f0c6c55-6b39-4b8f-9f53-2d6b1c0e7a11");
    /// <summary>El identificador fijo de «Entrar a mi cuenta»: es el de su fila en la lista de WhatsApp.</summary>
    public static readonly Guid SignInOptionId = new("8d3f2a9e-41c7-4e0b-b6a2-5c9e7f1d3b40");

    private PlatformWhatsAppBot() : base(SingletonId) { }

    protected override int SystemOptions => 1;

    public static PlatformWhatsAppBot Create() => new();

    public Result Update(string welcome, string outOfHours, string timeZoneId, IEnumerable<WhatsAppBotHours> days,
        IEnumerable<WhatsAppBotOption> menu) =>
        Apply(welcome, outOfHours, timeZoneId, days, menu.Where(option => option.Id != SignInOptionId));
}
