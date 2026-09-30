namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>Genera el secreto temporal que se envía para probar posesión de un método de ingreso; la persistencia recibe solo su hash.</summary>
public interface ILoginCodeGenerator
{
    /// <summary>Un código numérico aleatorio con la cantidad de dígitos configurada.</summary>
    string Generate();
}
