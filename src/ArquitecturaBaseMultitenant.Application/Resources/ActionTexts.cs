using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;

namespace ArquitecturaBaseMultitenant.Application.Resources;

/// <summary>
/// Traduce el nombre, la descripción y los campos de cada acción declarada del núcleo (<c>Action.&lt;id&gt;@&lt;versión&gt;.*</c>)
/// según la cultura del pedido. Las acciones de un módulo leen los suyos de sus recursos.
/// </summary>
public static class ActionTexts
{
    internal static ResourceManager ResourceManager { get; } =
        new("ArquitecturaBaseMultitenant.Application.Resources.Actions", typeof(ActionTexts).Assembly);

    public static string NameKey(AssistantActionDefinition action) => $"Action.{action.Key}.Name";

    public static string DescriptionKey(AssistantActionDefinition action) => $"Action.{action.Key}.Description";

    public static string FieldKey(AssistantActionDefinition action, string field) => $"Action.{action.Key}.Field.{field}";

    /// <summary>Una opción fija de un campo, como los alcances de un rol: <c>Action.&lt;id&gt;@&lt;versión&gt;.Field.&lt;campo&gt;.&lt;opción&gt;</c>.</summary>
    public static string OptionKey(AssistantActionDefinition action, string field, string option) => $"{FieldKey(action, field)}.{option}";

    public static string Name(AssistantActionDefinition action) => Get(NameKey(action));

    public static string Description(AssistantActionDefinition action) => Get(DescriptionKey(action));

    public static string Field(AssistantActionDefinition action, string field) => Get(FieldKey(action, field));

    public static string Option(AssistantActionDefinition action, string field, string option) =>
        OptionIn(action, field, option, CultureInfo.CurrentUICulture);

    public static string OptionIn(AssistantActionDefinition action, string field, string option, CultureInfo culture) =>
        ResourceManager.GetString(OptionKey(action, field, option), culture) ?? option;

    private static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
