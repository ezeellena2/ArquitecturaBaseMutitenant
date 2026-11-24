namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// Qué cuenta un contador de <c>identity.MessageQuotaBuckets</c> (ADR 0063 y decisión 256). Cada uno suma por hora y su
/// clave es un HMAC: de la dirección, de la cuenta o de la organización; el global no tiene clave propia.
/// </summary>
public enum MessageQuotaScope
{
    /// <summary>Todo correo con un código o una invitación, de la plataforma entera.</summary>
    Global,

    /// <summary>Los códigos por correo pedidos desde una dirección (una IPv6, por su /64), hayan salido o no.</summary>
    CodeIp,

    /// <summary>Los códigos por correo que una cuenta pide desde Mi cuenta.</summary>
    CodeAccount,

    /// <summary>Las invitaciones por correo de una persona, sume las organizaciones que sume, también un operador.</summary>
    Inviter,

    /// <summary>Las invitaciones por correo de una organización.</summary>
    Organization,

    /// <summary>Los códigos por WhatsApp que una cuenta pide desde Mi cuenta.</summary>
    WhatsAppCodeAccount,
}
