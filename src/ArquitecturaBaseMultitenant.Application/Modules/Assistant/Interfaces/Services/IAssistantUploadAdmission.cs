using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;

/// <summary>
/// La admisión de una subida del Inicio antes de leer su cuerpo (ADR 0064 §2): el cupo diario de la persona y el tope
/// mensual de la organización, con la misma guarda que admite un turno. Sin cupo, el 429 de siempre y no se guarda nada.
/// </summary>
public interface IAssistantUploadAdmission
{
    Task<Result> AdmitAsync(CancellationToken cancellationToken);
}
