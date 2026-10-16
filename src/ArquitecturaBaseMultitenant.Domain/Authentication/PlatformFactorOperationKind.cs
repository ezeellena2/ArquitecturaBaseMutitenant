namespace ArquitecturaBaseMultitenant.Domain.Authentication;

/// <summary>Qué operación del segundo factor quedó confirmada con una clave de idempotencia (E9).</summary>
public enum PlatformFactorOperationKind
{
    Verify,
    Complete,
    Reauthenticate,
}
