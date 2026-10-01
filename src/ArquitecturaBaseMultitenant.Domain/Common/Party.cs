namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>
/// Identifica a la persona o a la empresa como parte de una interacción compartida. PartyPolicy lo usa para
/// exigir que una acción se realice desde la parte correspondiente.
/// </summary>
public enum Party
{
    Consumer,
    Business,
}
