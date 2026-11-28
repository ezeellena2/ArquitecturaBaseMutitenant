using System.IO.Compression;
using System.Runtime.InteropServices;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// La cota de un PDF, leída de sus bytes crudos sin abrirlo con PDFsharp (KAN-69). Abrirlo inflaba enteros el stream de
/// referencias cruzadas y los de objetos, reservaba por cada /Length, /N o /Size que declara el archivo, seguía /Prev sin ver
/// ciclos y anidaba con recursión: un PDF de pocos KB agotaba la memoria o la pila de la copia. Acá se recorre el archivo una
/// vez, sin recursión y sin reservar ni seguir ningún número del archivo: cada stream cuenta lo que ocupa decodificado (Flate y
/// RunLength, contados por bloques hasta lo que queda del tope) y nunca menos que sus bytes; las páginas son los objetos
/// /Type /Page distintos, también dentro de un stream de objetos, o el /Count mayor del árbol; y un /Encrypt lo deja afuera.
/// </summary>
internal static partial class AssistantTurnContextGuard
{
    // PDFsharp y los visores buscan la cabecera en el primer kilobyte.
    private const int PdfHeaderWindow = 1024;

    private static bool TryAccountPdf(ReadOnlyMemory<byte> data, ref long bytes)
    {
        if (!MemoryMarshal.TryGetArray(data, out var file)) file = new ArraySegment<byte>(data.ToArray());
        var pdf = file.AsSpan();
        if (pdf[..Math.Min(pdf.Length, PdfHeaderWindow)].IndexOf("%PDF-"u8) < 0) return false;
        var tally = new PdfTally(file, bytes);
        var lexer = new PdfLexer(pdf, 0);
        long previous = -1, last = -1, pending = -1;
        for (var token = lexer.Next(); token.Kind != PdfTokenKind.End; token = lexer.Next())
        {
            // «n g obj»: el diccionario que sigue es el del objeto n.
            if (token.Kind == PdfTokenKind.Integer) { (previous, last) = (last, token.Value); continue; }
            var objectNumber = pending;
            pending = IsKeyword(pdf, token, "obj"u8) && previous >= 0 && last >= 0 ? previous : -1;
            (previous, last) = (-1, -1);
            var accounted = token.Kind switch
            {
                PdfTokenKind.DictionaryStart => TryAccountObject(pdf, ref lexer, tally, objectNumber),
                PdfTokenKind.ArrayStart => SkipNested(pdf, ref lexer),
                PdfTokenKind.Keyword when IsKeyword(pdf, token, "stream"u8) => TryAccountStream(pdf, ref lexer, default, tally),
                _ => true,
            };
            if (!accounted) return false;
        }
        bytes = tally.Total;
        return tally.Pages > 0 && tally.Fits;
    }

    /// <summary>Un diccionario del archivo y, si le sigue, su stream.</summary>
    private static bool TryAccountObject(ReadOnlySpan<byte> pdf, ref PdfLexer lexer, PdfTally tally, long objectNumber)
    {
        var dictionary = ReadDictionary(pdf, ref lexer);
        if (dictionary.HasEncrypt || !tally.TryCount(dictionary, objectNumber)) return false;
        var save = lexer.Position;
        if (IsKeyword(pdf, lexer.Next(), "stream"u8)) return TryAccountStream(pdf, ref lexer, dictionary, tally);
        lexer.Position = save;
        return true;
    }

    /// <summary>
    /// Los datos de un stream, desde la línea que sigue a «stream» hasta su fin, contados según su filtro. Los de un stream de
    /// objetos se leen además para contar sus páginas: sin filtro o con Flate solo; con otro filtro no se pueden leer.
    /// </summary>
    private static bool TryAccountStream(ReadOnlySpan<byte> pdf, ref PdfLexer lexer, in PdfDictionary dictionary, PdfTally tally)
    {
        var start = StreamStart(pdf, lexer.Position);
        var end = StreamEnd(pdf, start, dictionary.Length);
        lexer.Position = end;
        if (end == start) return true;
        if (dictionary.Filter == PdfFilter.Unsupported) return false;
        var limit = tally.Limit;
        long decoded = dictionary.Filter switch
        {
            PdfFilter.Flate => Inflate(tally.Segment(start, end - start), tally.Buffer, limit),
            PdfFilter.RunLength => RunLengthSize(pdf[start..end], limit),
            _ => end - start,
        };
        if (!tally.Add(Math.Max(decoded, end - start))) return false;
        if (dictionary.Type != PdfType.ObjectStream) return true;
        if (dictionary.Filter == PdfFilter.None) return TryCountObjectStream(pdf[start..end], dictionary.First, tally);
        if (dictionary.Filter != PdfFilter.Flate || dictionary.FilterCount > 1 || dictionary.HasDecodeParms) return false;
        return TryCountObjectStream(tally.Buffer.AsSpan(0, (int)decoded), dictionary.First, tally);
    }

    /// <summary>
    /// Las páginas de un stream de objetos: los pares número y desplazamiento hasta /First, y el diccionario de cada objeto, en
    /// orden y sin volver a leer uno que se superpone con el anterior, así el recorrido no pasa del largo del contenido.
    /// </summary>
    private static bool TryCountObjectStream(ReadOnlySpan<byte> content, long? first, PdfTally tally)
    {
        var start = first ?? 0;
        if (start < 0 || start > content.Length) return false;
        var objects = new List<(long Offset, long Number)>();
        var header = new PdfLexer(content[..(int)start], 0);
        for (var number = header.Next(); number.Kind == PdfTokenKind.Integer; number = header.Next())
        {
            var offset = header.Next();
            if (offset.Kind != PdfTokenKind.Integer) break;
            if (offset.Value >= 0 && offset.Value < content.Length - start) objects.Add((start + offset.Value, number.Value));
        }
        objects.Sort();
        var parsedUntil = 0;
        foreach (var (offset, number) in objects)
        {
            if (offset < parsedUntil) continue;
            var lexer = new PdfLexer(content, (int)offset);
            if (lexer.Next().Kind == PdfTokenKind.DictionaryStart && !tally.TryCount(ReadDictionary(content, ref lexer), number)) return false;
            parsedUntil = lexer.Position;
        }
        return true;
    }

    /// <summary>Lee un diccionario ya abierto: lo anidado se saltea con un contador, sin recursión.</summary>
    private static PdfDictionary ReadDictionary(ReadOnlySpan<byte> pdf, ref PdfLexer lexer)
    {
        var dictionary = default(PdfDictionary);
        while (true)
        {
            var save = lexer.Position;
            var key = lexer.Next();
            switch (key.Kind)
            {
                case PdfTokenKind.End or PdfTokenKind.DictionaryEnd: return dictionary;
                case PdfTokenKind.Name: ReadEntry(pdf, ref lexer, key, ref dictionary); break;
                case PdfTokenKind.DictionaryStart or PdfTokenKind.ArrayStart: SkipNested(pdf, ref lexer); break;
                // Un diccionario sin cerrar termina donde empieza el stream o el objeto siguiente.
                case PdfTokenKind.Keyword when IsStructural(pdf, key): lexer.Position = save; return dictionary;
            }
        }
    }

    private static void ReadEntry(ReadOnlySpan<byte> pdf, ref PdfLexer lexer, PdfToken key, ref PdfDictionary dictionary)
    {
        var save = lexer.Position;
        var value = lexer.Next();
        if (value.Kind == PdfTokenKind.End || IsStructural(pdf, value)) { lexer.Position = save; return; }
        if (IsName(pdf, key, "Filter"u8)) { ReadFilters(pdf, ref lexer, value, ref dictionary); return; }
        if (IsName(pdf, key, "Type"u8)) dictionary.Type = TypeOf(pdf, value);
        else if (IsName(pdf, key, "Length"u8)) dictionary.Length = DirectInteger(pdf, ref lexer, value);
        else if (IsName(pdf, key, "Count"u8)) dictionary.Count = DirectInteger(pdf, ref lexer, value);
        else if (IsName(pdf, key, "First"u8)) dictionary.First = DirectInteger(pdf, ref lexer, value);
        else if (IsName(pdf, key, "Kids"u8)) dictionary.HasKids = true;
        else if (IsName(pdf, key, "Encrypt"u8)) dictionary.HasEncrypt = true;
        else if (IsName(pdf, key, "DecodeParms"u8)) dictionary.HasDecodeParms = !IsKeyword(pdf, value, "null"u8);
        if (value.Kind is PdfTokenKind.DictionaryStart or PdfTokenKind.ArrayStart) SkipNested(pdf, ref lexer);
    }

    /// <summary>Un nombre o un arreglo de nombres; una referencia u otra cosa no se puede medir.</summary>
    private static void ReadFilters(ReadOnlySpan<byte> pdf, ref PdfLexer lexer, PdfToken value, ref PdfDictionary dictionary)
    {
        if (value.Kind == PdfTokenKind.Name) { AddFilter(pdf, value, ref dictionary); return; }
        if (value.Kind != PdfTokenKind.ArrayStart) { dictionary.Filter = PdfFilter.Unsupported; return; }
        while (true)
        {
            var save = lexer.Position;
            var item = lexer.Next();
            if (item.Kind is PdfTokenKind.End or PdfTokenKind.ArrayEnd) return;
            if (IsStructural(pdf, item)) { lexer.Position = save; return; }
            if (item.Kind == PdfTokenKind.Name) AddFilter(pdf, item, ref dictionary);
            else
            {
                dictionary.Filter = PdfFilter.Unsupported;
                if (item.Kind is PdfTokenKind.DictionaryStart or PdfTokenKind.ArrayStart) SkipNested(pdf, ref lexer);
            }
        }
    }

    /// <summary>
    /// El primer filtro decide cómo se cuenta: Flate y RunLength decodificados, ASCII y los códecs de imagen por sus bytes. Los
    /// que siguen solo pueden ser códecs de imagen (DCT, JPX, CCITT o JBIG2), que cuentan lo que reciben. LZW, Crypt, uno
    /// desconocido u otra cadena no se miden sin decodificarlos y dejan el PDF afuera.
    /// </summary>
    private static void AddFilter(ReadOnlySpan<byte> pdf, PdfToken name, ref PdfDictionary dictionary)
    {
        var imageCodec = IsName(pdf, name, "DCTDecode"u8) || IsName(pdf, name, "DCT"u8) || IsName(pdf, name, "JPXDecode"u8)
            || IsName(pdf, name, "CCITTFaxDecode"u8) || IsName(pdf, name, "CCF"u8) || IsName(pdf, name, "JBIG2Decode"u8);
        var filter = imageCodec ? PdfFilter.Raw
            : IsName(pdf, name, "FlateDecode"u8) || IsName(pdf, name, "Fl"u8) ? PdfFilter.Flate
            : IsName(pdf, name, "RunLengthDecode"u8) || IsName(pdf, name, "RL"u8) ? PdfFilter.RunLength
            : IsName(pdf, name, "ASCIIHexDecode"u8) || IsName(pdf, name, "AHx"u8)
                || IsName(pdf, name, "ASCII85Decode"u8) || IsName(pdf, name, "A85"u8) ? PdfFilter.Raw
            : PdfFilter.Unsupported;
        var position = dictionary.FilterCount++;
        if (dictionary.Filter == PdfFilter.Unsupported) return;
        if (position == 0) dictionary.Filter = filter;
        else if (!imageCodec) dictionary.Filter = PdfFilter.Unsupported;
    }

    private static PdfType TypeOf(ReadOnlySpan<byte> pdf, PdfToken value) =>
        IsName(pdf, value, "Page"u8) ? PdfType.Page
        : IsName(pdf, value, "Pages"u8) ? PdfType.Pages
        : IsName(pdf, value, "ObjStm"u8) ? PdfType.ObjectStream
        : PdfType.None;

    /// <summary>El entero directo de un valor; null si es otra cosa o una referencia «n g R», que se consume entera.</summary>
    private static long? DirectInteger(ReadOnlySpan<byte> pdf, ref PdfLexer lexer, PdfToken value)
    {
        if (value.Kind != PdfTokenKind.Integer) return null;
        var save = lexer.Position;
        if (lexer.Next().Kind == PdfTokenKind.Integer && IsKeyword(pdf, lexer.Next(), "R"u8)) return null;
        lexer.Position = save;
        return value.Value;
    }

    /// <summary>Saltea un arreglo o un diccionario ya abierto con un contador de profundidad; siempre sigue.</summary>
    private static bool SkipNested(ReadOnlySpan<byte> pdf, ref PdfLexer lexer)
    {
        for (long depth = 1; depth > 0;)
        {
            var save = lexer.Position;
            var token = lexer.Next();
            if (token.Kind == PdfTokenKind.End) break;
            if (token.Kind is PdfTokenKind.DictionaryStart or PdfTokenKind.ArrayStart) depth++;
            else if (token.Kind is PdfTokenKind.DictionaryEnd or PdfTokenKind.ArrayEnd) depth--;
            else if (IsStructural(pdf, token)) { lexer.Position = save; break; }
        }
        return true;
    }

    /// <summary>Donde empiezan los datos: después de «stream», de los espacios que tolera PDFsharp y del fin de línea.</summary>
    private static int StreamStart(ReadOnlySpan<byte> pdf, int position)
    {
        while (position < pdf.Length && pdf[position] == ' ') position++;
        if (position < pdf.Length && pdf[position] == '\r') position++;
        if (position < pdf.Length && pdf[position] == '\n') position++;
        return position;
    }

    /// <summary>
    /// Donde terminan los datos: en su /Length si ahí viene «endstream»; si no, antes del primer «endstream» que sigue, como lo
    /// corrige PDFsharp, y sin ninguno, al final del archivo. El /Length solo se usa para ubicar, nunca para reservar.
    /// </summary>
    private static int StreamEnd(ReadOnlySpan<byte> pdf, int start, long? length)
    {
        if (length is { } declared && declared >= 0 && declared <= pdf.Length - start)
        {
            var end = start + (int)declared;
            var after = end;
            while (after < pdf.Length && IsWhitespace(pdf[after])) after++;
            if (pdf[after..].StartsWith("endstream"u8)) return end;
        }
        var found = pdf[start..].IndexOf("endstream"u8);
        if (found < 0) return pdf.Length;
        var stop = start + found;
        if (stop > start && pdf[stop - 1] == '\n') stop--;
        if (stop > start && pdf[stop - 1] == '\r') stop--;
        return stop;
    }

    /// <summary>
    /// Infla un stream Flate como PDFsharp (saltea la cabecera zlib y no mira la suma) por bloques, sin pasar de
    /// <paramref name="limit"/> más un byte, que alcanza para saber que no entra. Lo inflado queda al principio de
    /// <paramref name="output"/>.
    /// </summary>
    private static int Inflate(ArraySegment<byte> data, byte[] output, int limit)
    {
        if (data.Count <= 2) return 0;
        using var compressed = new MemoryStream(data.Array!, data.Offset + 2, data.Count - 2, writable: false);
        using var inflater = new DeflateStream(compressed, CompressionMode.Decompress);
        var total = 0;
        while (total <= limit)
        {
            var read = inflater.Read(output, total, limit + 1 - total);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    /// <summary>Lo que ocupa decodificado un RunLength, sin decodificarlo: se corta apenas pasa <paramref name="limit"/>.</summary>
    private static long RunLengthSize(ReadOnlySpan<byte> data, long limit)
    {
        long size = 0;
        for (var index = 0; index < data.Length && size <= limit;)
        {
            var run = data[index];
            if (run == 128) break;
            size += run < 128 ? run + 1 : 257 - run;
            index += run < 128 ? run + 2 : 2;
        }
        return size;
    }

    private static bool IsKeyword(ReadOnlySpan<byte> pdf, PdfToken token, ReadOnlySpan<byte> word) =>
        token.Kind == PdfTokenKind.Keyword && pdf.Slice(token.Start, token.Length).SequenceEqual(word);

    // Las palabras que cortan un diccionario o un arreglo sin cerrar, para no leer los datos de un stream como tokens.
    private static bool IsStructural(ReadOnlySpan<byte> pdf, PdfToken token) =>
        IsKeyword(pdf, token, "stream"u8) || IsKeyword(pdf, token, "endstream"u8) || IsKeyword(pdf, token, "obj"u8)
        || IsKeyword(pdf, token, "endobj"u8) || IsKeyword(pdf, token, "trailer"u8) || IsKeyword(pdf, token, "xref"u8)
        || IsKeyword(pdf, token, "startxref"u8);

    /// <summary>Compara un nombre con <paramref name="expected"/>, con sus escapes #hh decodificados.</summary>
    private static bool IsName(ReadOnlySpan<byte> pdf, PdfToken token, ReadOnlySpan<byte> expected)
    {
        if (token.Kind != PdfTokenKind.Name) return false;
        var name = pdf.Slice(token.Start, token.Length);
        var matched = 0;
        for (var index = 0; index < name.Length; index++, matched++)
        {
            var value = name[index];
            if (value == '#' && index + 2 < name.Length && byte.TryParse(name.Slice(index + 1, 2), System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out var decoded))
            {
                value = decoded;
                index += 2;
            }
            if (matched >= expected.Length || expected[matched] != value) return false;
        }
        return matched == expected.Length;
    }

    private static bool IsWhitespace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private static bool IsDelimiter(byte value) =>
        value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private enum PdfTokenKind { End, Integer, Name, String, Keyword, DictionaryStart, DictionaryEnd, ArrayStart, ArrayEnd }

    private enum PdfType { None, Page, Pages, ObjectStream }

    private enum PdfFilter { None, Flate, RunLength, Raw, Unsupported }

    /// <summary>Un token: su clase, dónde está y, si es un entero, su valor.</summary>
    private readonly record struct PdfToken(PdfTokenKind Kind, int Start, int Length, long Value);

    /// <summary>Lo que importa de un diccionario para contar el PDF.</summary>
    private struct PdfDictionary
    {
        internal PdfType Type;
        internal PdfFilter Filter;
        internal int FilterCount;
        internal long? Length;
        internal long? Count;
        internal long? First;
        internal bool HasKids;
        internal bool HasEncrypt;
        internal bool HasDecodeParms;
    }

    /// <summary>Tokens de PDF sin recursión ni reservas: strings, nombres, enteros, palabras y delimitadores.</summary>
    private ref struct PdfLexer
    {
        private readonly ReadOnlySpan<byte> _pdf;
        internal int Position;

        internal PdfLexer(ReadOnlySpan<byte> pdf, int position)
        {
            _pdf = pdf;
            Position = position;
        }

        internal PdfToken Next()
        {
            SkipSpace();
            var start = Position;
            if (start >= _pdf.Length) return new(PdfTokenKind.End, start, 0, 0);
            switch (_pdf[start])
            {
                case (byte)'(':
                    SkipLiteral();
                    return Token(PdfTokenKind.String, start);
                case (byte)'<' when At(start + 1, (byte)'<'):
                    Position += 2;
                    return Token(PdfTokenKind.DictionaryStart, start);
                case (byte)'<':
                    var close = _pdf[start..].IndexOf((byte)'>');
                    Position = close < 0 ? _pdf.Length : start + close + 1;
                    return Token(PdfTokenKind.String, start);
                case (byte)'>' when At(start + 1, (byte)'>'):
                    Position += 2;
                    return Token(PdfTokenKind.DictionaryEnd, start);
                case (byte)'[':
                    Position++;
                    return Token(PdfTokenKind.ArrayStart, start);
                case (byte)']':
                    Position++;
                    return Token(PdfTokenKind.ArrayEnd, start);
                case (byte)'/':
                    Position++;
                    SkipRegular();
                    return new(PdfTokenKind.Name, start + 1, Position - start - 1, 0);
                case (byte)'>' or (byte)')' or (byte)'{' or (byte)'}':
                    Position++;
                    return Token(PdfTokenKind.Keyword, start);
                default:
                    SkipRegular();
                    return Word(start);
            }
        }

        private readonly bool At(int index, byte value) => index < _pdf.Length && _pdf[index] == value;

        private readonly PdfToken Token(PdfTokenKind kind, int start) => new(kind, start, Position - start, 0);

        /// <summary>Un entero de hasta 18 cifras con su signo; cualquier otra palabra (un real, true, R, obj) es una palabra.</summary>
        private readonly PdfToken Word(int start)
        {
            var word = _pdf[start..Position];
            var digits = word[0] is (byte)'+' or (byte)'-' ? word[1..] : word;
            if (digits.Length is 0 or > 18 || digits.IndexOfAnyExceptInRange((byte)'0', (byte)'9') >= 0)
                return Token(PdfTokenKind.Keyword, start);
            var value = 0L;
            foreach (var digit in digits) value = (value * 10) + (digit - '0');
            return new(PdfTokenKind.Integer, start, word.Length, word[0] == '-' ? -value : value);
        }

        private void SkipSpace()
        {
            while (Position < _pdf.Length)
            {
                var current = _pdf[Position];
                if (current == '%')
                {
                    var end = _pdf[Position..].IndexOfAny((byte)'\r', (byte)'\n');
                    Position = end < 0 ? _pdf.Length : Position + end;
                }
                else if (IsWhitespace(current)) Position++;
                else return;
            }
        }

        private void SkipRegular()
        {
            while (Position < _pdf.Length && !IsWhitespace(_pdf[Position]) && !IsDelimiter(_pdf[Position])) Position++;
        }

        /// <summary>Un string entre paréntesis, con sus paréntesis anidados y sus escapes.</summary>
        private void SkipLiteral()
        {
            long depth = 0;
            while (Position < _pdf.Length)
            {
                var current = _pdf[Position++];
                if (current == '\\') Position++;
                else if (current == '(') depth++;
                else if (current == ')' && --depth == 0) break;
            }
            Position = Math.Min(Position, _pdf.Length);
        }
    }

    /// <summary>Lo contado de un PDF: lo anterior, sus streams y sus páginas, contra <see cref="AssistantTechnicalLimits.MaxInputTokens"/>.</summary>
    private sealed class PdfTally(ArraySegment<byte> file, long before)
    {
        private readonly HashSet<long> _pageObjects = [];
        private long _anonymousPages;
        private long _treeCount;
        private long _streams;
        private byte[]? _buffer;

        /// <summary>Donde se infla cada stream; alcanza para el tope entero más un byte.</summary>
        internal byte[] Buffer => _buffer ??= new byte[AssistantTechnicalLimits.MaxInputTokens + 1];

        internal long Pages => Math.Max(_pageObjects.Count + _anonymousPages, _treeCount);

        internal long Total => before + _streams + (AssistantTechnicalLimits.PdfTokensPerPage * Pages);

        internal bool Fits => Pages <= AssistantTechnicalLimits.MaxPdfPages && Total <= AssistantTechnicalLimits.MaxInputTokens;

        /// <summary>Lo que queda del tope para el próximo stream.</summary>
        internal int Limit => (int)Math.Clamp(AssistantTechnicalLimits.MaxInputTokens - Total, 0, AssistantTechnicalLimits.MaxInputTokens);

        internal ArraySegment<byte> Segment(int start, int length) => file.Slice(start, length);

        internal bool Add(long streamBytes)
        {
            _streams += streamBytes;
            return Fits;
        }

        /// <summary>
        /// Una página por número de objeto (las versiones de una actualización incremental cuentan una vez) y el /Count de un nodo
        /// del árbol; cualquiera de los dos pasado deja el PDF afuera.
        /// </summary>
        internal bool TryCount(in PdfDictionary dictionary, long objectNumber)
        {
            if (dictionary.Type == PdfType.Page)
            {
                if (objectNumber < 0) _anonymousPages++;
                else _pageObjects.Add(objectNumber);
            }
            if (dictionary.Count is { } count && (dictionary.Type == PdfType.Pages || dictionary.HasKids))
                _treeCount = Math.Max(_treeCount, Math.Min(count, AssistantTechnicalLimits.MaxPdfPages + 1L));
            return Fits;
        }
    }
}
