using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Domain.Auditing;

/// <summary>Diff de valores técnicos; no acepta texto libre ni identificadores personales.</summary>
public sealed class SecurityEventChanges
{
    private readonly Dictionary<string, object> changes = [];

    public void AddBoolean(string key, bool? before, bool? after) => Add(key, before, after);
    public void AddCount(string key, int? before, int? after) => Add(key, before, after);
    public void AddEnum<T>(string key, T before, T after) where T : struct, Enum =>
        AddEnum<T>(key, before, (T?)after);
    public void AddEnum<T>(string key, T? before, T? after) where T : struct, Enum =>
        Add(key, before?.ToString(), after?.ToString());

    public void AddUtc(string key, DateTime? before, DateTime? after)
    {
        if (before is { Kind: not DateTimeKind.Utc } || after is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("Change instants must be UTC.");
        Add(key, before, after);
    }

    public string ToJson() => JsonSerializer.Serialize(changes);

    private void Add<T>(string key, T before, T after)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!EqualityComparer<T>.Default.Equals(before, after)) changes.Add(key, new { before, after });
    }
}
