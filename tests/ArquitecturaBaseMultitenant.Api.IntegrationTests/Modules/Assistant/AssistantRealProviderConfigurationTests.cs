using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;
using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf.IO;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantRealProviderConfigurationTests
{
    [Fact]
    public void Runner_content_root_points_to_the_existing_api_project()
    {
        var root = AssistantRealProviderFixture.ApiContentRoot();
        Assert.True(Path.IsPathFullyQualified(root));
        Assert.True(File.Exists(Path.Combine(root, "ArquitecturaBaseMultitenant.Api.csproj")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Missing_or_blank_key_fails_before_any_provider_or_database_access(string? value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [AssistantRealProviderFixture.KeyName] = value }).Build();
        var failure = Assert.Throws<AssistantRealProviderFixture.ProbeFailure>(() => AssistantRealProviderFixture.HostSettings(configuration));
        Assert.Equal("ConfiguredKeyRequired", failure.Code);
        Assert.Equal("Assistant real probe failed.", failure.Message);
    }

    [Fact]
    public void Host_imports_only_the_requested_secret_and_explicitly_disables_fake()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [AssistantRealProviderFixture.KeyName] = "synthetic-probe-key", ["ConnectionStrings:appdb"] = "unrelated-secret" }).Build();
        var settings = AssistantRealProviderFixture.HostSettings(configuration);
        Assert.Equal("false", settings["Assistant:UseFakeChatClient"]);
        Assert.Equal("true", settings["Assistant:IsolatedE2E"]);
        Assert.Equal("true", settings["Assistant:TurnWorkerEnabled"]);
        Assert.True(settings.ContainsKey(AssistantRealProviderFixture.KeyName));
        Assert.False(settings.ContainsKey("ConnectionStrings:appdb"));
    }

    [Fact]
    public void Provider_assets_contain_a_full_rgb_image_and_a_parseable_pdf_page()
    {
        var files = AssistantRealProviderAssets.Files();
        var png = files.Single(file => file.Type == "image/png").Bytes;
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal(128u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(128u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20)));
        using var compressed = new MemoryStream();
        for (var offset = 8; offset < png.Length;)
        {
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset)));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "IDAT") compressed.Write(png, offset + 8, length);
            offset += length + 12;
        }
        compressed.Position = 0;
        using var decoder = new ZLibStream(compressed, CompressionMode.Decompress);
        using var pixels = new MemoryStream(); decoder.CopyTo(pixels);
        Assert.Equal(128 * (1 + 128 * 3), pixels.Length);
        var rows = pixels.ToArray();
        Assert.Equal(new byte[] { 0, 255, 0, 0 }, rows[..4]);
        using var pdfStream = new MemoryStream(files.Single(file => file.Type == "application/pdf").Bytes);
        using var pdf = PdfReader.Open(pdfStream, PdfDocumentOpenMode.Import);
        Assert.Equal(1, pdf.PageCount);
    }
}
