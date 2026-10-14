using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Decide qué pasa con cada teléfono de «Cargar teléfonos», sin guardar (ADR 0038, precisión del 11/10). Un miembro activo
/// con ese teléfono verificado ya queda vinculado; uno inactivo no se carga. Los demás números se invitan si son celulares
/// de un país habilitado: un número con cuenta que no es miembro se ve igual que uno sin cuenta. Las filas cuentan desde 1,
/// en el orden en que llegan; un número repetido se carga una sola vez.
/// </summary>
internal sealed class WhatsAppPhoneImportResolver(IPhoneNumberParser phones, IWhatsAppChannelSettings settings,
    IActiveMemberPhoneReader members)
{
    internal bool CanInvite => settings.IsEnabled;

    internal async Task<IReadOnlyList<Row>> ResolveAsync(IReadOnlyList<PhoneInputRequest> inputs, CancellationToken ct)
    {
        var parsed = inputs.Select(input => phones.Parse(input.Country, input.Number, PhoneUsage.Mobile)).ToArray();
        var valid = parsed.Where(result => result.IsSuccess).Select(result => result.Value).DistinctBy(phone => phone.Value).ToArray();
        var known = (await members.ListMembersAsync(valid, ct)).ToDictionary(member => member.Phone, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<Row>(parsed.Length);
        for (var index = 0; index < parsed.Length; index++)
        {
            var number = index + 1;
            if (parsed[index].IsFailure)
            {
                rows.Add(new Row(null, new(number, null, null, WhatsAppPhoneImportOutcome.Invalid)));
                continue;
            }
            var phone = parsed[index].Value;
            var outcome = !seen.Add(phone.Value) ? WhatsAppPhoneImportOutcome.Invalid
                : known.TryGetValue(phone.Value, out var member)
                    ? member.IsActive ? WhatsAppPhoneImportOutcome.AlreadyMember : WhatsAppPhoneImportOutcome.Invalid
                : settings.CountryOf(phone.Value) is { } country && settings.Countries.Contains(country, StringComparer.Ordinal)
                    ? WhatsAppPhoneImportOutcome.Invite : WhatsAppPhoneImportOutcome.Invalid;
            var name = outcome == WhatsAppPhoneImportOutcome.Invite ? null : known.GetValueOrDefault(phone.Value)?.DisplayName;
            rows.Add(new Row(phone, new(number, phone.Value, name, outcome)));
        }
        return rows;
    }

    internal sealed record Row(PhoneNumber? Phone, WhatsAppPhoneImportRowResponse Response);
}
