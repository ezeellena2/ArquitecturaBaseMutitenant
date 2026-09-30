using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Valida la configuración general de correo al arrancar, según el modo de entrega. Rechaza un pickup fuera del entorno de desarrollo.</summary>
internal sealed class EmailOptionsValidator(IHostEnvironment environment) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options) =>
        options.Delivery == EmailDelivery.PickupDirectory
        && !environment.IsDevelopment()
        && !environment.IsEnvironment("Testing")
            ? ValidateOptionsResult.Fail("Email:Delivery: PickupDirectory is allowed only in Development or Testing.")
            : ValidateOptionsResult.Success;
}
