namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public static class AssistantTechnicalLimits
{
    public static IReadOnlyList<string> AttachmentMediaTypes { get; } = Array.AsReadOnly(Domain.Common.FileMediaTypes.Images.Append(Domain.Common.FileMediaTypes.Pdf).ToArray());
    public const int MaxAttachmentBytes = 10 * 1024 * 1024;
    public const int MaxAttachments = 5;
    public const long MaxProviderPayloadBytes = 32L * 1024 * 1024;
    public const int QuickRows = 20;
    public const int SmallQuickPageSize = 10;
    public const int RemainingMessagesNotice = 10;
    public const int BudgetNearPercent = Domain.Assistant.AssistantBudgetAlert.NearThreshold;
    public const int BudgetReachedPercent = Domain.Assistant.AssistantBudgetAlert.ReachedThreshold;
    public const decimal BudgetNearFraction = BudgetNearPercent / 100m;
    public const int MaxInputTokens = 51200;
    public const int MaxOutputTokens = 2048;
    public const int TitleMaxOutputTokens = 64;
    // Includes the title call. Admission/reservations and the provider loop are supplied by T05/T06.
    public const int MaxCallsPerTurn = 5;
}
