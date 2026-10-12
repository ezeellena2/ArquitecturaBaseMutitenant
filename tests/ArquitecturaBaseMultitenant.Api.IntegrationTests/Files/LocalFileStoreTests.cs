using ArquitecturaBaseMultitenant.Application.Models.Files;
using ArquitecturaBaseMultitenant.Infrastructure.Files;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Files;

public sealed class LocalFileStoreTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Private_store_writes_atomically_reads_and_deletes_with_opaque_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new LocalFileStore(Options.Create(new LocalFileStoreOptions { RootPath = root }));
        try
        {
            await using var input = new MemoryStream([1, 2, 3]);
            var saved = await store.PutAsync(new FileObject(Guid.NewGuid(), "image/png", 3), input, Ct);
            Assert.Single(await store.ListUnconfirmedAsync("general", new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc), Ct));
            await store.ConfirmAsync(saved.StorageKey, "general", Ct);
            Assert.Empty(await store.ListUnconfirmedAsync("general", new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc), Ct));
            Assert.True(Guid.TryParseExact(saved.StorageKey, "N", out _));
            await using (var read = await store.OpenReadAsync(saved.StorageKey, Ct))
            {
                Assert.NotNull(read);
                using var output = new MemoryStream();
                await read.CopyToAsync(output, Ct);
                Assert.Equal(new byte[] { 1, 2, 3 }, output.ToArray());
            }
            await store.DeleteAsync(saved.StorageKey, Ct);
            await store.DeleteAsync(saved.StorageKey, Ct);
            Assert.Null(await store.OpenReadAsync(saved.StorageKey, Ct));
            await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync("../outside", Ct));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.PutAsync(new FileObject(Guid.NewGuid(), "image/png", 2), new MemoryStream([1, 2, 3]), Ct));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
