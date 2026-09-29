using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Copia el patrón de ArquitecturaBase sin anunciar proveedores no configurados.</summary>
internal sealed class LoginMethodsService(
    IEnumerable<ILoginCodeChannel> channels,
    IGoogleAvailability google,
    TimeProvider timeProvider,
    ILogger<LoginMethodsService> logger) : ILoginMethodsService
{
    public Task<Result<LoginMethodsResponse>> GetLoginMethodsAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<LoginMethodsResponse>(logger, timeProvider, "GetLoginMethods", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var available = channels.Select(channel => new LoginChannelAvailability(channel.Key, []))
                .ToList();
            if (google.IsEnabled)
            {
                available.Add(new LoginChannelAvailability("google", []));
            }

            return Task.FromResult<Result<LoginMethodsResponse>>(new LoginMethodsResponse(available));
        });
}
