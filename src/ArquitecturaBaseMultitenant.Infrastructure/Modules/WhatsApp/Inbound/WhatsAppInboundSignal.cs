using System.Threading.Channels;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound;
internal sealed class WhatsAppInboundSignal : IWhatsAppInboundSignal
{
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    public void Notify() => _wake.Writer.TryWrite(true);
    internal async Task WaitAsync(TimeSpan interval, TimeProvider clock, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(interval, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try { await _wake.Reader.ReadAsync(linked.Token); } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
    }
}
