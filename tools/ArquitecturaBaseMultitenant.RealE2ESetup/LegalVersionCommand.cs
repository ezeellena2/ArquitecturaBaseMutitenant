using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>
/// Publica una segunda versión de términos cuando el runner deja una orden en su archivo de control.
/// Permite probar la aceptación de nuevos legales dentro de la base aislada del recorrido E2E.
/// </summary>
internal static class LegalVersionCommand
{
    public static async Task TryExecuteAsync(IServiceProvider services, string readyFile,
        CancellationToken cancellationToken)
    {
        var commandFile = Path.ChangeExtension(readyFile, ".legal.json");
        var completedFile = commandFile + ".done";
        if (!File.Exists(commandFile) || File.Exists(completedFile)) return;
        using var command = JsonDocument.Parse(await File.ReadAllTextAsync(commandFile, cancellationToken));
        if (command.RootElement.GetProperty("kind").GetString() != "Terms"
            || command.RootElement.GetProperty("version").GetInt32() != 2)
            throw new InvalidOperationException("Unsupported isolated E2E legal command.");

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var repository = provider.GetRequiredService<ILegalRepository>();
            var current = await repository.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, ct);
            if (current?.Version != 2)
            {
                var document = LegalDocument.Create(LegalDocumentKind.Terms, 2, nowUtc);
                repository.AddDocument(document);
                repository.AddContent(LegalDocumentContent.Create(document.Id, "es-AR", "Términos nuevos de prueba."));
                repository.AddContent(LegalDocumentContent.Create(document.Id, "en-US", "New test terms."));
            }
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);
        await using var marker = new FileStream(completedFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await marker.FlushAsync(cancellationToken);
    }
}
