namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

public enum WhatsAppNumberStatus { Connected, PaymentMissing, Disconnected }

/// <summary>De dónde salió el número: el arranque desde la configuración (solo Plataforma) o el alta embebida.</summary>
public enum WhatsAppChannelSource { Configuration, Signup }

public enum WhatsAppInbox { Customers, Internal }

public enum WhatsAppChatMessageKind { Text, Template, Note, Event, Media }

public enum WhatsAppMediaKind { Image, Audio, Video, Document, Sticker, Location, Other }

/// <summary><c>Uncertain</c> («Sin confirmar», ADR 0066) se deriva al leer, desde la cola: no se guarda.</summary>
public enum WhatsAppChatMessageStatus { Received, Pending, Sent, Delivered, Read, Failed, Uncertain }

public enum WhatsAppConsent { Accepted, Declined, NoAnswer, Unsubscribed }

public enum WhatsAppConsentSource { Bot, Import, Manual, Unsubscribe }

public enum WhatsAppTemplateCategory { Utility, Marketing, Authentication }

public enum WhatsAppTemplateStatus { Pending, Approved, Rejected, Paused }

public enum WhatsAppBroadcastStatus { Scheduled, Sending, Sent, Cancelled }

/// <summary><c>Uncertain</c> («Sin confirmar», decisión 255) se deriva al leer, desde la cola: no cuenta como enviado ni como «No llegó».</summary>
public enum WhatsAppRecipientStatus { Pending, Sent, Delivered, Read, Replied, Unsubscribed, Failed, Uncertain }

public enum WhatsAppBotAction { Team, Reply }

public enum WhatsAppAudienceKind { AllAccepting, Tag }

public enum WhatsAppPhoneImportOutcome { AlreadyMember, Invite, Invalid }
