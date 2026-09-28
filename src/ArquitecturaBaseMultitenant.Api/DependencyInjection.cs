using ArquitecturaBaseMultitenant.Api.ErrorHandling;

namespace ArquitecturaBaseMultitenant.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            ProblemDetailsMapper.CompleteFrameworkProblem(context.ProblemDetails);
            ProblemDetailsMapper.AddTraceId(context.ProblemDetails, context.HttpContext);
        });
        services.AddControllers();

        return services;
    }
}
