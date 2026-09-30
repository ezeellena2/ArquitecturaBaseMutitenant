namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

/// <summary>Transporta un correo renderizado entre la plantilla y el canal de entrega; su representación textual oculta datos sensibles.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody)
{
    public override string ToString() => "EmailMessage [redacted]";
}
