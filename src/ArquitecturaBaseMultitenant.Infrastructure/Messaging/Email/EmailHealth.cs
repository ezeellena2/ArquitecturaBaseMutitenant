namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// N5: lo que la copia sabe del servidor de correo por sus envíos. Un 535 marca las credenciales rechazadas hasta el próximo
/// envío que sale. Vive en memoria, por copia: un reinicio arranca sin problema anotado.
/// </summary>
internal sealed class EmailHealth
{
    private volatile bool _credentialsRejected;

    public bool CredentialsRejected => _credentialsRejected;

    public void ReportSent() => _credentialsRejected = false;

    public void ReportCredentialsRejected() => _credentialsRejected = true;
}
