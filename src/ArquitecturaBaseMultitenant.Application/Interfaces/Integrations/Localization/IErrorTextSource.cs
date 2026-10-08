using System.Globalization;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
/// <summary>Un módulo aporta sus errores localizados sin que Api o el núcleo conozcan sus resources.</summary>
public interface IErrorTextSource
{
    string? Find(string code, CultureInfo culture);
}
