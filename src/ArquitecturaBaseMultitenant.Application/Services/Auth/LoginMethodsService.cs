using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Informa al front qué canales de código y proveedores externos están configurados para ofrecer solo métodos de ingreso disponibles.</summary>
internal sealed class LoginMethodsService(
    IEnumerable<ILoginCodeChannel> channels,
    IGoogleAvailability google,
    TimeProvider timeProvider,
    ILogger<LoginMethodsService> logger) : ILoginMethodsService
{
    public Task<Result<LoginMethodsResponse>> GetLoginMethodsAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<LoginMethodsResponse>(logger, timeProvider, "GetLoginMethods", async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var available = new List<LoginChannelAvailability>();
            foreach (var channel in channels.Where(channel => channel.IsAvailable))
                available.Add(new LoginChannelAvailability(channel.Key, await channel.CountriesAsync(cancellationToken), channel.ContactPhone));
            if (google.IsEnabled)
            {
                available.Add(new LoginChannelAvailability("google", []));
            }

            return Result.Success(new LoginMethodsResponse(available));
        });
}
