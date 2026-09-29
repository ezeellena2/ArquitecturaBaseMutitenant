using System.Collections.Concurrent;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Idempotency;

/// <summary>El único efecto observable de este POST es el contador de ejecución del probe.</summary>
[ApiController]
[Route("test/idempotency")]
public sealed class IdempotencyTestController : ControllerBase
{
    [HttpPost("create")]
    [HttpPost("alternate")]
    [Idempotent]
    public async Task<IActionResult> Create(
        [FromBody] IdempotencyTestRequest request,
        CancellationToken cancellationToken)
    {
        var probe = IdempotencyProbe.Get(request.ProbeId);
        var execution = probe.NextExecution();
        probe.Started.TrySetResult();
        await probe.Release.Task.WaitAsync(cancellationToken);

        if (probe.ThrowFirst && execution == 1)
        {
            throw new InvalidOperationException("Test failure before the HTTP response.");
        }

        if (probe.FailFirst && execution == 1)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { execution });
        }

        if (request.BadRequest)
        {
            return BadRequest(new { execution, request.Name });
        }

        return StatusCode(StatusCodes.Status201Created, new { execution, request.Name });
    }
}

public sealed record IdempotencyTestRequest(Guid ProbeId, string Name, bool BadRequest = false);

public sealed class IdempotencyProbe
{
    private static readonly ConcurrentDictionary<Guid, IdempotencyProbe> Probes = new();

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _executionCount;

    public int ExecutionCount => Volatile.Read(ref _executionCount);

    public bool FailFirst { get; private init; }

    public bool ThrowFirst { get; private init; }

    public Guid Id { get; } = Guid.NewGuid();

    public static IdempotencyProbe Create(bool block = false, bool failFirst = false, bool throwFirst = false)
    {
        var probe = new IdempotencyProbe { FailFirst = failFirst, ThrowFirst = throwFirst };
        Probes[probe.Id] = probe;
        if (!block)
        {
            probe.Release.TrySetResult();
        }

        return probe;
    }

    public static IdempotencyProbe Get(Guid id) => Probes[id];

    public int NextExecution() => Interlocked.Increment(ref _executionCount);
}
