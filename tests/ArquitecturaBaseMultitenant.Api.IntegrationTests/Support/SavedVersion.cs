using System.Net;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Un guardado con versión responde 200 con la versión que quedó, y el front la toma como base del próximo guardado
/// (docs/rules/concurrencia.md).
/// </summary>
internal static class SavedVersion
{
    /// <summary>Exige 200 con <c>version</c> y la devuelve.</summary>
    internal static async Task<uint> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("version").GetUInt32();
    }

    /// <summary>
    /// Guarda con la versión leída y vuelve a guardar con la que respondió el primer guardado: ninguno da 409 y el
    /// primero cambia la versión. Devuelve la del segundo, que tiene que ser la que lee la ficha.
    /// </summary>
    internal static async Task<uint> SaveTwiceAsync(Func<uint, Task<HttpResponseMessage>> save, uint version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(save);
        using var first = await save(version);
        var firstVersion = await ReadAsync(first, cancellationToken);
        Assert.NotEqual(version, firstVersion);
        using var second = await save(firstVersion);
        return await ReadAsync(second, cancellationToken);
    }
}
