using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal sealed class EmailOptionsValidator(IHostEnvironment environment) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options) =>
        options.Delivery == EmailDelivery.PickupDirectory
        && !environment.IsDevelopment()
        && !environment.IsEnvironment("Testing")
            ? ValidateOptionsResult.Fail("Email:Delivery: PickupDirectory is allowed only in Development or Testing.")
            : ValidateOptionsResult.Success;
}
