namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class IdentityAccessTests
{
    // E11: el vínculo de una conversación con una cuenta necesita el espacio personal de esa cuenta (D2).
    static partial void ContributeWhatsAppGlobalAccessReaders(List<string> readers) =>
        readers.Add("ArquitecturaBaseMultitenant.Application/Modules/WhatsApp/Services/WhatsAppConversationLinkWriter.cs");
}
