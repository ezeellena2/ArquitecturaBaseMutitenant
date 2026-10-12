using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Idempotency;

/// <summary>Reserva antes del model binding y guarda el resultado solo cuando la acción terminó.</summary>
public sealed class IdempotencyFilter(
    IIdempotencyStore store,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IPublicSiteContext publicSiteContext) : IAsyncResourceFilter, IAsyncResultFilter
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

        if (context.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().SingleOrDefault()?.Mode
            is IdempotencyMode.DurableReference or IdempotencyMode.DurableStream)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<IdempotencyDurableAttribute>().SingleOrDefault() is { } durable)
                await ExecuteDurableAsync(context, next, key, durable);
            else await next(); // Unimplemented stream/upload protocols retain their honest stubs.
            return;
        }

        request.EnableBuffering();
        request.Body.Position = 0;
        var bodyHash = Convert.ToHexString(await SHA256.HashDataAsync(
            request.Body, httpContext.RequestAborted));
        request.Body.Position = 0;
        var route = string.Concat(request.PathBase, request.Path, request.QueryString);
        // La organización resuelta es parte de la ruta lógica; el Host arbitrario nunca da pertenencia.
        if (publicSiteContext.BusinessTenantId is { } businessTenantId)
            route = string.Concat(route, "|public-site:", businessTenantId.ToString("D"));

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
                if (context.ActionDescriptor.EndpointMetadata.OfType<IdempotencyReplayAttribute>().SingleOrDefault() is { } metadata)
                {
                    var verifier = (IIdempotencyReplayVerifier)httpContext.RequestServices.GetRequiredService(metadata.VerifierType);
                    var checkedReplay = await verifier.ValidateAsync(request, reservation.ResponseStatusCode!.Value, reservation.ResponseBody, httpContext.RequestAborted);
                    if (checkedReplay.IsFailure)
                    {
                        var details = ProblemDetailsMapper.FromError(checkedReplay.Error,
                            httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>(), httpContext);
                        var rejected = new ObjectResult(details) { StatusCode = details.Status };
                        rejected.ContentTypes.Add("application/problem+json");
                        context.Result = rejected;
                        return;
                    }
                }
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

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var binding = context.HttpContext.RequestServices.GetService<IdempotentRequestContext>();
        if (binding?.ReservationId is { } id && context.Result is IIdempotentReferencedResult referenced)
        {
            binding.HasReference = true;
            // The private UoW has committed. Complete before writing JSON and independently of the client disconnect.
            await store.CompleteReferenceAsync(id, referenced.Reference, CancellationToken.None);
        }
        await next();
    }

    private async Task ExecuteDurableAsync(ResourceExecutingContext context, ResourceExecutionDelegate next,
        Guid key, IdempotencyDurableAttribute metadata)
    {
        var http = context.HttpContext;
        var request = http.Request;
        request.EnableBuffering();
        request.Body.Position = 0;
        var digest = await DurableDigestAsync(request, http.RequestAborted);
        request.Body.Position = 0;
        var binding = http.RequestServices.GetRequiredService<IdempotentRequestContext>();
        binding.Request = new IdempotentRequest(key, digest, string.Concat(request.PathBase, request.Path, request.QueryString),
            string.Concat(request.Scheme, "://", request.Host));
        var verifier = (IIdempotentRequestVerifier)http.RequestServices.GetRequiredService(metadata.VerifierType);
        var recovered = await verifier.ValidateAsync(binding.Request, http.RequestAborted);
        if (recovered.IsFailure)
        {
            context.Result = FromError(http, recovered.Error);
            return;
        }
        if (recovered.Value is { } reference)
        {
            await ReplayDurableAsync(context, reference);
            return;
        }
        var reservation = await store.ReserveAsync(tenantContext.TenantId, currentUser.UserId ?? Guid.Empty,
            key, digest, binding.Request.Route, http.RequestAborted);
        switch (reservation.State)
        {
            case IdempotencyReservationState.Reused:
                context.Result = Problem(http, ErrorType.Validation, ApiErrorCodes.IdempotencyKeyReused, 422);
                return;
            case IdempotencyReservationState.InProgress:
                // A commit may have landed between preflight and reservation (or its publication may have failed).
                recovered = await verifier.ValidateAsync(binding.Request, http.RequestAborted);
                if (recovered.IsFailure) context.Result = FromError(http, recovered.Error);
                else if (recovered.Value is { } committed) await ReplayDurableAsync(context, committed);
                else context.Result = Problem(http, ErrorType.Conflict, ApiErrorCodes.InProgress, 409);
                return;
            case IdempotencyReservationState.Replay:
                if (reservation.Reference is not { } completed)
                    context.Result = Problem(http, ErrorType.Validation, ApiErrorCodes.IdempotencyKeyReused, 422);
                else await ReplayDurableAsync(context, completed);
                return;
            case IdempotencyReservationState.Reserved:
                binding.ReservationId = reservation.Id;
                try { await next(); }
                finally
                {
                    if (!binding.HasReference) await store.ReleaseAsync(reservation.Id, CancellationToken.None);
                }
                return;
            default: throw new InvalidOperationException("Unknown durable reservation state.");
        }
    }

    private static async Task ReplayDurableAsync(ResourceExecutingContext context, IdempotentReference reference)
    {
        var http = context.HttpContext;
        var resolver = http.RequestServices.GetServices<IIdempotentReplayResolver>().Single(value => value.Kind == reference.Kind);
        var result = await resolver.ResolveAsync(reference, http.RequestAborted);
        if (result.IsFailure) { context.Result = FromError(http, result.Error); return; }
        http.Response.Headers["Idempotent-Replayed"] = "true";
        context.Result = new IdempotentReferenceResult(result.Value.ResponseValue!, result.Value.Reference);
    }

    private static ObjectResult FromError(HttpContext http, Error error)
    {
        if (error.Code == ApiErrorCodes.IdempotencyKeyReused)
            return Problem(http, ErrorType.Validation, ApiErrorCodes.IdempotencyKeyReused, 422);
        var details = ProblemDetailsMapper.FromError(error, http.RequestServices.GetRequiredService<ProblemDetailsFactory>(), http);
        var result = new ObjectResult(details) { StatusCode = details.Status };
        result.ContentTypes.Add("application/problem+json");
        return result;
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

    private static async Task<string> DurableDigestAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Convert.ToHexString(await SHA256.HashDataAsync(request.Body, ct));
        var form = await request.ReadFormAsync(ct);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        static void Append(IncrementalHash hash, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
        }
        Append(hash, "form-v1");
        foreach (var field in form.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            Append(hash, "field"); Append(hash, field.Key);
            Append(hash, ArquitecturaBaseMultitenant.Application.Common.Formatting.VersionWireFormatter.Format(field.Value.Count));
            foreach (var value in field.Value) Append(hash, value ?? string.Empty);
        }
        foreach (var file in form.Files.OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.FileName, StringComparer.Ordinal))
        {
            Append(hash, "file"); Append(hash, file.Name); Append(hash, file.FileName); Append(hash, file.ContentType);
            Append(hash, ArquitecturaBaseMultitenant.Application.Common.Formatting.VersionWireFormatter.Format(file.Length));
            await using var content = file.OpenReadStream();
            hash.AppendData(await SHA256.HashDataAsync(content, ct));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
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
