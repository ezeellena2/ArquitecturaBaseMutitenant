using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>Errores de un catálogo y sus opciones (E12, §5); los textos están en FormsTexts.</summary>
public static class FormCatalogErrors
{
    public const string NotFoundCode = "Forms.Catalog.NotFound";
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "Catalog not found.");
    public const string ItemNotFoundCode = "Forms.Catalog.ItemNotFound";
    /// <summary>La opción no es de ese catálogo, en <c>deleteItems[i]</c> o <c>moves[i]</c>.</summary>
    public static Error ItemNotFound(string field) => Field(ItemNotFoundCode, "That option isn't in this catalog.", field, ErrorType.NotFound);
    public const string ItemInUseCode = "Forms.Catalog.ItemInUse";
    /// <summary>Borrar una opción que algo usa (129): no se borra ninguna, y <c>uses</c> dice qué la usa, sin repetir.</summary>
    public static Error ItemInUse(int index, IReadOnlyList<string> uses) =>
        Field(ItemInUseCode, "It can't be deleted because it's in use.", $"deleteItems[{index}]", ErrorType.Conflict)
            with { Metadata = new Dictionary<string, object?> { ["uses"] = uses } };
    public const string DuplicateCodeCode = "Forms.Catalog.DuplicateCode";
    /// <summary>El código ya existe en el catálogo: <c>n</c> es el número de la opción que ya lo tiene.</summary>
    public static Error DuplicateCode(int index, int optionNumber) =>
        Field(DuplicateCodeCode, "Another option already has that code.", $"items[{index}].code", ErrorType.Validation)
            with { Metadata = new Dictionary<string, object?> { ["n"] = optionNumber } };
    public const string TooManyItemsCode = "Forms.Catalog.TooManyItems";
    public static readonly Error TooManyItems = Error.Conflict(TooManyItemsCode, "The catalog reached its maximum number of options.");
    public const string ValueRequiredCode = "Forms.Catalog.ValueRequired";
    public static Error ValueRequired(int index) => Field(ValueRequiredCode, "Enter the value.", $"items[{index}].value", ErrorType.Validation);
    public const string ValueInvalidCode = "Forms.Catalog.ValueInvalid";
    /// <summary>El valor no es del tipo del catálogo; el mensaje del campo dice qué escribir según el tipo.</summary>
    public static Error ValueInvalid(int index, FormCatalogValueType valueType) =>
        new ValidationError(ValueInvalidCode, "The value doesn't match the catalog's type.",
            new Dictionary<string, string[]> { [$"items[{index}].value"] = [ValueInvalidCode + "." + valueType] }, ErrorType.Validation);
    public const string MoveOutOfRangeCode = "Forms.Catalog.MoveOutOfRange";
    public static readonly Error MoveOutOfRange = Error.Conflict(MoveOutOfRangeCode, "It can't move any further.");
    public const string InactiveCode = "Forms.Catalog.Inactive";
    public static readonly Error Inactive = Error.Conflict(InactiveCode, "The catalog is deactivated.");
    public const string WrongSourceCode = "Forms.Catalog.WrongSource";
    public static readonly Error WrongSource = Error.Conflict(WrongSourceCode, "That doesn't match this catalog's source.");
    public const string SampleRequiredCode = "Forms.Catalog.SampleRequired";
    public static readonly Error SampleRequired = Field(SampleRequiredCode, "Choose a person.", "sampleMemberId", ErrorType.Validation);
    public const string FiltersInvalidCode = "Forms.Catalog.FiltersInvalid";
    public static Error FiltersInvalid(int index) => Field(FiltersInvalidCode, "Check this condition.", $"conditions[{index}]", ErrorType.Validation);
    public const string SampleMemberNotFoundCode = "Forms.Catalog.SampleMemberNotFound";
    public static readonly Error SampleMemberNotFound = Field(SampleMemberNotFoundCode, "That person is no longer in the organization.", "sampleMemberId", ErrorType.Validation);
    public const string SampleContactNotFoundCode = "Forms.Catalog.SampleContactNotFound";
    public static readonly Error SampleContactNotFound = Field(SampleContactNotFoundCode, "That customer is no longer in Customers.", "sampleContactId", ErrorType.Validation);
    public const string ItemTextTooLongCode = "Forms.Catalog.ItemTextTooLong";
    public static Error ItemTextTooLong(int index) => Field(ItemTextTooLongCode, "The text is too long.", $"items[{index}].text", ErrorType.Validation);
    public const string ItemDetailTooLongCode = "Forms.Catalog.ItemDetailTooLong";
    public static Error ItemDetailTooLong(int index) => Field(ItemDetailTooLongCode, "The detail is too long.", $"items[{index}].detail", ErrorType.Validation);

    private static ValidationError Field(string code, string description, string field, ErrorType type) =>
        new(code, description, new Dictionary<string, string[]> { [field] = [code] }, type);
}
