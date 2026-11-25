using System.Globalization;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
/// <summary>Un módulo aporta los textos de auditoría que lo nombran sin que el núcleo conozca sus resources.</summary>
public interface IAuditTextSource
{
    string? Find(string key, CultureInfo culture);
}
