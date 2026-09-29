using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Entrega un aviso a un método de ingreso mediante un canal registrado y el outbox.</summary>
public interface IAccountNoticeChannel
{
    string Key { get; }
    LoginMethodType MethodType { get; }

    void Enqueue(string destination, AccountNotice notice, string culture);
}
