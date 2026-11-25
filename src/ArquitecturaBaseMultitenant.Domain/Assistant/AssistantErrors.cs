using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

public static class AssistantErrors
{
    public const string ConversationNotFoundCode = "Assistant.Conversation.NotFound";
    public static readonly Error ConversationNotFound = Error.NotFound(ConversationNotFoundCode, "Conversation not found.");
    public const string AttachmentNotFoundCode = "Assistant.Attachment.NotFound";
    public static readonly Error AttachmentNotFound = Error.NotFound(AttachmentNotFoundCode, "Attachment not found.");
    public const string SettingsNotConfiguredCode = "Assistant.Settings.NotConfigured";
    public static readonly Error SettingsNotConfigured = Error.Validation(SettingsNotConfiguredCode, "The assistant is not configured yet.");
    public const string SettingsKeyRejectedCode = "Assistant.Settings.KeyRejected";
    public static readonly Error SettingsKeyRejected = Field(SettingsKeyRejectedCode, "The key is invalid.", "apiKey", ErrorType.Validation);
    public const string SettingsOwnKeyNotAllowedCode = "Assistant.Settings.OwnKeyNotAllowed";
    public static readonly Error SettingsOwnKeyNotAllowed = Field(SettingsOwnKeyNotAllowedCode, "The platform has not enabled your own key.", "mode", ErrorType.Forbidden);
    public const string SettingsAlertRecipientRequiredCode = "Assistant.Settings.AlertRecipientRequired";
    public static readonly Error SettingsAlertRecipientRequired = Field(SettingsAlertRecipientRequiredCode, "To use your organization's key, an owner must have a verified email.", "mode", ErrorType.Validation);
    public const string ModelNotAllowedCode = "Assistant.Model.NotAllowed";
    public static readonly Error ModelNotAllowed = Field(ModelNotAllowedCode, "Choose an enabled model.", "modelId", ErrorType.Validation);
    public const string ModelAlreadyExistsCode = "Assistant.Model.AlreadyExists";
    public static readonly Error ModelAlreadyExists = Field(ModelAlreadyExistsCode, "That model is already in the catalog.", "providerModelId", ErrorType.Conflict);
    public const string ModelNotFoundCode = "Assistant.Model.NotFound";
    public static readonly Error ModelNotFound = Error.NotFound(ModelNotFoundCode, "Model not found.");
    public const string SettingsPlatformLimitsReadOnlyCode = "Assistant.Settings.PlatformLimitsReadOnly";
    public static readonly Error SettingsPlatformLimitsReadOnly = Field(SettingsPlatformLimitsReadOnlyCode, "These limits are set by the platform.", "dailyMessageLimit", ErrorType.Validation);
    public const string NoticeAcceptanceRequiredCode = "Assistant.Notice.AcceptanceRequired";
    public static readonly Error NoticeAcceptanceRequired = Field(NoticeAcceptanceRequiredCode, "Read the notice before your first query.", "noticeVersion", ErrorType.Validation);
    public const string LimitDailyReachedCode = "Assistant.Limit.DailyReached";
    public static Error LimitDailyReached(int retryAfter, int dailyLimit, DateTime dailyResetAtUtc) => Error.TooManyRequests(LimitDailyReachedCode, "You reached today's limit of {limit} queries. They reset at {time}.", new Dictionary<string, object?> { ["retryAfter"] = retryAfter, ["dailyLimit"] = dailyLimit, ["dailyResetAtUtc"] = dailyResetAtUtc });
    public const string LimitMonthlyReachedCode = "Assistant.Limit.MonthlyReached";
    public static Error LimitMonthlyReached(int retryAfter) => Error.TooManyRequests(LimitMonthlyReachedCode, "You reached the monthly limit.", new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
    public const string TurnInProgressCode = "Assistant.Turn.InProgress";
    public static readonly Error TurnInProgress = Error.Conflict(TurnInProgressCode, "Wait for or stop the current response.");
    public const string TurnNotFoundCode = "Assistant.Turn.NotFound";
    public static readonly Error TurnNotFound = Error.NotFound(TurnNotFoundCode, "Response not found.");
    public const string AttachmentUnsupportedTypeCode = "Assistant.Attachment.UnsupportedType";
    public static readonly Error AttachmentUnsupportedType = Field(AttachmentUnsupportedTypeCode, "Attach an image or a PDF.", "file", ErrorType.Validation);
    public const string AttachmentTooLargeCode = "Assistant.Attachment.TooLarge";
    public static readonly Error AttachmentTooLarge = Field(AttachmentTooLargeCode, "The file is larger than 10 MB.", "file", ErrorType.PayloadTooLarge);
    public const string AttachmentTooManyCode = "Assistant.Attachment.TooMany";
    public static readonly Error AttachmentTooMany = Field(AttachmentTooManyCode, "You can attach up to 5 files per message.", "attachmentIds", ErrorType.Validation);
    public const string AttachmentAlreadyUsedCode = "Assistant.Attachment.AlreadyUsed";
    public static readonly Error AttachmentAlreadyUsed = Field(AttachmentAlreadyUsedCode, "That attachment already belongs to a message.", "attachmentIds", ErrorType.Conflict);
    public const string QuickQueryNotAvailableCode = "Assistant.QuickQuery.NotAvailable";
    public static readonly Error QuickQueryNotAvailable = Field(QuickQueryNotAvailableCode, "That query is unavailable.", "key", ErrorType.Validation);
    public const string ProviderUnavailableCode = "Assistant.Provider.Unavailable";
    public static readonly Error ProviderUnavailable = Error.ServiceUnavailable(ProviderUnavailableCode, "The assistant is unavailable now.");
    public const string ProviderContextTooLargeCode = "Assistant.Provider.ContextTooLarge";
    public static readonly Error ProviderContextTooLarge = Field(ProviderContextTooLargeCode, "The conversation or attachments are too large. Start a new one.", "text", ErrorType.Validation);
    public static Error ProviderUnavailableWithRetry(int retryAfter) => Error.ServiceUnavailable(ProviderUnavailableCode, ProviderUnavailable.Description,
        new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
    // Subidas (ADR 0064 §2 y §4): sin cupo, el 429 de siempre; sin lugar, 429 con espera; el cuerpo tiene 120 s.
    public const string AttachmentUploadBusyCode = "Assistant.Attachment.UploadBusy";
    public static Error AttachmentUploadBusy(int retryAfter) => Error.TooManyRequests(AttachmentUploadBusyCode,
        "Another upload of yours is in progress.", new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
    public const string AttachmentUploadQueueFullCode = "Assistant.Attachment.UploadQueueFull";
    public static Error AttachmentUploadQueueFull(int retryAfter) => Error.TooManyRequests(AttachmentUploadQueueFullCode,
        "Too many uploads are waiting.", new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
    public const string AttachmentUploadTimedOutCode = "Assistant.Attachment.UploadTimedOut";
    public static readonly Error AttachmentUploadTimedOut = Error.Validation(AttachmentUploadTimedOutCode, "The upload took too long.");
    /// <summary>ADR 0064 §6: una conversación que esta versión no puede leer se ve no disponible y se puede borrar.</summary>
    public const string ConversationUnavailableCode = "Assistant.Conversation.Unavailable";
    public static readonly Error ConversationUnavailable = Error.Conflict(ConversationUnavailableCode, "The conversation cannot be read.");
    // Fallas del proveedor (ADR 0064 §7), en el evento error, el estado del turno y providerIssue; no son respuestas HTTP.
    public const string ProviderCredentialRejectedCode = "Assistant.Provider.CredentialRejected";
    public const string ProviderLimitedCode = "Assistant.Provider.Limited";
    public const string ProviderModelRejectedCode = "Assistant.Provider.ModelRejected";
    public const string ProviderRejectedCode = "Assistant.Provider.Rejected";
    public const string ProviderUncertainCode = "Assistant.Provider.Uncertain";
    public const string ProviderKeyUnreadableCode = "Assistant.Provider.KeyUnreadable";
    public const string ProviderUnsupportedProviderCode = "Assistant.Provider.UnsupportedProvider";
    public const string ProviderPlatformUnavailableCode = "Assistant.Provider.PlatformUnavailable";
    /// <summary>La clave guardada no se puede descifrar acá (0064 §7): el turno falla sin llamar al proveedor ni gastar.</summary>
    public static readonly Error ProviderKeyUnreadable = Error.ServiceUnavailable(ProviderKeyUnreadableCode, "We couldn't read the saved key: replace it.");
    /// <summary>El proveedor guardado no lo conoce esta versión (0064 §6): la conexión queda pausada.</summary>
    public static readonly Error ProviderUnsupportedProvider = Error.ServiceUnavailable(ProviderUnsupportedProviderCode,
        "The connection is paused: the saved provider isn't available in this version. Save the settings to discard it.");
    /// <summary>
    /// Lo que dijo el proveedor en un turno y queda anotado para quien paga, aparte de su configuración, hasta que vuelve a
    /// responder o se guarda la configuración (0064 §7, B13). La clave ilegible y el proveedor desconocido no se guardan: se miran al leer.
    /// </summary>
    public static IReadOnlyList<string> ProviderRecordedIssueCodes { get; } =
        [ProviderCredentialRejectedCode, ProviderLimitedCode, ProviderModelRejectedCode, ProviderUnavailableCode];
    /// <summary>Los códigos que toma <c>providerIssue</c> en la configuración, según quién paga.</summary>
    public static IReadOnlyList<string> ProviderIssueCodes { get; } =
        [ProviderCredentialRejectedCode, ProviderLimitedCode, ProviderModelRejectedCode, ProviderUnavailableCode, ProviderKeyUnreadableCode, ProviderPlatformUnavailableCode];
    /// <summary>Los códigos de falla del proveedor en el evento error y el estado del turno.</summary>
    public static IReadOnlyList<string> ProviderFailureCodes { get; } =
        [ProviderCredentialRejectedCode, ProviderLimitedCode, ProviderModelRejectedCode, ProviderRejectedCode, ProviderUncertainCode,
            ProviderUnavailableCode, ProviderKeyUnreadableCode, ProviderUnsupportedProviderCode, ProviderPlatformUnavailableCode];
    public const string ExportFailedCode = "Assistant.Export.Failed";
    public static readonly Error ExportFailed = Error.Failure(ExportFailedCode, "We could not export the conversation.");
    private static ValidationError Field(string code, string description, string field, ErrorType type) =>
        new(code, description, new Dictionary<string, string[]> { [field] = [code] }, type);
}
