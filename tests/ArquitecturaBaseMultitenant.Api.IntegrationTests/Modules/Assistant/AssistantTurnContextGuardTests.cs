using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.Extensions.AI;
using static System.FormattableString;

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
    /// uno pasado de páginas se rechaza, y uno normal (plano o comprimido) sigue entrando.
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

        var bombCompressed = CompressZlib([], 2L * 1024 * 1024 * 1024);
        var bombPdf = BuildPdf(pageCount: 1, streamBytes: bombCompressed, flateDecode: true);
        Assert.InRange(bombPdf.Length, 1024, AssistantTechnicalLimits.MaxAttachmentBytes);

        var (fits, allocated) = MeasureFits(bombPdf);
        Assert.False(fits);
        Assert.True(allocated < BoundedAllocation, $"La validación del PDF asignó {allocated} bytes.");
    }

    /// <summary>
    /// KAN-69: el stream de referencias cruzadas (/XRef) y el de objetos (/ObjStm) de un PDF 1.5 los inflaba PDFsharp enteros al
    /// abrirlo, antes de cualquier tope. Ahora se cuentan por bloques como los demás y se cortan en el tope; los normales entran.
    /// </summary>
    [Theory]
    [InlineData("xref")]
    [InlineData("objstm")]
    public void A_compressed_cross_reference_or_object_stream_bomb_is_rejected_with_bounded_memory(string stream)
    {
        byte[] Pdf(long padding) => stream == "xref" ? CrossReferenceStreamPdf(padding) : ObjectStreamPdf(padding, declaredObjects: 1, flate: true);
        Assert.True(AssistantTurnContextGuard.Fits([PdfMessage(Pdf(0))], [], Binding));

        var bomb = Pdf(64L * 1024 * 1024);
        Assert.InRange(bomb.Length, 1024, AssistantTechnicalLimits.MaxAttachmentBytes);
        var (fits, allocated) = MeasureFits(bomb);
        Assert.False(fits);
        Assert.True(allocated < BoundedAllocation, $"La validación del PDF asignó {allocated} bytes.");
    }

    /// <summary>
    /// KAN-69: PDFsharp reservaba por los números que declara el archivo antes de leerlo: un /Length de 200 MB, un /N de diez
    /// millones de objetos o un /Size de cinco millones de entradas con /W [0 0 0]. Ninguno dimensiona ahora una reserva.
    /// </summary>
    [Theory]
    [InlineData("length")]
    [InlineData("objects")]
    [InlineData("entries")]
    public void Numbers_declared_by_the_file_never_size_an_allocation(string number)
    {
        var pdf = number switch
        {
            "length" => OnePage().Stream(4, "", Encoding.ASCII.GetBytes("BT /F1 12 Tf 72 720 Td (Hola) Tj ET"), declaredLength: 200_000_000).WithTable(),
            "objects" => ObjectStreamPdf(0, declaredObjects: 10_000_000, flate: false),
            _ => CrossReferenceStreamPdf(0, declaredEntries: 5_000_000),
        };
        var (fits, allocated) = MeasureFits(pdf);
        Assert.True(fits);
        Assert.True(allocated < BoundedAllocation, $"La validación del PDF asignó {allocated} bytes.");
    }

    /// <summary>
    /// KAN-69: un /Prev que apunta a su propia tabla dejaba a PDFsharp en un bucle que crecía unos 150 MB por segundo hasta
    /// agotar la memoria. El guard no sigue /Prev: el PDF, de una página, se mide en el momento.
    /// </summary>
    [Fact]
    public async Task A_previous_cross_reference_pointing_to_itself_does_not_loop()
    {
        var pdf = OnePage().WithTable("/Prev {xref}");
        var check = Task.Run(() => AssistantTurnContextGuard.Fits([PdfMessage(pdf)], [], Binding), TestContext.Current.CancellationToken);
        var finished = await Task.WhenAny(check, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(check, finished);
        Assert.True(await check);
    }

    /// <summary>
    /// KAN-69: unos miles de corchetes anidados desbordaban la pila del parser recursivo de PDFsharp y tiraban el proceso entero
    /// (con dos mil ya pasa). Se recorren con un contador, sin recursión.
    /// </summary>
    [Fact]
    public void A_deeply_nested_object_does_not_exhaust_the_stack()
    {
        var pdf = OnePage().Object(5, new string('[', 100_000) + new string(']', 100_000)).WithTable();
        Assert.True(AssistantTurnContextGuard.Fits([PdfMessage(pdf)], [], Binding));
    }

    /// <summary>
    /// Las páginas salen de cada objeto /Type /Page, también dentro de un stream de objetos, y no solo del /Count del árbol, que
    /// puede mentir: siete páginas no entran aunque el árbol declare una.
    /// </summary>
    [Fact]
    public void Pages_inside_an_object_stream_count_even_when_the_tree_declares_fewer()
    {
        Assert.True(AssistantTurnContextGuard.Fits([PdfMessage(ObjectStreamPdf(0, declaredObjects: 1, flate: true))], [], Binding));
        var pages = AssistantTechnicalLimits.MaxPdfPages + 1;
        var lying = ObjectStreamPdf(0, declaredObjects: pages, flate: true, pages: pages);
        Assert.False(AssistantTurnContextGuard.Fits([PdfMessage(lying)], [], Binding));
    }

    /// <summary>
    /// Un escaneo en blanco y negro (CCITT o JBIG2), un RunLength o un JPEG comprimido con Flate cuentan sus bytes y entran; LZW,
    /// que se infla sin poder medirlo barato, una cadena que empieza con ASCII85 y un PDF protegido no entran.
    /// </summary>
    [Theory]
    [InlineData("/CCITTFaxDecode", true)]
    [InlineData("/JBIG2Decode", true)]
    [InlineData("/RunLengthDecode", true)]
    [InlineData("[/FlateDecode /DCTDecode]", true)]
    [InlineData("/LZWDecode", false)]
    [InlineData("[/ASCII85Decode /FlateDecode]", false)]
    [InlineData("encrypted", false)]
    public void Image_filters_count_their_bytes_while_unmeasurable_or_protected_pdfs_are_rejected(string filter, bool fits)
    {
        var opaque = Enumerable.Range(0, 32).Select(value => (byte)(value * 7)).ToArray();
        var pdf = filter switch
        {
            "encrypted" => OnePage().Object(5, "<< /Filter /Standard /V 1 /R 2 /O <" + new string('0', 64) + "> /U <" + new string('0', 64)
                + "> /P -4 >>").WithTable("/Encrypt 5 0 R /ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]"),
            _ => OnePage().Stream(5, "/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceGray /BitsPerComponent 1 /Filter " + filter,
                filter switch
                {
                    "/RunLengthDecode" => [2, (byte)'a', (byte)'b', (byte)'c', 253, (byte)'x', 128],
                    "[/FlateDecode /DCTDecode]" => CompressZlib([0xff, 0xd8, .. opaque, 0xff, 0xd9]),
                    _ => opaque,
                }).WithTable(),
        };
        Assert.Equal(fits, AssistantTurnContextGuard.Fits([PdfMessage(pdf)], [], Binding));
    }

    private const long BoundedAllocation = 8L * 1024 * 1024;

    /// <summary>Lo que asigna el hilo al medir un PDF, después de una medición normal que calienta el resto del guard.</summary>
    private static (bool Fits, long Allocated) MeasureFits(byte[] pdf)
    {
        Assert.True(AssistantTurnContextGuard.Fits([PdfMessage(OnePage().WithTable())], [], Binding));
        var messages = new[] { PdfMessage(pdf) };
        var binding = Binding;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var fits = AssistantTurnContextGuard.Fits(messages, [], binding);
        return (fits, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static ChatMessage PdfMessage(byte[] pdfBytes) =>
        new(ChatRole.User, [new TextContent("Revisá el PDF"), new DataContent(pdfBytes, "application/pdf")]);

    /// <summary>Comprime <paramref name="plain"/> seguido de <paramref name="zeros"/> ceros, de a un mega, sin tenerlos en memoria.</summary>
    private static byte[] CompressZlib(byte[] plain, long zeros = 0)
    {
        var chunk = new byte[1024 * 1024];
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(plain, 0, plain.Length);
            for (var remaining = zeros; remaining > 0; remaining -= chunk.Length)
                zlib.Write(chunk, 0, (int)Math.Min(chunk.Length, remaining));
        }
        return output.ToArray();
    }

    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";
    private const string PageTree = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>";

    /// <summary>El catálogo (1), el árbol (2) y una página (3); el contenido y lo que agrega cada prueba van desde el 4.</summary>
    private static PdfBuilder OnePage(string version = "1.4") => new PdfBuilder(version).Object(1, Catalog).Object(2, PageTree).Object(3, Page);

    private static byte[] BuildPdf(int pageCount, byte[] streamBytes, bool flateDecode)
    {
        // 1: Catalog, 2: Pages, 3: Font, 4: Stream, 5..(4+pageCount): Pages
        var kids = string.Join(" ", Enumerable.Range(5, pageCount).Select(id => Invariant($"{id} 0 R")));
        var pdf = new PdfBuilder("1.4")
            .Object(1, Catalog)
            .Object(2, Invariant($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>"))
            .Object(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
            .Stream(4, flateDecode ? "/Filter /FlateDecode" : "", streamBytes);
        for (var id = 5; id < 5 + pageCount; id++)
            pdf.Object(id, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents 4 0 R >>");
        return pdf.WithTable();
    }

    /// <summary>
    /// PDF 1.5 cuya tabla de referencias es un stream (4): comprimido, con sus entradas y <paramref name="padding"/> ceros detrás;
    /// con <paramref name="declaredEntries"/>, sin entradas, con /W [0 0 0] y ese /Size.
    /// </summary>
    private static byte[] CrossReferenceStreamPdf(long padding, int? declaredEntries = null)
    {
        var pdf = OnePage("1.5");
        var start = pdf.Position;
        if (declaredEntries is { } size)
            pdf.Stream(4, Invariant($"/Type /XRef /Size {size} /W [0 0 0] /Root 1 0 R"), []);
        else
        {
            byte[] entries = [.. Entry(0, 0, 255), .. Entry(1, pdf.OffsetOf(1), 0), .. Entry(1, pdf.OffsetOf(2), 0),
                .. Entry(1, pdf.OffsetOf(3), 0), .. Entry(1, start, 0)];
            pdf.Stream(4, "/Type /XRef /Size 5 /W [1 4 1] /Root 1 0 R /Filter /FlateDecode", CompressZlib(entries, padding));
        }
        return pdf.End(start);
    }

    /// <summary>
    /// PDF 1.5 con sus páginas (3 en adelante) dentro de un stream de objetos (objeto 50), anotado en una tabla de referencias sin
    /// comprimir (4): con el /N que se declare y, si va comprimido, <paramref name="padding"/> ceros detrás de las páginas.
    /// </summary>
    private static byte[] ObjectStreamPdf(long padding, int declaredObjects, bool flate, int pages = 1)
    {
        const int objectStream = 50;
        var pdf = new PdfBuilder("1.5").Object(1, Catalog).Object(2, PageTree);
        var offsets = Enumerable.Range(0, pages).Select(index => index * (Page.Length + 1));
        var header = Encoding.ASCII.GetBytes(string.Concat(offsets.Select((offset, index) => Invariant($"{3 + index} {offset} "))));
        byte[] content = [.. header, .. Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(Page + " ", pages)))];
        pdf.Stream(objectStream, Invariant($"/Type /ObjStm /N {declaredObjects} /First {header.Length}") + (flate ? " /Filter /FlateDecode" : ""),
            flate ? CompressZlib(content, padding) : content);
        var start = pdf.Position;
        var entries = new List<byte>();
        for (var id = 0; id <= objectStream; id++)
        {
            entries.AddRange(id switch
            {
                1 or 2 => Entry(1, pdf.OffsetOf(id), 0),
                4 => Entry(1, start, 0),
                objectStream => Entry(1, pdf.OffsetOf(objectStream), 0),
                _ when id >= 3 && id < 3 + pages => Entry(2, objectStream, (byte)(id - 3)),
                _ => Entry(0, 0, 255),
            });
        }
        pdf.Stream(4, Invariant($"/Type /XRef /Size {objectStream + 1} /W [1 4 1] /Root 1 0 R"), [.. entries]);
        return pdf.End(start);
    }

    private static byte[] Entry(byte type, long field2, byte field3) => [type, .. BigEndian32((int)field2), field3];

    /// <summary>Arma un PDF de a un objeto, anotando dónde empieza cada uno para su tabla de referencias.</summary>
    private sealed class PdfBuilder
    {
        private readonly List<byte> _pdf = [];
        private readonly SortedDictionary<int, long> _offsets = [];

        internal PdfBuilder(string version) => Ascii("%PDF-" + version + "\n");

        internal long Position => _pdf.Count;

        internal long OffsetOf(int id) => _offsets[id];

        internal PdfBuilder Object(int id, string body)
        {
            _offsets[id] = _pdf.Count;
            return Ascii(Invariant($"{id} 0 obj\n{body}\nendobj\n"));
        }

        internal PdfBuilder Stream(int id, string dictionary, byte[] data, long? declaredLength = null)
        {
            _offsets[id] = _pdf.Count;
            Ascii(Invariant($"{id} 0 obj\n<< {dictionary} /Length {declaredLength ?? data.Length} >>\nstream\n"));
            _pdf.AddRange(data);
            return Ascii("\nendstream\nendobj\n");
        }

        /// <summary>La tabla clásica, con una entrada libre por cada número que falta, y su trailer; {xref} es donde empieza.</summary>
        internal byte[] WithTable(string trailer = "")
        {
            var start = _pdf.Count;
            var size = _offsets.Keys.Max() + 1;
            var table = new StringBuilder(Invariant($"xref\n0 {size}\n0000000000 65535 f \n"));
            for (var id = 1; id < size; id++)
                table.Append(_offsets.TryGetValue(id, out var offset) ? Invariant($"{offset:D10} 00000 n \n") : "0000000000 65535 f \n");
            Ascii(table.ToString());
            Ascii(Invariant($"trailer\n<< /Size {size} /Root 1 0 R {trailer.Replace("{xref}", start.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)} >>\n"));
            return End(start);
        }

        internal byte[] End(long start)
        {
            Ascii(Invariant($"startxref\n{start}\n%%EOF\n"));
            return [.. _pdf];
        }

        private PdfBuilder Ascii(string text)
        {
            _pdf.AddRange(Encoding.ASCII.GetBytes(text));
            return this;
        }
    }
}
