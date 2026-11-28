using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

public static class WhatsAppErrors
{
    public const string CountryNotAllowedCode = "WhatsApp.Phone.CountryNotAllowed";
    public const string WebhookTooManyRequestsCode = "WhatsApp.Webhook.TooManyRequests";
    public static readonly Error CountryNotAllowed = Error.Validation(CountryNotAllowedCode, "WhatsApp is not enabled for that country.");
    public static Error WebhookTooManyRequests(int retryAfter) => Error.TooManyRequests(WebhookTooManyRequestsCode,
        "Too many webhook requests.", new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
    public static readonly Error InvalidSignature = Error.Unauthorized("WhatsApp.Webhook.InvalidSignature", "Invalid webhook signature.");
    public static readonly Error InvalidPayload = Error.Validation("WhatsApp.Webhook.InvalidPayload", "Invalid webhook payload.");
    public static readonly Error LeaseExpired = Error.Conflict("WhatsApp.Inbound.LeaseExpired", "Inbound message lease expired.");

    public const string NumberNotFoundCode = "WhatsApp.Number.NotFound";
    public const string NumberAlreadyConnectedCode = "WhatsApp.Number.AlreadyConnected";
    public const string NumberSignupExpiredCode = "WhatsApp.Number.SignupExpired";
    public const string NumberMetaRejectedCode = "WhatsApp.Number.MetaRejected";
    public const string ConversationNotFoundCode = "WhatsApp.Conversation.NotFound";
    public const string ConversationTakenByOtherCode = "WhatsApp.Conversation.TakenByOther";
    public const string ConversationWindowClosedCode = "WhatsApp.Conversation.WindowClosed";
    public const string ConversationPaymentRequiredCode = "WhatsApp.Conversation.PaymentRequired";
    public const string ConversationReadOnlyCode = "WhatsApp.Conversation.ReadOnly";
    public const string BotTooManyOptionsCode = "WhatsApp.Bot.TooManyOptions";
    public const string TeamNotFoundCode = "WhatsApp.Team.NotFound";
    public const string TeamNameTakenCode = "WhatsApp.Team.NameTaken";
    public const string ContactNotFoundCode = "WhatsApp.Contact.NotFound";
    public const string TemplateNotFoundCode = "WhatsApp.Template.NotFound";
    public const string TemplateNameTakenCode = "WhatsApp.Template.NameTaken";
    public const string TemplateNotApprovedCode = "WhatsApp.Template.NotApproved";
    public const string TemplateNumberRequiredCode = "WhatsApp.Template.NumberRequired";
    public const string BroadcastNotFoundCode = "WhatsApp.Broadcast.NotFound";
    public const string BroadcastNoRecipientsCode = "WhatsApp.Broadcast.NoRecipients";
    public const string BroadcastNotCancellableCode = "WhatsApp.Broadcast.NotCancellable";
    public const string ImportInvalidFileCode = "WhatsApp.Import.InvalidFile";
    public const string ImportTooManyRowsCode = "WhatsApp.Import.TooManyRows";
    public const string LinkNotFoundCode = "WhatsApp.Link.NotFound";
    public const string PhoneImportConsentRequiredCode = "WhatsApp.PhoneImport.ConsentRequired";
    public const string PhoneImportDailyLimitCode = "WhatsApp.PhoneImport.DailyLimit";
    public const string PhoneImportTooManyInvitationsCode = "WhatsApp.PhoneImport.TooManyInvitations";
    public const string RetryAfterKey = "retryAfter";
    public const string DailyLimitKey = "dailyLimit";

    public static readonly Error NumberNotFound = Error.NotFound(NumberNotFoundCode, "Number not found.");
    public static readonly Error NumberAlreadyConnected = Error.Conflict(NumberAlreadyConnectedCode, "That number is already connected.");
    public static readonly Error NumberSignupExpired = Field(NumberSignupExpiredCode, "The Meta signup expired. Connect the number again.", "code", ErrorType.Validation);
    public static Error NumberMetaRejected(string metaCode) => Error.ServiceUnavailable(NumberMetaRejectedCode,
        "Meta rejected the connection.", new Dictionary<string, object?> { ["metaCode"] = metaCode });
    public static readonly Error ConversationNotFound = Error.NotFound(ConversationNotFoundCode, "Conversation not found.");

    /// <summary>
    /// 272: el plazo de los chats de una organización va de 1 al máximo que fija la plataforma al guardar. El texto del campo
    /// dice ese máximo, que el validador no conoce.
    /// </summary>
    public const string ChatRetentionOutOfRangeCode = "WhatsApp.Settings.ChatRetentionOutOfRange";
    public const string MaxChatRetentionDaysKey = "maxChatRetentionDays";
    public static Error ChatRetentionOutOfRange(int maxChatRetentionDays) => Error.Validation(ChatRetentionOutOfRangeCode,
        "The retention is above the platform maximum.", new Dictionary<string, object?> { [MaxChatRetentionDaysKey] = maxChatRetentionDays });

    /// <summary>Al menos un país habilitado debe tener tarifas de Meta cargadas para estimar costos de plantillas y difusiones.</summary>
    public const string CountriesWithoutRatesCode = "WhatsApp.Settings.CountriesWithoutRates";
    public static readonly Error CountriesWithoutRates = Field(CountriesWithoutRatesCode,
        "Include at least one country with Meta rates.", "allowedCountries", ErrorType.Validation);

    /// <summary>ADR 0066: solo se marca como revisado un mensaje que quedó «Sin confirmar».</summary>
    public const string MessageNotUncertainCode = "WhatsApp.Message.NotUncertain";
    public static readonly Error MessageNotUncertain = Error.Conflict(MessageNotUncertainCode, "The message is not unconfirmed.");
    public static Error ConversationTakenByOther(string takenBy) => Error.Conflict(ConversationTakenByOtherCode,
        "Someone else took this conversation.", new Dictionary<string, object?> { ["takenBy"] = takenBy });
    public static readonly Error ConversationWindowClosed = Error.Conflict(ConversationWindowClosedCode, "More than 24 hours passed: send a template.");
    public static readonly Error ConversationPaymentRequired = Error.Conflict(ConversationPaymentRequiredCode, "The number needs a payment method in Meta.");
    public static readonly Error ConversationReadOnly = Error.Conflict(ConversationReadOnlyCode, "The number of this conversation is disconnected.");
    public static readonly Error BotTooManyOptions = Field(BotTooManyOptionsCode, "The menu allows up to 10 options.", "menu", ErrorType.Validation);
    public static readonly Error TeamNotFound = Error.NotFound(TeamNotFoundCode, "Team not found.");
    public static readonly Error TeamNameTaken = Field(TeamNameTakenCode, "There is already a team with that name.", "name", ErrorType.Conflict);
    public static readonly Error ContactNotFound = Error.NotFound(ContactNotFoundCode, "Contact not found.");
    public static readonly Error TemplateNotFound = Error.NotFound(TemplateNotFoundCode, "Template not found.");
    public static readonly Error TemplateNameTaken = Field(TemplateNameTakenCode, "There is already a template with that name.", "name", ErrorType.Conflict);
    /// <summary>Sin un número conectado no hay cuenta de Meta donde mandar la plantilla: es un estado, no un rechazo de Meta (CC27).</summary>
    public static readonly Error TemplateNumberRequired = Error.Conflict(TemplateNumberRequiredCode, "Connect a number to create templates.");
    public static readonly Error TemplateNotApproved = Field(TemplateNotApprovedCode, "Meta has not approved that template.", "templateId", ErrorType.Conflict);
    public static readonly Error BroadcastNotFound = Error.NotFound(BroadcastNotFoundCode, "Broadcast not found.");
    public static readonly Error BroadcastNoRecipients = Field(BroadcastNoRecipientsCode, "No contact in that audience accepts promotions.", "audience", ErrorType.Validation);
    public static readonly Error BroadcastNotCancellable = Error.Conflict(BroadcastNotCancellableCode, "The broadcast can no longer be cancelled.");
    public static readonly Error ImportInvalidFile = Field(ImportInvalidFileCode, "Upload an .xlsx or .csv file like the sample.", "file", ErrorType.Validation);
    public static readonly Error ImportTooManyRows = Field(ImportTooManyRowsCode, "The file has more than 5,000 rows.", "file", ErrorType.Validation);
    public static readonly Error LinkNotFound = Error.NotFound(LinkNotFoundCode, "Link not found.");
    /// <summary>«Cargar teléfonos» invita solo si quien carga confirma que la persona aceptó los mensajes (decisión 179).</summary>
    public static readonly Error PhoneImportConsentRequired = Field(PhoneImportConsentRequiredCode,
        "Confirm that the person agreed to receive WhatsApp messages.", "consentConfirmed", ErrorType.Validation);
    /// <summary>
    /// Tope diario de invitaciones por WhatsApp de «Cargar teléfonos» (decisión 1 de los arreglos de la revisión), que cuenta
    /// también sus reenvíos desde Usuarios o desde una empresa (B9).
    /// </summary>
    public static Error PhoneImportDailyLimit(int retryAfterSeconds) => Error.TooManyRequests(PhoneImportDailyLimitCode,
        "No more WhatsApp invitations can be sent for now, and nothing was saved.",
        new Dictionary<string, object?> { [RetryAfterKey] = Math.Max(1, retryAfterSeconds) });
    /// <summary>
    /// Una carga con más invitaciones que el tope diario (el menor entre el de la organización y el global) no entra ni con la
    /// ventana vacía: sin espera, con el máximo en <c>dailyLimit</c>, para partirla (B9).
    /// </summary>
    public static Error PhoneImportTooManyInvitations(int dailyLimit) => Error.Validation(PhoneImportTooManyInvitationsCode,
        "The upload has more invitations than can be sent by WhatsApp in a day, and nothing was saved.",
        new Dictionary<string, object?> { [DailyLimitKey] = dailyLimit });

    private static ValidationError Field(string code, string description, string field, ErrorType type) =>
        new(code, description, new Dictionary<string, string[]> { [field] = [code] }, type);
}
