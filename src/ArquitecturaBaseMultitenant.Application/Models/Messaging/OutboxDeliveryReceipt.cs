using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Messaging;
namespace ArquitecturaBaseMultitenant.Application.Models.Messaging;

/// <summary>
/// Resultado técnico seguro de un intento, como lo clasifica el transporte (ADR 0066): aceptación no significa entrega, y
/// <see cref="Uncertain"/> dice que pudo haber salido. No admite payload ni datos del destinatario.
/// </summary>
public sealed class OutboxDeliveryReceipt
{
    private OutboxDeliveryReceipt(OutboxAttemptOutcome outcome, string? providerMessageId, string? safeCode,
        string? providerScope, string? category)
    {
        Outcome = outcome; ProviderMessageId = providerMessageId;
        SafeCode = safeCode; ProviderScope = providerScope; Category = category;
    }
    public OutboxAttemptOutcome Outcome { get; }
    public bool IsAccepted => Outcome == OutboxAttemptOutcome.Accepted;
    public bool IsRetryable => Outcome == OutboxAttemptOutcome.Retry;
    public bool IsUncertain => Outcome == OutboxAttemptOutcome.Uncertain;
    public string? ProviderMessageId { get; }
    public string? SafeCode { get; }
    public string? ProviderScope { get; }
    public string? Category { get; }
    public static OutboxDeliveryReceipt Accepted(string? providerMessageId = null,
        string? providerScope = null, string? category = null) =>
        new(OutboxAttemptOutcome.Accepted, providerMessageId, null, providerScope, category);

    /// <summary>No salió: con <paramref name="retryable"/> vuelve a pendiente con su espera; sin él, falla.</summary>
    public static OutboxDeliveryReceipt Rejected(string safeCode, bool retryable) =>
        new(retryable ? OutboxAttemptOutcome.Retry : OutboxAttemptOutcome.Rejected, null, RequireCode(safeCode), null, null);

    /// <summary>No se sabe si salió: queda «Sin confirmar» y no se repite.</summary>
    public static OutboxDeliveryReceipt Uncertain(string safeCode) =>
        new(OutboxAttemptOutcome.Uncertain, null, RequireCode(safeCode), null, null);

    /// <summary>
    /// El canal lo posterga sin llamar al transporte (una difusión de una organización con el módulo apagado): vuelve a
    /// pendiente con su espera y no gasta el intento.
    /// </summary>
    public static OutboxDeliveryReceipt Deferred(string safeCode) =>
        new(OutboxAttemptOutcome.Deferred, null, RequireCode(safeCode), null, null);

    private static string RequireCode(string safeCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeCode);
        if (safeCode.Length > TextLimits.ShortName || safeCode.Any(value => !char.IsAsciiLetterOrDigit(value) && value != '.'))
            throw new ArgumentException("A static technical error code is required.", nameof(safeCode));
        return safeCode;
    }

    public override string ToString() => $"OutboxDeliveryReceipt ({Outcome})";
}
