using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ApplicationServicesTests
{
    static partial void AddFormsServices(IServiceCollection services) =>
        ArquitecturaBaseMultitenant.Application.Modules.Forms.FormsModule.AddFormsModule(services);
}
