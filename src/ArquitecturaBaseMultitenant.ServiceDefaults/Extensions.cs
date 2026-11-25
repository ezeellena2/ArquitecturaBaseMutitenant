using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Basado en la plantilla aspire-servicedefaults 13.5.4: https://aka.ms/aspire/service-defaults
/// <summary>
/// Comparte descubrimiento, resiliencia, health checks y telemetría entre los servicios Aspire.
/// Los endpoints de salud separan disponibilidad general de vida del proceso.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private static readonly string[] LiveTags = ["live"];

    /// <summary>El ambiente del arnés de integración.</summary>
    private const string TestingEnvironment = "Testing";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Sin reintentar un POST, PUT, PATCH ni DELETE (N18): repetido, duplica el efecto en el proveedor (dos
            // mensajes, dos cobros). Lo que conviene reintentar lo reintenta la cola de cada módulo.
            http.AddStandardResilienceHandler(resilience => resilience.Retry.DisableForUnsafeHttpMethods());
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Fuera de Development y Testing, la consola escribe una línea JSON por log con sus scopes (N10): el traceId y la
        // ruta que pone ASP.NET Core en cada pedido, y los ids de la persona y de la organización de
        // Api/RequestContext/RequestLogScopeMiddleware. Así el código que ve la persona en un error se encuentra en los
        // logs aunque no haya exportador.
        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment(TestingEnvironment))
        {
            builder.Logging.AddJsonConsole(console =>
            {
                console.IncludeScopes = true;
                console.UseUtcTimestamp = true;
                console.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            });
        }

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // Aciertos, misses, lecturas directas y caídas de la caché compartida (ADR 0052).
                    .AddMeter("ArquitecturaBaseMultitenant.Cache")
                    // Enviados y descartados de la cola, por canal (ADR 0066).
                    .AddMeter("ArquitecturaBaseMultitenant.Messaging");
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation()
                    // Spans de cada comando SQL, dentro de la traza del request que los provocó.
                    .AddNpgsql();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), LiveTags);

        return builder;
    }

    // En todos los entornos: los probes del orquestador los consultan en producción, que es donde importan. La
    // respuesta por defecto es una sola palabra ("Healthy"/"Unhealthy"), sin el detalle de cada check: no cuenta
    // qué dependencias tiene la aplicación ni por qué falló.
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Readiness: corre todos los checks registrados (el de la base se suma con persistencia en la E2).
        app.MapHealthChecks(HealthEndpointPath);

        // Liveness: solo los checks con el tag "live", que no tocan dependencias externas. Responde si el proceso
        // sigue en pie; si esto falla, reiniciar el contenedor es la reacción correcta.
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        return app;
    }
}
