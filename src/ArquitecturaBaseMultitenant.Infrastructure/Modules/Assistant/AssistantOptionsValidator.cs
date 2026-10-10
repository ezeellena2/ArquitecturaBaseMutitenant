using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

internal sealed class AssistantOptionsValidator(IHostEnvironment environment) : IValidateOptions<AssistantOptions>
{
    public ValidateOptionsResult Validate(string? name, AssistantOptions options) =>
        options.UseFakeChatClient && !(environment.IsEnvironment("Testing") || environment.IsEnvironment("E2E") && options.IsolatedE2E)
            ? ValidateOptionsResult.Fail("Assistant:UseFakeChatClient requires Testing or an explicitly isolated E2E environment.")
            : ValidateOptionsResult.Success;
}
