using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

/// <summary>Transporta un correo renderizado entre la plantilla y el canal de entrega; su representación textual oculta datos sensibles.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody)
{
    /// <summary>
    /// El remitente de «Nombre y correos» (273), que el canal lee al mandar: no viaja en la cola, así un cambio alcanza a lo
    /// que espera. Null toma el de la configuración de arranque.
    /// </summary>
    [JsonIgnore]
    public string? FromName { get; init; }

    public override string ToString() => "EmailMessage [redacted]";
}
