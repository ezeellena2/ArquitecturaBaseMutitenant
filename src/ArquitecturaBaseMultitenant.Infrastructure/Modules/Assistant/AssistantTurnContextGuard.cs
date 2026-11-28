using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using Microsoft.Extensions.AI;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Conservative complete-context bound, computed only in provider memory before a billable call.</summary>
internal static class AssistantTurnContextGuard
{
    internal static bool Fits(IReadOnlyList<ChatMessage> messages, IReadOnlyList<AITool> tools, AssistantChatBinding binding)
    {
        try
        {
            // UTF8 bytes overbound textual tokens, including opaque reasoning, function arguments,
            // names, call IDs, result values and metadata. Extra framing covers the provider envelope.
            long bytes = 1024 + JsonSerializer.SerializeToUtf8Bytes(new
            {
                binding.ProviderModelId,
                binding.Effort,
                MaxOutputTokens = AssistantTechnicalLimits.MaxOutputTokens,
            }, AIJsonUtilities.DefaultOptions).LongLength;
            long payloadBytes = bytes;
            foreach (var message in messages)
            {
                if (message.Contents.Any(content => content is not (TextContent or TextReasoningContent or FunctionCallContent or FunctionResultContent or DataContent)))
                    return false;
                var textOnly = new ChatMessage(message.Role, message.Contents.Where(content => content is not DataContent).ToList())
                { AuthorName = message.AuthorName, AdditionalProperties = message.AdditionalProperties };
                bytes += 64 + JsonSerializer.SerializeToUtf8Bytes(textOnly, AIJsonUtilities.DefaultOptions).LongLength;
                payloadBytes += 64 + JsonSerializer.SerializeToUtf8Bytes(textOnly, AIJsonUtilities.DefaultOptions).LongLength;
                foreach (var content in message.Contents.OfType<DataContent>())
                {
                    if (content.Data.Length == 0 || content.Data.Length > AssistantTechnicalLimits.MaxAttachmentBytes) return false;
                    payloadBytes += 256 + ((content.Data.Length + 2L) / 3L * 4L);
                    if (payloadBytes > AssistantTechnicalLimits.MaxProviderPayloadBytes) return false;
                    if (Domain.Common.FileMediaTypes.Images.Contains(content.MediaType)) bytes += ImageTokens(content.Data.Span, content.MediaType);
                    else if (content.MediaType == Domain.Common.FileMediaTypes.Pdf)
                    {
                        if (!TryAccountPdf(content.Data, ref bytes)) return false;
                    }
                    else return false;
                }
                if (bytes > AssistantTechnicalLimits.MaxInputTokens) return false;
            }
            foreach (var tool in tools)
            {
                if (tool is not AIFunctionDeclaration function) return false;
                bytes += 64 + JsonSerializer.SerializeToUtf8Bytes(new
                {
                    function.Name,
                    function.Description,
                    function.JsonSchema,
                    function.ReturnJsonSchema,
                    function.AdditionalProperties,
                }, AIJsonUtilities.DefaultOptions).LongLength;
                if (bytes > AssistantTechnicalLimits.MaxInputTokens) return false;
            }
            return bytes <= AssistantTechnicalLimits.MaxInputTokens;
        }
        catch (JsonException) { return false; }
        catch (InvalidDataException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (PdfReaderException) { return false; }
    }

    private const int PdfInflateBufferBytes = 8192;

    /// <summary>
    /// Acota páginas y descomprime cada stream por bloques sin usar <c>UnfilteredValue</c> (KAN-69), cortando apenas supera
    /// <see cref="AssistantTechnicalLimits.MaxInputTokens"/>.
    /// </summary>
    private static bool TryAccountPdf(ReadOnlyMemory<byte> data, ref long bytes)
    {
        using var input = MemoryMarshal.TryGetArray(data, out var segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(data.ToArray(), writable: false);
        using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        if (pdf.SecuritySettings.IsEncrypted || pdf.PageCount < 0 || pdf.PageCount > AssistantTechnicalLimits.MaxPdfPages) return false;
        bytes += AssistantTechnicalLimits.PdfTokensPerPage * pdf.PageCount;
        if (bytes > AssistantTechnicalLimits.MaxInputTokens) return false;

        byte[]? inflateBuffer = null;
        foreach (var dictionary in pdf.Internals.GetAllObjects().OfType<PdfDictionary>())
        {
            if (dictionary.Stream is not { Value: { Length: > 0 } raw }) continue;
            if (!TryMeasurePdfStream(dictionary, raw, ref bytes, ref inflateBuffer)) return false;
        }
        return true;
    }

    private static bool TryMeasurePdfStream(PdfDictionary dictionary, byte[] raw, ref long bytes, ref byte[]? inflateBuffer)
    {
        if (!dictionary.Elements.TryGetValue("/Filter", out var filter) || filter is null)
        {
            bytes += raw.LongLength;
            return bytes <= AssistantTechnicalLimits.MaxInputTokens;
        }
        if (filter is PdfReference reference) filter = reference.Value;
        string? filterName;
        if (filter is PdfArray array)
        {
            if (array.Elements.Count == 0)
            {
                bytes += raw.LongLength;
                return bytes <= AssistantTechnicalLimits.MaxInputTokens;
            }
            if (array.Elements.Count > 1) return false;
            filterName = ReadFilterName(array.Elements[0]);
        }
        else filterName = ReadFilterName(filter);

        switch (filterName)
        {
            case "FlateDecode" or "Fl":
                inflateBuffer ??= new byte[PdfInflateBufferBytes];
                return TryInflateFlate(raw, ref bytes, inflateBuffer);
            case "ASCIIHexDecode" or "AHx" or "ASCII85Decode" or "A85" or "DCTDecode" or "DCT" or "JPXDecode":
                bytes += raw.LongLength;
                return bytes <= AssistantTechnicalLimits.MaxInputTokens;
            default:
                return false;
        }
    }

    private static string? ReadFilterName(PdfItem? item) => item switch
    {
        PdfReference reference => ReadFilterName(reference.Value),
        PdfName name => name.Value.TrimStart('/'),
        PdfNameObject nameObject => nameObject.Value.TrimStart('/'),
        _ => null,
    };

    private static bool TryInflateFlate(byte[] raw, ref long bytes, byte[] buffer)
    {
        using var compressed = new MemoryStream(raw, writable: false);
        using var inflater = new ZLibStream(compressed, CompressionMode.Decompress, leaveOpen: false);
        while (true)
        {
            var remaining = AssistantTechnicalLimits.MaxInputTokens - bytes;
            var toRead = (int)Math.Min(buffer.Length, remaining + 1);
            var read = inflater.Read(buffer, 0, toRead);
            if (read == 0) return true;
            bytes += read;
            if (bytes > AssistantTechnicalLimits.MaxInputTokens) return false;
        }
    }

    // The most one image may count, whatever its size: above the documented high-resolution maximum (about 4784 tokens).
    internal const long MaxImageTokens = 8192;
    // Block framing and any provider minimum for a very small image.
    internal const long ImageFrameTokens = 1024;
    // Providers count about one visual token per 28x28 px patch (or per 750 px²) and never upscale: a 27 px patch overbounds both.
    private const long ImagePatchPixels = 27;

    /// <summary>
    /// Visual bound of one image from its real dimensions, read from its header: one token per 27x27 px patch plus the framing,
    /// never above <see cref="MaxImageTokens"/>. An image whose size cannot be read counts the maximum.
    /// </summary>
    internal static long ImageTokens(ReadOnlySpan<byte> data, string? mediaType)
    {
        long width = 0, height = 0;
        var read = mediaType switch
        {
            Domain.Common.FileMediaTypes.Png => TryReadPngSize(data, out width, out height),
            Domain.Common.FileMediaTypes.Jpeg => TryReadJpegSize(data, out width, out height),
            Domain.Common.FileMediaTypes.Webp => TryReadWebpSize(data, out width, out height),
            _ => false,
        };
        if (!read || width <= 0 || height <= 0) return MaxImageTokens;
        var patches = (width + ImagePatchPixels - 1) / ImagePatchPixels * ((height + ImagePatchPixels - 1) / ImagePatchPixels);
        return Math.Min(MaxImageTokens, ImageFrameTokens + patches);
    }

    // PNG: the IHDR chunk always comes first, with width and height as big-endian 32-bit values.
    private static bool TryReadPngSize(ReadOnlySpan<byte> data, out long width, out long height)
    {
        width = height = 0;
        if (data.Length < 24 || !Domain.Common.FileSignaturePolicy.Matches(Domain.Common.FileMediaTypes.Png, data)
            || !data[12..16].SequenceEqual("IHDR"u8)) return false;
        width = BinaryPrimitives.ReadUInt32BigEndian(data[16..20]);
        height = BinaryPrimitives.ReadUInt32BigEndian(data[20..24]);
        return true;
    }

    // JPEG: walks the segments up to the first start-of-frame marker (SOF0-SOF15 except DHT, JPG and DAC); none before the scan
    // means the size cannot be read.
    private static bool TryReadJpegSize(ReadOnlySpan<byte> data, out long width, out long height)
    {
        width = height = 0;
        var position = 2;
        while (position + 4 <= data.Length && data[0] == 0xff && data[1] == 0xd8)
        {
            if (data[position] != 0xff) return false;
            var marker = data[position + 1];
            if (marker == 0xff) { position++; continue; }
            if (marker is 0x01 or (>= 0xd0 and <= 0xd8)) { position += 2; continue; }
            if (marker is 0xd9 or 0xda) return false;
            if (marker is >= 0xc0 and <= 0xcf and not (0xc4 or 0xc8 or 0xcc))
            {
                if (position + 9 > data.Length) return false;
                height = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 5)..]);
                width = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 7)..]);
                return true;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(position + 2)..]);
            if (length < 2) return false;
            position += 2 + length;
        }
        return false;
    }

    // WebP: the first chunk is VP8 (lossy), VP8L (lossless) or VP8X (extended), each with its own size layout.
    private static bool TryReadWebpSize(ReadOnlySpan<byte> data, out long width, out long height)
    {
        width = height = 0;
        if (data.Length < 30 || !Domain.Common.FileSignaturePolicy.Matches(Domain.Common.FileMediaTypes.Webp, data)) return false;
        var chunk = data[12..16];
        if (chunk.SequenceEqual("VP8 "u8))
        {
            if (data[23] != 0x9d || data[24] != 0x01 || data[25] != 0x2a) return false;
            width = BinaryPrimitives.ReadUInt16LittleEndian(data[26..28]) & 0x3fff;
            height = BinaryPrimitives.ReadUInt16LittleEndian(data[28..30]) & 0x3fff;
            return true;
        }
        if (chunk.SequenceEqual("VP8L"u8))
        {
            if (data[20] != 0x2f) return false;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(data[21..25]);
            width = (bits & 0x3fff) + 1;
            height = ((bits >> 14) & 0x3fff) + 1;
            return true;
        }
        if (!chunk.SequenceEqual("VP8X"u8)) return false;
        width = 1 + (data[24] | (data[25] << 8) | (data[26] << 16));
        height = 1 + (data[27] | (data[28] << 8) | (data[29] << 16));
        return true;
    }
}
