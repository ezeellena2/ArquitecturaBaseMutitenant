using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Las plantillas de la organización del claim. Exige la transacción del caso de uso.</summary>
public interface IWhatsAppTemplateRepository
{
    /// <summary>La plantilla de la organización; con versión, la exige.</summary>
    Task<WhatsAppTemplate?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    void Add(WhatsAppTemplate template);
    void Remove(WhatsAppTemplate template);
}

/// <summary>Las plantillas «Para consultas» de Plataforma. Exige la transacción del caso de uso.</summary>
public interface IPlatformWhatsAppTemplateRepository
{
    Task<PlatformWhatsAppTemplate?> GetAsync(Guid id, uint? expectedVersion, CancellationToken ct);
    void Add(PlatformWhatsAppTemplate template);
    void Remove(PlatformWhatsAppTemplate template);
}

/// <summary>
/// Lo que Meta dice de cada plantilla por cuenta de WhatsApp (global, sin organización). Lo escriben el webhook y la
/// sincronización, bajo el lock de la cuenta.
/// </summary>
public interface IWhatsAppTemplateStateRepository
{
    Task LockAsync(string wabaId, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppTemplateState>> ListAsync(string wabaId, CancellationToken ct);
    void Add(WhatsAppTemplateState state);
    void Remove(WhatsAppTemplateState state);
}
