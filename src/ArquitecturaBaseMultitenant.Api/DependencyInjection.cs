using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using Microsoft.AspNetCore.Mvc;

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
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddControllers(options => options.Filters.Add(new EmptyJsonBodyContentTypeFilter()));
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = MvcInvalidModelStateResponseFactory.Create);

        return services;
    }
}
