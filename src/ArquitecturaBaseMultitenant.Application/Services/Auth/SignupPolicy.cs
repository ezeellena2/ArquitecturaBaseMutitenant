using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Consulta la configuración global y aplica la política que permite o cierra el registro de personas.</summary>
internal sealed class SignupPolicy(IPlatformSettingsReader settings)
{
    public async Task<Result> CanRegisterAsync(CancellationToken cancellationToken)
    {
        var row = await settings.FindAsync(cancellationToken)
            ?? throw new InvalidOperationException("PlatformSettings has not been seeded.");
        return PlatformSettings.CanRegisterConsumer(row.ConsumerSignup);
    }
}
