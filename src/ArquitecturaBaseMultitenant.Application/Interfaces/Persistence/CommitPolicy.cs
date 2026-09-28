namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Decide si un resultado de negocio fallido confirma las escrituras del caso de uso.</summary>
public enum CommitPolicy
{
    OnSuccess = 0,
    OnAnyResult = 1,
}
