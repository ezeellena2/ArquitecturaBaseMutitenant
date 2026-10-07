using System.Reflection;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

/// <summary>Compone ApiFactory con el modelo sintético; conserva migraciones, rol runtime, UoW y cinco interceptores.</summary>
public sealed class PersonalIsolationFactory : IAsyncLifetime
{
    private readonly ApiFactory _database = new();
    private WebApplicationFactory<Program>? _host;

    internal WebApplicationFactory<Program> Host => _host ?? throw new InvalidOperationException("Initialize the fixture first.");
    internal IServiceProvider Services => Host.Services;

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();
        _host = _database.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.ReplaceService<IModelCustomizer, IsolationModelCustomizer>());
            services.AddScoped<IPersonalWidgetService, PersonalWidgetService>();
            services.AddScoped<IPersonalWidgetReader, PersonalWidgetReader>();
            services.AddScoped<IPersonalWidgetRepository, PersonalWidgetRepository>();
            services.AddScoped<IValidator<ListPersonalWidgetsRequest>, ListPersonalWidgetsRequestValidator>();
            services.AddScoped<IValidator<CreatePersonalWidgetRequest>, CreatePersonalWidgetRequestValidator>();
            services.AddScoped<IValidator<UpdatePersonalWidgetRequest>, UpdatePersonalWidgetRequestValidator>();
            services.AddScoped<IValidator<DeletePersonalWidgetRequest>, DeletePersonalWidgetRequestValidator>();
            services.AddControllers().ConfigureApplicationPartManager(parts =>
                parts.ApplicationParts.Add(new PersonalControllerPart()));
        }));
    }

    internal HttpClient Client() => Host.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    public async ValueTask DisposeAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
        await _database.DisposeAsync();
    }

    private sealed class PersonalControllerPart : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => nameof(PersonalControllerPart);
        public IEnumerable<TypeInfo> Types =>
        [typeof(PersonalWidgetsController).GetTypeInfo(), typeof(BusinessWidgetsController).GetTypeInfo()];
    }
}
