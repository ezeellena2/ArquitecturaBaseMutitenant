using System.Diagnostics;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// 0064 §11 (B15): el turno guarda la traza de quien lo pidió, el worker abre una actividad con ese padre y un turno fallido queda
/// en error con su código. Sin Docker.
/// </summary>
public sealed class AssistantTurnTraceTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
    private const string Parent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [Fact]
    public void The_turn_activity_continues_the_trace_of_whoever_asked_and_a_failed_turn_is_an_error()
    {
        using var listener = Listen();
        var turnId = Guid.CreateVersion7();
        using (var activity = AssistantTracing.StartTurn(turnId, Parent))
        {
            Assert.NotNull(activity);
            Assert.Equal("0af7651916cd43dd8448eb211c80319c", activity.TraceId.ToHexString());
            Assert.Equal("b7ad6b7169203331", activity.ParentSpanId.ToHexString());
            Assert.Equal(turnId.ToString("D"), activity.GetTagItem("assistant.turn.id"));
            AssistantTracing.MarkOutcome(activity, "Failed", AssistantErrors.ProviderUncertainCode);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal(AssistantErrors.ProviderUncertainCode, activity.StatusDescription);
        }
        using var completed = AssistantTracing.StartTurn(turnId, Parent);
        AssistantTracing.MarkOutcome(completed, "Complete", null);
        Assert.Equal(ActivityStatusCode.Unset, completed!.Status);
    }

    [Fact]
    public void A_stored_trace_that_cannot_be_read_starts_its_own_trace()
    {
        using var listener = Listen();
        using var activity = AssistantTracing.StartTurn(Guid.CreateVersion7(), "no-es-un-traceparent");
        Assert.NotNull(activity);
        Assert.Equal(default, activity.ParentSpanId);
        Assert.NotEqual("0af7651916cd43dd8448eb211c80319c", activity.TraceId.ToHexString());
    }

    [Fact]
    public void The_turn_keeps_the_trace_of_the_request_once()
    {
        using var listener = Listen();
        using var request = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var trace = AssistantTracing.Current();
        Assert.Equal(request.Id, trace);
        var work = AssistantTurnWork.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Access.Consumer, Guid.CreateVersion7(),
            TenantKind.Personal, "es-AR", "America/Argentina/Buenos_Aires", null, Guid.CreateVersion7(), "model", "Low",
            AssistantKeyFunding.PlatformKey, Now, Now.AddMinutes(5));
        work.RecordTrace(trace!);
        work.RecordTrace(Parent);
        Assert.Equal(trace, work.TraceParent);
        Assert.Throws<ArgumentException>(() => work.RecordTrace("corto"));
    }

    private static ActivityListener Listen()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AssistantTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
