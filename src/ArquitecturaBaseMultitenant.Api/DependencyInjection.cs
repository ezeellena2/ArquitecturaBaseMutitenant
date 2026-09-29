using System.Threading.RateLimiting;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
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
        services.AddScoped<IdempotencyFilter>();
        services.AddOpenApiDocumentation();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                var delay = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? retryAfter : TimeSpan.FromSeconds(1);
                var seconds = (int)Math.Clamp(Math.Ceiling(delay.TotalSeconds), 1, int.MaxValue);
                var httpContext = context.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                httpContext.Response.Headers.RetryAfter = RetryAfterHeaderFormatter.Format(seconds);

                var code = ApiErrorCodes.TooManyRequests;
                var problem = ProblemDetailsMapper.Create(
                    ErrorType.TooManyRequests, code, ErrorTexts.Get(code));
                problem.Extensions["retryAfter"] = seconds;
                ProblemDetailsMapper.AddTraceId(problem, httpContext);
                await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = httpContext,
                        ProblemDetails = problem,
                    });
            };
        });
        services.ConfigureHttpJsonOptions(options => JsonConfiguration.ConfigureJson(options.SerializerOptions));
        services.AddControllers(options => options.Filters.Add(new EmptyJsonBodyContentTypeFilter()))
            .AddJsonOptions(options => JsonConfiguration.ConfigureJson(options.JsonSerializerOptions));
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = MvcInvalidModelStateResponseFactory.Create);

        return services;
    }
}
