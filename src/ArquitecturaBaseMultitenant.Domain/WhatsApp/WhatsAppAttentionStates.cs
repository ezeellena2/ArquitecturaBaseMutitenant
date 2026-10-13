namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

public enum WhatsAppNumberStatus { Connected, PaymentMissing, Disconnected }

public enum WhatsAppInbox { Customers, Internal }

public enum WhatsAppChatMessageKind { Text, Template, Note, Event, Media }

public enum WhatsAppMediaKind { Image, Audio, Video, Document, Sticker, Location, Other }

public enum WhatsAppChatMessageStatus { Received, Pending, Sent, Delivered, Read, Failed }

public enum WhatsAppConsent { Accepted, Declined, NoAnswer, Unsubscribed }

public enum WhatsAppConsentSource { Bot, Import, Manual, Unsubscribe }

public enum WhatsAppTemplateCategory { Utility, Marketing, Authentication }

public enum WhatsAppTemplateStatus { Pending, Approved, Rejected, Paused }

public enum WhatsAppBroadcastStatus { Scheduled, Sending, Sent, Cancelled }

public enum WhatsAppRecipientStatus { Pending, Sent, Delivered, Read, Replied, Unsubscribed, Failed }

public enum WhatsAppBotAction { Team, Reply }

public enum WhatsAppAudienceKind { AllAccepting, Tag }

public enum WhatsAppPhoneImportOutcome { AlreadyMember, Invite, Invalid }
