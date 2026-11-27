namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>Una opción que se parece a una búsqueda: con su código y su texto, que son los nombres con que se la elige.</summary>
public sealed record FormCatalogItemCandidateRow(string Search, Guid Id, string Code, string Text)
{
    public override string ToString() => nameof(FormCatalogItemCandidateRow);
}
