using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Files;
using ArquitecturaBaseMultitenant.Application.Models.Files;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Files;

public sealed class LocalFileStoreOptions
{
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "arquitectura-private-files");
}

internal sealed class LocalFileStore(IOptions<LocalFileStoreOptions> options) : IFileStore, IFileStoreMaintenance
{
    public async Task<StoredFile> PutAsync(FileObject file, Stream content, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(file.ByteLength);
        var key = Guid.NewGuid().ToString("N");
        var path = Resolve(key);
        var temporary = path + ".pending";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var journal = Journal(key, file.Category);
        await File.WriteAllTextAsync(journal, string.Empty, cancellationToken);
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await content.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    total += count;
                    if (total > file.ByteLength) throw new InvalidDataException("The content length changed.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                if (total != file.ByteLength) throw new InvalidDataException("The content length changed.");
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporary, path);
            return new StoredFile(key);
        }
        catch { File.Delete(temporary); File.Delete(journal); throw; }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        try { return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous)); }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<Stream?>(null); }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Resolve(storageKey);
        File.Delete(path);
        File.Delete(path + ".pending");
        var root = Path.GetDirectoryName(path)!;
        if (Directory.Exists(root))
            foreach (var journal in Directory.EnumerateFiles(root, storageKey + ".*.unconfirmed")) File.Delete(journal);
        return Task.CompletedTask;
    }

    public Task ConfirmAsync(string storageKey, string category, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(Journal(storageKey, category));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListUnconfirmedAsync(string category, DateTime cutoffUtc, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var root = Path.GetDirectoryName(Journal(Guid.Empty.ToString("N"), category))!;
        if (!Directory.Exists(root)) return Task.FromResult<IReadOnlyList<string>>([]);
        return Task.FromResult<IReadOnlyList<string>>(Directory.EnumerateFiles(root, "*." + category + ".unconfirmed")
            .Where(path => File.GetLastWriteTimeUtc(path) <= cutoffUtc).Order(StringComparer.Ordinal).Take(100)
            .Select(path => Path.GetFileName(path)[..32]).ToArray());
    }

    private string Journal(string key, string category)
    {
        if (category.Length is < 1 or > 60 || category.Any(value => !char.IsAsciiLetterOrDigit(value) && value != '-'))
            throw new ArgumentException("A technical category is required.", nameof(category));
        return Resolve(key) + "." + category + ".unconfirmed";
    }

    private string Resolve(string key)
    {
        if (key.Length != 32 || !Guid.TryParseExact(key, "N", out _)) throw new ArgumentException("An opaque storage key is required.", nameof(key));
        var root = Path.GetFullPath(options.Value.RootPath);
        if (root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("wwwroot", StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Private files cannot use a web root.");
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Private storage cannot traverse symbolic links.");
        var path = Path.Combine(root, key);
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Private storage cannot traverse symbolic links.");
        return path;
    }
}
