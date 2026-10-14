namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>La cola de entrada procesa lo de una organización en su scope, como esa organización: el vínculo es un dato compartido.</summary>
public sealed partial class TenantScopeUsageTests
{
    static partial void ContributeAccessInitializerCallers(HashSet<string> declared) =>
        declared.Add("ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Inbound.WhatsAppInboundProcessor");
}
