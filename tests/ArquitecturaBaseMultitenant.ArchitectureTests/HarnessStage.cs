namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Indica la última etapa cerrada que el arnés puede exigir. Permite comprobar los entregables ya
/// comprometidos sin adelantar requisitos de etapas pendientes.
/// </summary>
internal static class HarnessStage
{
    public const int Closed = 2;
}
