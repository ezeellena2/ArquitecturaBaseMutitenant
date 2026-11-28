using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.Extensions.AI;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantTurnContextGuardTests
{
    private static AssistantChatBinding Binding => new(AssistantMode.PlatformKey, Guid.NewGuid(), "fixture-model", AssistantEffort.Low);

    [Theory]
    [InlineData("arguments")]
    [InlineData("callId")]
    [InlineData("thinking")]
    [InlineData("protected")]
    [InlineData("result")]
    [InlineData("description")]
    public void Content_outside_plain_text_cannot_escape_the_complete_input_bound(string field)
    {
        var large = new string('x', 60000);
        AIContent content = field switch
        {
            "arguments" => new FunctionCallContent("call", "my_account", new Dictionary<string, object?> { ["padding"] = large }),
            "callId" => new FunctionCallContent(large, "my_account", new Dictionary<string, object?>()),
            "thinking" => new TextReasoningContent(large),
            "protected" => new TextReasoningContent("") { ProtectedData = large },
            "result" => new FunctionResultContent("call", large),
            _ => new TextContent("Brief"),
        };
        var messages = new[] { new ChatMessage(ChatRole.Assistant, [content]) };
        var tools = new AITool[] { Declaration(field == "description" ? large : "Description") };
        Assert.False(AssistantTurnContextGuard.Fits(messages, tools, Binding));
    }

    [Fact]
    public void Small_complete_context_with_opaque_blocks_and_a_tool_is_accepted()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "Brief"),
            new ChatMessage(ChatRole.Assistant,
                [new TextReasoningContent("Private fixture") { ProtectedData = "opaque-fixture" },
                 new FunctionCallContent("call", "my_account", new Dictionary<string, object?>())]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call", "{}")]),
        };
        Assert.True(AssistantTurnContextGuard.Fits(messages, [Declaration("Description")], Binding));
    }

    private static AIFunctionDeclaration Declaration(string description) => AIFunctionFactory.CreateDeclaration("my_account", description,
        JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, additionalProperties = false }));

    [Fact]
    public void An_inline_image_uses_the_provider_visual_bound_instead_of_base64_tokens()
    {
        var messages = new[] { new ChatMessage(ChatRole.User, [new TextContent("Consulta"), new DataContent(new byte[100_000], "image/png")]) };
        Assert.True(AssistantTurnContextGuard.Fits(messages, [], Binding));
        var unknown = new[] { new ChatMessage(ChatRole.User, [new DataContent(new byte[100], "application/octet-stream")]) };
        Assert.False(AssistantTurnContextGuard.Fits(unknown, [], Binding));
    }
    /// <summary>
    /// La cota visual sale de las dimensiones reales de la imagen: una chica cuenta el marco y sus parches, una grande el
    /// tope, y una que no se puede leer, también el tope.
    /// </summary>
    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp-lossy")]
    [InlineData("webp-lossless")]
    [InlineData("webp-extended")]
    public void An_image_counts_its_real_size_and_never_more_than_the_maximum(string format)
    {
        var (tiny, mediaType) = Image(format, 1, 1);
        var (medium, _) = Image(format, 270, 271);
        var (large, _) = Image(format, 4000, 3000);
        Assert.Equal(AssistantTurnContextGuard.ImageFrameTokens + 1, AssistantTurnContextGuard.ImageTokens(tiny, mediaType));
        Assert.Equal(AssistantTurnContextGuard.ImageFrameTokens + 10 * 11, AssistantTurnContextGuard.ImageTokens(medium, mediaType));
        Assert.Equal(AssistantTurnContextGuard.MaxImageTokens, AssistantTurnContextGuard.ImageTokens(large, mediaType));
        Assert.Equal(AssistantTurnContextGuard.MaxImageTokens, AssistantTurnContextGuard.ImageTokens(tiny.AsSpan(0, 20), mediaType));
        Assert.Equal(AssistantTurnContextGuard.MaxImageTokens, AssistantTurnContextGuard.ImageTokens(new byte[100], mediaType));
    }

    /// <summary>
    /// Cinco imágenes chicas dejan lugar para la conversación y las herramientas (con las de acciones, unos 13 KB); cinco
    /// grandes, que cuentan el tope cada una, no.
    /// </summary>
    [Fact]
    public void Five_small_images_leave_room_for_the_text_and_the_tools_and_five_large_ones_do_not()
    {
        var text = new string('x', 13_000);
        ChatMessage[] With(byte[] image) => [new ChatMessage(ChatRole.User,
            [new TextContent(text), .. Enumerable.Range(0, AssistantTechnicalLimits.MaxAttachments).Select(_ => new DataContent(image, "image/png"))])];
        Assert.True(AssistantTurnContextGuard.Fits(With(Image("png", 1, 1).Bytes), [Declaration("Description")], Binding));
        Assert.False(AssistantTurnContextGuard.Fits(With(Image("png", 4000, 3000).Bytes), [Declaration("Description")], Binding));
    }

    private static (byte[] Bytes, string MediaType) Image(string format, int width, int height) => format switch
    {
        "png" => ([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, .. "IHDR"u8, .. BigEndian32(width), .. BigEndian32(height), 8, 0, 0, 0, 0], "image/png"),
        "jpeg" => ([0xff, 0xd8, 0xff, 0xe0, 0, 16, .. "JFIF"u8, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0,
            0xff, 0xc0, 0, 17, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3], "image/jpeg"),
        "webp-lossy" => (Webp("VP8 ", [0, 0, 0, 0x9d, 0x01, 0x2a, (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8)]), "image/webp"),
        "webp-lossless" => (Webp("VP8L", [0x2f, .. LittleEndian32((uint)(width - 1) | ((uint)(height - 1) << 14)), 0, 0, 0, 0, 0]), "image/webp"),
        _ => (Webp("VP8X", [0, 0, 0, 0, (byte)(width - 1), (byte)((width - 1) >> 8), (byte)((width - 1) >> 16),
            (byte)(height - 1), (byte)((height - 1) >> 8), (byte)((height - 1) >> 16)]), "image/webp"),
    };

    private static byte[] Webp(string chunk, byte[] payload) =>
        [.. "RIFF"u8, .. LittleEndian32((uint)(payload.Length + 12)), .. "WEBP"u8, .. System.Text.Encoding.ASCII.GetBytes(chunk), .. LittleEndian32((uint)payload.Length), .. payload];

    private static byte[] BigEndian32(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] LittleEndian32(uint value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];

    /// <summary>
    /// KAN-69: un PDF con un stream /FlateDecode que se infla a 2 GB se corta en streaming sin inflar en RAM,
    /// uno pasado de páginas se rechaza antes de descomprimir, y uno normal (plano o comprimido) sigue entrando.
    /// </summary>
    [Fact]
    public void Pdf_stream_bomb_and_excessive_pages_are_rejected_with_bounded_memory_while_normal_pdfs_fit()
    {
        var normalContent = Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Hola) Tj ET\n");
        var normalPlain = BuildPdf(pageCount: 1, streamBytes: normalContent, flateDecode: false);
        var normalCompressed = BuildPdf(pageCount: 1, streamBytes: CompressZlib(normalContent), flateDecode: true);
        Assert.True(AssistantTurnContextGuard.Fits([PdfMessage(normalPlain)], [], Binding));
        Assert.True(AssistantTurnContextGuard.Fits([PdfMessage(normalCompressed)], [], Binding));

        var tooManyPages = BuildPdf(pageCount: AssistantTechnicalLimits.MaxPdfPages + 1, streamBytes: normalContent, flateDecode: false);
        Assert.False(AssistantTurnContextGuard.Fits([PdfMessage(tooManyPages)], [], Binding));

        var bombCompressed = CompressZerosZlib(2L * 1024 * 1024 * 1024);
        var bombPdf = BuildPdf(pageCount: 1, streamBytes: bombCompressed, flateDecode: true);
        Assert.InRange(bombPdf.Length, 1024, AssistantTechnicalLimits.MaxAttachmentBytes);

        var messages = new[] { PdfMessage(bombPdf) };
        var before = GC.GetAllocatedBytesForCurrentThread();
        var fits = AssistantTurnContextGuard.Fits(messages, [], Binding);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.False(fits);
        Assert.True(allocated < 32L * 1024 * 1024, $"La validación del PDF asignó {allocated} bytes.");
    }

    private static ChatMessage PdfMessage(byte[] pdfBytes) =>
        new(ChatRole.User, [new TextContent("Revisá el PDF"), new DataContent(pdfBytes, "application/pdf")]);

    private static byte[] CompressZlib(byte[] plain)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(plain, 0, plain.Length);
        return output.ToArray();
    }

    private static byte[] CompressZerosZlib(long totalBytes)
    {
        var chunk = new byte[1024 * 1024];
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var remaining = totalBytes;
            while (remaining > 0)
            {
                var step = (int)Math.Min(chunk.Length, remaining);
                zlib.Write(chunk, 0, step);
                remaining -= step;
            }
        }
        return output.ToArray();
    }

    private static byte[] BuildPdf(int pageCount, byte[] streamBytes, bool flateDecode)
    {
        using var pdf = new MemoryStream();
        void WriteAscii(string text) => pdf.Write(Encoding.ASCII.GetBytes(text));
        var offsets = new List<long>();
        void BeginObject(int id)
        {
            offsets.Add(pdf.Position);
            WriteAscii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
        }

        // 1: Catalog, 2: Pages, 3: Font, 4: Stream, 5..(4+pageCount): Pages
        var kids = string.Join(" ", Enumerable.Range(5, pageCount).Select(id => id.ToString(CultureInfo.InvariantCulture) + " 0 R"));
        WriteAscii("%PDF-1.4\n");
        BeginObject(1);
        WriteAscii("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        BeginObject(2);
        WriteAscii($"<< /Type /Pages /Kids [{kids}] /Count {pageCount.ToString(CultureInfo.InvariantCulture)} >>\nendobj\n");
        BeginObject(3);
        WriteAscii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        BeginObject(4);
        var filter = flateDecode ? " /Filter /FlateDecode" : string.Empty;
        WriteAscii($"<< /Length {streamBytes.Length.ToString(CultureInfo.InvariantCulture)}{filter} >>\nstream\n");
        pdf.Write(streamBytes, 0, streamBytes.Length);
        WriteAscii("\nendstream\nendobj\n");
        for (var i = 0; i < pageCount; i++)
        {
            BeginObject(5 + i);
            WriteAscii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents 4 0 R >>\nendobj\n");
        }
        var xref = pdf.Position;
        var count = offsets.Count + 1;
        WriteAscii($"xref\n0 {count.ToString(CultureInfo.InvariantCulture)}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            WriteAscii(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        WriteAscii($"trailer\n<< /Size {count.ToString(CultureInfo.InvariantCulture)} /Root 1 0 R >>\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        return pdf.ToArray();
    }
}
