namespace ArquitecturaBaseMultitenant.Domain.Features;

/// <summary>
/// Origen de la habilitación de un módulo. Plan, Manual y Trial los administra Plataforma; Approved (un pedido
/// aprobado) y Subscription (un pago) los escribe la etapa del cobro (ADR 0069), y van al final porque se guardan
/// como texto. Plan queda solo para leer filas viejas.
/// </summary>
public enum TenantFeatureSource
{
    Plan,
    Manual,
    Trial,
    Approved,
    Subscription,
}
