namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ModuleControllersTests
{
    static partial void AddFormsControllers(Microsoft.Extensions.DependencyInjection.IServiceCollection registrations)
    {
        ArquitecturaBaseMultitenant.Application.Modules.Forms.FormsModule.AddFormsModule(registrations);
        ArquitecturaBaseMultitenant.Api.Modules.Forms.FormsApiModule.AddFormsModule(registrations);
    }
}
