namespace ArquitecturaBaseMultitenant.Domain.Results;

/// <summary>
/// Clasifica un error de negocio para que la API elija su respuesta HTTP. Distingue validación,
/// autenticación, permisos, ausencia, conflicto, límite de pedidos y falla.
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    Conflict = 5,
    TooManyRequests = 6,
}
