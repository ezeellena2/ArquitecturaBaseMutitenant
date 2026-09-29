namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;

/// <summary>Protege el contenido del outbox mientras espera el envío.</summary>
public interface IPayloadProtector
{
    string Protect(string payload);

    string Unprotect(string protectedPayload);
}
