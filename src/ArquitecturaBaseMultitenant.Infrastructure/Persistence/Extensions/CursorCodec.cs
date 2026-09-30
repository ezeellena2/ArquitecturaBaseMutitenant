using System.Buffers.Binary;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Ubica una página por instante UTC y un ID que desempata filas con la misma fecha.</summary>
internal readonly record struct CursorPosition(DateTime SortUtc, Guid Id);

/// <summary>Codifica y valida un cursor opaco de paginación con instante UTC e ID de desempate. Rechaza versiones y representaciones no canónicas antes de reutilizarlo en una consulta.</summary>
internal static class CursorCodec
{
    private const byte Version = 1;
    private const int PayloadLength = 25;
    private const int EncodedLength = 34;

    public static string Encode(DateTime sortUtc, Guid id)
    {
        if (sortUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Cursor sort value must be UTC.", nameof(sortUtc));
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Cursor id cannot be empty.", nameof(id));
        }

        Span<byte> payload = stackalloc byte[PayloadLength];
        payload[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(payload[1..9], sortUtc.Ticks);
        if (!id.TryWriteBytes(payload[9..]))
        {
            throw new InvalidOperationException("Could not encode the cursor id.");
        }

        return Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool TryDecode(string? encoded, out CursorPosition position)
    {
        position = default;
        if (encoded is null || encoded.Length != EncodedLength || encoded.Any(character =>
                character is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
        {
            return false;
        }

        var padded = encoded.Replace('-', '+').Replace('_', '/') + "==";
        Span<byte> payload = stackalloc byte[PayloadLength];
        if (!Convert.TryFromBase64String(padded, payload, out var written) || written != PayloadLength
            || payload[0] != Version)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(payload[1..9]);
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        var id = new Guid(payload[9..]);
        if (id == Guid.Empty)
        {
            return false;
        }

        var candidate = new CursorPosition(new DateTime(ticks, DateTimeKind.Utc), id);
        if (!string.Equals(Encode(candidate.SortUtc, candidate.Id), encoded, StringComparison.Ordinal))
        {
            return false;
        }

        position = candidate;
        return true;
    }
}
