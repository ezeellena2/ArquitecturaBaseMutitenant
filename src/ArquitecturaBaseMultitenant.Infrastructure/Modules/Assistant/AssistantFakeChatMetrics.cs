namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Contadores sin datos privados del proveedor determinístico autorizado sólo en Testing/E2E.</summary>
public sealed class AssistantFakeChatMetrics
{
    private long _calls;
    private long _titleCalls;
    private long _toolCycles;
    public long Calls => Interlocked.Read(ref _calls);
    public long TitleCalls => Interlocked.Read(ref _titleCalls);
    public long ToolCycles => Interlocked.Read(ref _toolCycles);
    internal void Call() => Interlocked.Increment(ref _calls);
    internal void Title() => Interlocked.Increment(ref _titleCalls);
    internal void Tool() => Interlocked.Increment(ref _toolCycles);
}
