using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>Errores de pegar, importar y exportar un catálogo (E12, C7 a C11); los textos están en FormsTexts.</summary>
public static class FormCatalogImportErrors
{
    public const string FileChangedCode = "Forms.CatalogImport.FileChanged";
    public static readonly Error FileChanged = Error.Conflict(FileChangedCode, "This isn't the file you reviewed.");
    public const string FileInvalidCode = "Forms.CatalogImport.FileInvalid";
    /// <summary>El mensaje del campo <c>file</c> cuando el archivo pasa los topes (C8), con el mismo código.</summary>
    public const string FileOverLimitsMessage = FileInvalidCode + ".Limits";
    /// <summary>El formato o la codificación del archivo, o con <paramref name="overLimits"/> sus topes (C8); los dos van en <c>file</c>.</summary>
    public static Error FileInvalid(bool overLimits) => overLimits
        ? FileField(FileOverLimitsMessage, "The file is over the limits.")
        : FileField(FileInvalidCode, "The file isn't an XLSX or a UTF-8 CSV.");
    /// <summary>Un número o una fecha que se lee de dos formas (C9): va en la fila y la columna de la revisión, nunca como respuesta.</summary>
    public const string ValueAmbiguousCode = "Forms.CatalogImport.ValueAmbiguous";
    /// <summary>Falta un dato obligatorio de la fila: va en la revisión, nunca como respuesta.</summary>
    public const string CellRequiredCode = "Forms.CatalogImport.CellRequired";
    public const string ConfirmationRequiredCode = "Forms.CatalogImport.ConfirmationRequired";
    public static readonly Error ConfirmationRequired = Error.Validation(ConfirmationRequiredCode, "Confirm the deactivations or the rows with errors before applying.");
    public const string BusyCode = "Forms.CatalogImport.Busy";
    public static readonly Error Busy = Error.Conflict(BusyCode, "You already have an import or an export in progress.");
    public const string TooManyAtOnceCode = "Forms.CatalogImport.TooManyAtOnce";
    public static Error TooManyAtOnce(int retryAfter) => Error.TooManyRequests(TooManyAtOnceCode, "There are too many imports and exports in progress.",
        new Dictionary<string, object?> { ["retryAfter"] = retryAfter });

    private static ValidationError FileField(string message, string description) =>
        new(FileInvalidCode, description, new Dictionary<string, string[]> { ["file"] = [message] }, ErrorType.Validation);
}
