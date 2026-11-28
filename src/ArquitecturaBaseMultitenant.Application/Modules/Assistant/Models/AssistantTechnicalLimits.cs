namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public static class AssistantTechnicalLimits
{
    public static IReadOnlyList<string> AttachmentMediaTypes { get; } = Array.AsReadOnly(Domain.Common.FileMediaTypes.Images.Append(Domain.Common.FileMediaTypes.Pdf).ToArray());
    public const int MaxAttachmentBytes = 10 * 1024 * 1024;
    public const int MaxAttachments = 5;
    /// <summary>Los campos de una propuesta de una acción, como mucho (ADR 0068 §12).</summary>
    public const int MaxProposalFields = 40;
    public const long MaxProviderPayloadBytes = 32L * 1024 * 1024;
    public const int QuickRows = 20;
    public const int SmallQuickPageSize = 10;
    public const int RemainingMessagesNotice = 10;
    public const int BudgetNearPercent = Domain.Assistant.AssistantBudgetAlert.NearThreshold;
    public const int BudgetReachedPercent = Domain.Assistant.AssistantBudgetAlert.ReachedThreshold;
    public const decimal BudgetNearFraction = BudgetNearPercent / 100m;
    public const int MaxInputTokens = 51200;
    /// <summary>Cota conservadora de tokens por página de un PDF (coincide con el tope visual de una imagen).</summary>
    public const long PdfTokensPerPage = 8192;
    /// <summary>Máximo de páginas de un PDF derivado de <see cref="MaxInputTokens"/> antes de descomprimir streams (KAN-69).</summary>
    public const int MaxPdfPages = (int)(MaxInputTokens / PdfTokensPerPage);
    public const int MaxOutputTokens = 2048;
    public const int TitleMaxOutputTokens = 64;
    // Includes the title call. Admission/reservations and the provider loop are supplied by T05/T06.
    public const int MaxCallsPerTurn = 5;
    // ADR 0064 §12: the pending turns a copy takes per pass, as many as its four lanes start before a turn expires.
    public const int TurnsPerCopy = 4;
}
