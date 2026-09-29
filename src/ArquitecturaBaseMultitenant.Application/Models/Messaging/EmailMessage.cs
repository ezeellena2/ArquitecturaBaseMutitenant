namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody)
{
    public override string ToString() => "EmailMessage [redacted]";
}
