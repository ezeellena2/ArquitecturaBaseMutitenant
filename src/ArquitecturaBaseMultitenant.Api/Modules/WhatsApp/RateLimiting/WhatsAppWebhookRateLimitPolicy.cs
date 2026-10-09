using System.Threading.RateLimiting;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
namespace ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.RateLimiting;
internal sealed class WhatsAppWebhookRateLimitPolicy : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => RejectAsync;
    private static async ValueTask RejectAsync(OnRejectedContext rejection, CancellationToken ct)
    {
        var seconds = rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
            ? (int)Math.Clamp(Math.Ceiling(delay.TotalSeconds), 1, int.MaxValue) : 1;
        var context = rejection.HttpContext;
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = RetryAfterHeaderFormatter.Format(seconds);
        var error = WhatsAppErrors.WebhookTooManyRequests(seconds);
        var problem = ProblemDetailsMapper.FromError(error, context.RequestServices.GetRequiredService<ProblemDetailsFactory>(), context);
        problem.Extensions["retryAfter"] = seconds;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
