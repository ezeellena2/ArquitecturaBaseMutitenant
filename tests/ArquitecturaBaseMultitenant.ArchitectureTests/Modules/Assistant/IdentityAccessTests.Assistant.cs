namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class IdentityAccessTests
{
    static partial void ContributeGlobalAccessReaders(List<string> readers) =>
        readers.Add("ArquitecturaBaseMultitenant.Application/Modules/Assistant/Services/AssistantSessionPolicy.cs");
}
