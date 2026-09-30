using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>Reserva antes del model binding y guarda el resultado solo cuando la acción terminó.</summary>
public sealed class IdempotencyFilter(
    IIdempotencyStore store,
    ICurrentUser currentUser,
    ITenantContext tenantContext) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        if (!Guid.TryParse(request.Headers["Idempotency-Key"], out var key) || key == Guid.Empty)
        {
            context.Result = Problem(httpContext, ErrorType.Validation,
                ApiErrorCodes.IdempotencyKeyRequired, StatusCodes.Status400BadRequest);
            return;
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        var bodyHash = Convert.ToHexString(await SHA256.HashDataAsync(
            request.Body, httpContext.RequestAborted));
        request.Body.Position = 0;
        var route = string.Concat(request.PathBase, request.Path, request.QueryString);

        var reservation = await store.ReserveAsync(
            tenantContext.TenantId,
            currentUser.UserId ?? Guid.Empty,
            key,
            bodyHash,
            route,
            httpContext.RequestAborted);

        switch (reservation.State)
        {
            case IdempotencyReservationState.Reused:
                context.Result = Problem(httpContext, ErrorType.Validation,
                    ApiErrorCodes.IdempotencyKeyReused, StatusCodes.Status422UnprocessableEntity);
                return;
            case IdempotencyReservationState.InProgress:
                context.Result = Problem(httpContext, ErrorType.Conflict,
                    ApiErrorCodes.InProgress, StatusCodes.Status409Conflict);
                return;
            case IdempotencyReservationState.Replay:
                httpContext.Response.Headers["Idempotent-Replayed"] = "true";
                context.Result = new ContentResult
                {
                    StatusCode = reservation.ResponseStatusCode,
                    Content = reservation.ResponseBody,
                    ContentType = reservation.ResponseStatusCode >= 400
                        ? "application/problem+json"
                        : "application/json",
                };
                return;
            case IdempotencyReservationState.Reserved:
                await ExecuteReservedAsync(context, next, reservation.Id);
                return;
            default:
                throw new InvalidOperationException("Unknown idempotency reservation state.");
        }
    }

    private async Task ExecuteReservedAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next,
        Guid reservationId)
    {
        var response = context.HttpContext.Response;
        var originalBody = response.Body;
        await using var capture = new MemoryStream();
        response.Body = capture;
        var copyResponse = false;
        var preserveReservation = false;
        try
        {
            var executed = await next();
            if (executed.Exception is not null && !executed.ExceptionHandled)
            {
                await store.ReleaseAsync(reservationId, CancellationToken.None);
                return;
            }

            if (response.StatusCode is >= 200 and <= 499)
            {
                preserveReservation = true;
                var body = Encoding.UTF8.GetString(capture.ToArray());
                await store.CompleteAsync(reservationId, response.StatusCode, body,
                    CancellationToken.None);
            }
            else
            {
                await store.ReleaseAsync(reservationId, CancellationToken.None);
            }

            copyResponse = true;
        }
        catch
        {
            // Si falló la escritura de la respuesta luego del commit, la reserva queda en curso.
            if (!preserveReservation)
            {
                await store.ReleaseAsync(reservationId, CancellationToken.None);
            }

            throw;
        }
        finally
        {
            response.Body = originalBody;
            if (copyResponse)
            {
                capture.Position = 0;
                await capture.CopyToAsync(originalBody, context.HttpContext.RequestAborted);
            }
        }
    }

    private static ObjectResult Problem(HttpContext context, ErrorType type, string code, int statusCode)
    {
        var details = ProblemDetailsMapper.Create(type, code, ErrorTexts.Get(code), statusCode);
        ProblemDetailsMapper.AddTraceId(details, context);
        var result = new ObjectResult(details) { StatusCode = statusCode };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
