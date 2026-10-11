using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.RealE2ESetup;

/// <summary>
/// Publica la siguiente versión de términos cuando el runner deja una orden en su archivo de control.
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
            || (command.RootElement.TryGetProperty("version", out var legacyVersion) && legacyVersion.GetInt32() != 2))
            throw new InvalidOperationException("Unsupported isolated E2E legal command.");

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var published = await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var repository = provider.GetRequiredService<ILegalRepository>();
            await repository.LockKindAsync(LegalDocumentKind.Terms, ct);
            var latestVersion = await repository.GetLatestVersionAsync(LegalDocumentKind.Terms, ct);
            var nowUtc = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            var document = LegalDocument.Create(LegalDocumentKind.Terms, checked(latestVersion + 1), nowUtc);
            repository.AddDocument(document);
            repository.AddContent(LegalDocumentContent.Create(document.Id, "es-AR", "Términos nuevos de prueba."));
            repository.AddContent(LegalDocumentContent.Create(document.Id, "en-US", "New test terms."));
            return Result.Success((Id: document.Id, Version: document.Version));
        }, CommitPolicy.OnSuccess, cancellationToken);
        // El lector ve el recibo completo, y únicamente después de confirmar la publicación.
        var temporaryFile = completedFile + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(new
                { id = published.Value.Id, version = published.Value.Version }), cancellationToken);
            File.Move(temporaryFile, completedFile);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }
}
