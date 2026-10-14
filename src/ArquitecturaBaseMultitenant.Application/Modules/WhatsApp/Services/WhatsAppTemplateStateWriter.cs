using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Escribe lo que Meta dice de las plantillas de una cuenta de WhatsApp, dentro de la transacción del llamador y bajo el lock
/// de la cuenta: al crear una, por los webhooks de estado y categoría, y al sincronizar (que además quita las que Meta ya no
/// tiene). Solo cuentas de números vigentes.
/// </summary>
internal sealed class WhatsAppTemplateStateWriter(IWhatsAppTemplateStateRepository states, IWhatsAppChannelReader channels, TimeProvider clock)
{
    internal async Task ApplyAsync(string wabaId, IReadOnlyCollection<WhatsAppMetaTemplate> updates, bool replaceAll, CancellationToken ct)
    {
        if (!await channels.HasActiveAccountAsync(wabaId, ct)) return;
        await states.LockAsync(wabaId, ct);
        var current = (await states.ListAsync(wabaId, ct)).ToList();
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var seen = new HashSet<(string, string)>();
        foreach (var update in updates)
        {
            var language = WhatsAppTemplateBase.ToMetaLanguage(update.MetaLanguage);
            seen.Add((update.MetaName, language));
            var state = current.Find(value => value.MetaName == update.MetaName && value.MetaLanguage == language);
            if (update.Deleted)
            {
                if (state is not null) { states.Remove(state); current.Remove(state); }
                continue;
            }
            if (state is null)
            {
                state = WhatsAppTemplateState.Create(wabaId, update.MetaName, language, update.Category ?? WhatsAppTemplateCategory.Utility, nowUtc);
                states.Add(state); current.Add(state);
            }
            state.Apply(update.MetaTemplateId, update.Status, update.Category, update.RejectionReason, nowUtc);
        }
        if (replaceAll)
            foreach (var stale in current.Where(value => !seen.Contains((value.MetaName, value.MetaLanguage))).ToArray()) states.Remove(stale);
    }

    /// <summary>Quita el nombre de esas cuentas, en todos sus idiomas: la plantilla se borró.</summary>
    internal async Task RemoveAsync(IReadOnlyCollection<string> wabaIds, string metaName, CancellationToken ct)
    {
        foreach (var wabaId in wabaIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            await states.LockAsync(wabaId, ct);
            foreach (var state in (await states.ListAsync(wabaId, ct)).Where(value => value.MetaName == metaName)) states.Remove(state);
        }
    }
}
