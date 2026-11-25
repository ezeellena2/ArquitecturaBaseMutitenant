namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// B6 y 264: la entrega de una invitación sale del estado de su mensaje en la cola con una sola regla,
/// <c>InvitationDeliveryProjection</c>. Personas, Miembros y Cuentas la usan para mostrar, filtrar y ordenar; una copia escrita a
/// mano en un reader dejaría de ordenar por lo que muestra la fila el día que la regla cambie.
/// </summary>
public sealed class InvitationDeliveryRuleTests
{
    [Fact]
    public void The_invitation_delivery_rule_is_written_once()
    {
        var source = Path.Combine(SolutionRoot.FullPath, "src");
        var writers = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal))
            // Lo que la regla decide: un mensaje que no salió es una entrega fallida.
            .Where(path => File.ReadLines(Path.Combine(source, path))
                .Any(line => line.Contains("InvitationDeliveryStatus.Failed", StringComparison.Ordinal) && !line.TrimStart().StartsWith("///", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/InvitationDeliveryProjection.cs"], writers);
    }
}
