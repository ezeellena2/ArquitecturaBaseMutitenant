namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>
/// 0064 §11: las claves del anillo de Data Protection que ve esta copia. <c>Readable</c>: las que puede usar; <c>Unreadable</c>: las
/// que están en el anillo y no puede abrir (otro certificado). Nunca lleva una clave ni un control.
/// </summary>
public sealed record AiKeyRingKeys(IReadOnlyList<Guid> Readable, IReadOnlyList<Guid> Unreadable);
