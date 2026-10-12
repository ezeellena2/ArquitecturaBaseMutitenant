using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

internal static class AssistantRealProviderAssets
{
    internal const string PdfMarker = "AB8B-REAL-PDF-42";
    internal sealed record File(string Name, string Type, byte[] Bytes);
    internal static File[] Files()
    {
        return [new("imagen.png", "image/png", RedPng()), new("documento.pdf", "application/pdf", MarkerPdf())];
    }
    private static byte[] MarkerPdf()
    {
        // PDF real de una página con Helvetica estándar: no incrusta una fuente completa en el contexto.
        var content = "BT /F1 18 Tf 72 720 Td (Verification code: " + PdfMarker + ") Tj ET\n";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Length " + System.Xml.XmlConvert.ToString(Encoding.ASCII.GetByteCount(content)) + " >>\nstream\n" + content + "endstream",
        ];
        using var pdf = new MemoryStream();
        void Write(string value) => pdf.Write(Encoding.ASCII.GetBytes(value));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(pdf.Position);
            Write(System.Xml.XmlConvert.ToString(index + 1) + " 0 obj\n" + objects[index] + "\nendobj\n");
        }
        var xref = pdf.Position;
        Write("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write(System.Xml.XmlConvert.ToString(offset).PadLeft(10, '0') + " 00000 n \n");
        Write("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + System.Xml.XmlConvert.ToString(xref) + "\n%%EOF\n");
        return pdf.ToArray();
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
