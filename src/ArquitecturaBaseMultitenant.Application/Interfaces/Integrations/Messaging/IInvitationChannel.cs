namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Un canal registrado para invitaciones; su implementación usa el outbox.</summary>
public interface IInvitationChannel
{
    string Key { get; }

    void Enqueue(string destination, string loginUrl, string culture);
}
