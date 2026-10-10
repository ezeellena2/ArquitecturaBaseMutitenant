using ArquitecturaBaseMultitenant.Application.Models.Files;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;

public interface IFileStore
{
    Task<StoredFile> PutAsync(FileObject file, Stream content, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
