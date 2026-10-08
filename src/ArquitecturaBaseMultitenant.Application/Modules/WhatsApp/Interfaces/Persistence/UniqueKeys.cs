namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Claves de deduplicación durable del módulo, compartidas por EF y replay.</summary>
public static class UniqueKeys
{
    public const string MessageEvent = "IX_WhatsAppMessages_PhoneNumberId_Wamid_EventType";
    public const string ContactNumber = "IX_WhatsAppContacts_WaId";
}
