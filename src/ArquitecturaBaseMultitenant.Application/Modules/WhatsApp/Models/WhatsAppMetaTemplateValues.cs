using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;

/// <summary>
/// Cómo se leen el estado y la categoría que manda Meta, en la API y en los webhooks <c>message_template_status_update</c> y
/// <c>template_category_update</c>. Lo que no cambia si se puede mandar (FLAGGED, LOCKED) no se aplica.
/// </summary>
public static class WhatsAppMetaTemplateValues
{
    public static WhatsAppTemplateStatus? Status(string? value) => value?.ToUpperInvariant() switch
    {
        "APPROVED" or "REINSTATED" or "UNPAUSED" => WhatsAppTemplateStatus.Approved,
        "PENDING" or "IN_APPEAL" => WhatsAppTemplateStatus.Pending,
        "REJECTED" => WhatsAppTemplateStatus.Rejected,
        "PAUSED" or "DISABLED" => WhatsAppTemplateStatus.Paused,
        _ => null,
    };

    /// <summary>Meta la borró o la archivó: deja de estar en la cuenta.</summary>
    public static bool IsDeleted(string? value) => value?.ToUpperInvariant() is "DELETED" or "PENDING_DELETION" or "ARCHIVED";

    public static WhatsAppTemplateCategory? Category(string? value) => value?.ToUpperInvariant() switch
    {
        "MARKETING" => WhatsAppTemplateCategory.Marketing,
        "UTILITY" => WhatsAppTemplateCategory.Utility,
        "AUTHENTICATION" => WhatsAppTemplateCategory.Authentication,
        _ => null,
    };

    /// <summary>El motivo del rechazo como código corto de Meta (INVALID_FORMAT); NONE y lo que no es un código, null.</summary>
    public static string? Reason(string? value) =>
        value is { Length: > 0 and <= 40 } && value != "NONE" && value.All(character => char.IsAsciiLetterUpper(character) || character == '_') ? value : null;
}
