using ArquitecturaBaseMultitenant.Api.Modules.Forms.Controllers;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class VersionedContractTests
{
    /// <summary>
    /// Guardar un catálogo, revisar una importación y aplicarla reciben la versión que se leyó y responden la nueva: el detalle
    /// guardado y la revisión la llevan en <c>Version</c>, y aplicar responde <see cref="Application.Models.Common.VersionResponse"/>.
    /// </summary>
    [Fact]
    public void Installed_forms_saves_answer_their_new_version()
    {
        Assert.Equal(typeof(uint), VersionType(typeof(FormCatalogUpdatedResponse), "Version"));
        Assert.Equal(typeof(uint), VersionType(typeof(FormCatalogImportPreviewResponse), "Version"));
        foreach (var name in new[] { nameof(BusinessFormCatalogsController.Update), nameof(BusinessFormCatalogsController.PreviewImport),
                     nameof(BusinessFormCatalogsController.ApplyImport) })
        {
            var action = typeof(BusinessFormCatalogsController).GetMethod(name)!;
            Assert.True(IsVersionedSave(action), name);
            Assert.True(AnswersTheNewVersion(action), name);
        }
    }
}
