using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Validation;
using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Forms;

/// <summary>
/// El alta marca los ids locales repetidos de una pasada: un pedido con más opciones que el tope llega rápido al servicio, que
/// responde <c>TooManyItems</c> (409).
/// </summary>
public sealed class CreateFormCatalogRequestValidatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact(Timeout = 30_000)]
    public async Task Many_items_are_checked_in_one_pass_and_only_the_repeated_local_ids_are_marked()
    {
        var items = Enumerable.Range(0, 100_000).Select(index => new FormCatalogItemInput(null, $"n{index}", $"C{index}", "Texto", null, null, true))
            .Append(new FormCatalogItemInput(null, "n3", "R", "Repetida", null, null, true)).ToList();

        var failure = await Task.Run(() => ValidateAsync(Request(items)), TestContext.Current.CancellationToken);

        Assert.NotNull(failure);
        Assert.Equal(["items.100000", "items.3"], failure.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([FormCatalogInputRules.Repeated], failure.Errors["items.3"]);
    }

    private static CreateFormCatalogRequest Request(IReadOnlyList<FormCatalogItemInput> items) =>
        new("Talles", null, FormCatalogSource.Manual, null, items, null, null, null);

    private static Task<Domain.Results.ValidationError?> ValidateAsync(CreateFormCatalogRequest request) =>
        RequestValidator.ValidateAsync(request, [new CreateFormCatalogRequestValidator()], Ct);
}
