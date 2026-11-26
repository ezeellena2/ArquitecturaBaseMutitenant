namespace ArquitecturaBaseMultitenant.ArchitectureTests;

// La parte de WhatsApp de las acciones declaradas (ADR 0068 §4): conectar o desconectar un número con Meta es una
// conexión, y no se declara como acción. Sale con el módulo.
public sealed partial class AssistantActionDeclarationTests
{
    static partial void ContributeWhatsAppNotDeclarable(List<NotDeclarableRoute> routes) => routes.AddRange(
    [
        new("POST", "api/organization/whatsapp/numbers", "conexiones: un número con Meta"),
        new("DELETE", "api/organization/whatsapp/numbers", "conexiones: un número con Meta"),
        new("POST", "api/platform/whatsapp/number", "conexiones: el número de Plataforma con Meta"),
    ]);
}
