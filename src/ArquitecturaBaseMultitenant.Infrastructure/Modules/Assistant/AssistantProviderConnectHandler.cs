namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// ADR 0064 §7 (B13): los 10 s para conectarse con el proveedor vencen sin que salga nada. El handler de sockets los avisa como una
/// cancelación con un <see cref="TimeoutException"/> adentro, la misma forma que la espera del SDK después de mandar; acá abajo se
/// distinguen porque la espera del SDK cancela el pedido y la de la conexión no. Se avisan como una conexión fallida, que
/// <see cref="AssistantProviderFailureClassifier"/> lee como no disponible.
/// </summary>
internal sealed class AssistantProviderConnectHandler : DelegatingHandler
{
    internal static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try { return await base.SendAsync(request, cancellationToken); }
        catch (OperationCanceledException error) when (error.InnerException is TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "The provider connection timed out.");
        }
    }
}
