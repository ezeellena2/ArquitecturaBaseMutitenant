using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Export;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

internal static class AssistantRealProviderAssets
{
    internal const string PdfMarker = "AB8B-REAL-PDF-42";
    internal sealed record File(string Name, string Type, byte[] Bytes);
    internal static File[] Files()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var conversation = new AssistantConversationSummaryResponse(Guid.NewGuid(), "Documento sintético", false, 1, now, now);
        var message = new AssistantMessageResponse(Guid.NewGuid(), AssistantMessageRole.Assistant, "Código de verificación: " + PdfMarker,
            AssistantMessageStatus.Complete, now, 1, [], [], null);
        var pdf = new AssistantExportRenderer().Render(conversation, [message], AssistantExportFormat.Pdf);
        using var content = pdf.Content;
        using var bytes = new MemoryStream(); content.CopyTo(bytes);
        return [new("imagen.png", "image/png", RedPng()), new("documento.pdf", "application/pdf", bytes.ToArray())];
    }
    private static byte[] RedPng()
    {
        const int size = 128;
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, size);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), size);
        header[8] = 8; header[9] = 2;
        Chunk(png, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            for (var row = 0; row < size; row++)
            {
                zlib.WriteByte(0);
                for (var column = 0; column < size; column++) zlib.Write([255, 0, 0]);
            }
        Chunk(png, "IDAT", compressed.ToArray()); Chunk(png, "IEND", []);
        return png.ToArray();
    }
    private static void Chunk(Stream stream, string name, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length); stream.Write(number);
        var type = Encoding.ASCII.GetBytes(name); stream.Write(type); stream.Write(data);
        uint crc = uint.MaxValue;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); stream.Write(number);
    }
}
