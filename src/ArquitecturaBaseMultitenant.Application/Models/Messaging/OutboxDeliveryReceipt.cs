using ArquitecturaBaseMultitenant.Domain.Common;
namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

/// <summary>Resultado técnico seguro: aceptación no significa entrega. No admite payload ni datos del destinatario.</summary>
public sealed class OutboxDeliveryReceipt
{
    private OutboxDeliveryReceipt(bool accepted, bool retryable, string? providerMessageId, string? safeCode,
        string? providerScope, string? category)
    {
        IsAccepted = accepted; IsRetryable = retryable; ProviderMessageId = providerMessageId;
        SafeCode = safeCode; ProviderScope = providerScope; Category = category;
    }
    public bool IsAccepted { get; }
    public bool IsRetryable { get; }
    public string? ProviderMessageId { get; }
    public string? SafeCode { get; }
    public string? ProviderScope { get; }
    public string? Category { get; }
    public static OutboxDeliveryReceipt Accepted(string? providerMessageId = null,
        string? providerScope = null, string? category = null) =>
        new(true, false, providerMessageId, null, providerScope, category);
    public static OutboxDeliveryReceipt Rejected(string safeCode, bool retryable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeCode);
        if (safeCode.Length > TextLimits.ShortName || safeCode.Any(value => !char.IsAsciiLetterOrDigit(value) && value != '.'))
            throw new ArgumentException("A static technical error code is required.", nameof(safeCode));
        return new(false, retryable, null, safeCode, null, null);
    }
    public override string ToString() => $"OutboxDeliveryReceipt ({(IsAccepted ? "Accepted" : "Rejected")})";
}
