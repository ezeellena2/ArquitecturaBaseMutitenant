using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>Contributes one immutable demonstration revision, inside the core seed transaction.</summary>
internal sealed class AssistantLegalDocumentSeed(ApplicationDbContext context, TimeProvider clock) : IModuleDataSeedContributor
{
    private const int Version = 2;

    public async Task StageAsync(CancellationToken ct)
    {
        context.RequireTransaction();
        await context.AcquireAdvisoryLocksAsync(Enum.GetValues<LegalDocumentKind>().Select(AdvisoryLockKeys.LegalDocument), ct);
        foreach (var kind in Enum.GetValues<LegalDocumentKind>())
        {
            // A deployment's published revision (including a scheduled one) always takes precedence over the template.
            if (await context.LegalDocuments.AnyAsync(row => row.Kind == kind && row.Version >= Version, ct)) continue;
            var cultures = await context.Set<ArquitecturaBaseMultitenant.Domain.ReferenceData.Culture>().AsNoTracking()
                .Where(row => row.IsEnabled).Select(row => new { row.Code, row.LanguageCode }).ToListAsync(ct);
            // Reference rows may still be staged by the same seed on an empty database.
            foreach (var entry in context.ChangeTracker.Entries<ArquitecturaBaseMultitenant.Domain.ReferenceData.Culture>())
                if (entry.Entity.IsEnabled && cultures.All(row => row.Code != entry.Entity.Code))
                    cultures.Add(new { entry.Entity.Code, entry.Entity.LanguageCode });
            if (cultures.Count == 0) throw new InvalidOperationException("The legal seed requires an enabled culture.");
            var document = LegalDocument.Create(kind, Version, clock.GetUtcNow().UtcDateTime);
            context.Add(document);
            foreach (var culture in cultures)
                context.Add(LegalDocumentContent.Create(document.Id, culture.Code, Text(kind, culture.LanguageCode)));
        }
    }

    private static string Text(LegalDocumentKind kind, string language) => (kind, language) switch
    {
        (LegalDocumentKind.Terms, "es") => """
            DOCUMENTO DE DEMOSTRACIÓN. Esta plantilla no incluye términos legales definitivos. Reemplazá este texto por una nueva versión antes de publicar.

            El Asistente usa Anthropic para procesar tus consultas y los adjuntos que enviás. Las respuestas pueden contener errores: revisalas antes de usarlas. El aviso de inteligencia artificial requiere que elijas Entendido de forma explícita; registrarte, ingresar o aceptar estos documentos no acepta ese aviso.

            La función prevista conserva las conversaciones por 30 días sin actividad, con un plazo configurable por la plataforma. Las Guardadas no vencen por inactividad; borrarlas o completar la baja de la cuenta elimina también las guardadas y sus adjuntos. La exportación de una conversación en Markdown o PDF se incorporará con esa función. La exportación general de datos personales se implementará en la Etapa 10. Estas funciones pendientes deben verificarse antes de habilitar el servicio al público.
            """,
        (LegalDocumentKind.Privacy, "es") => """
            DOCUMENTO DE DEMOSTRACIÓN. Esta plantilla no incluye una política de privacidad definitiva. Publicá una nueva versión adaptada a tu negocio antes de usarla.

            El proveedor de inteligencia artificial es Anthropic. Cuando consultás al Asistente, se procesan el texto de la consulta, el contexto autorizado de la conversación y los adjuntos que enviás para generar una respuesta. Evitá adjuntar información personal innecesaria. Las conversaciones son privadas de su autor; el acceso Personal y el de Empresa mantienen sus contenidos separados.

            La retención prevista es de 30 días sin actividad, configurable por la plataforma. Leer una conversación no renueva el plazo. Las Guardadas se conservan sin vencimiento por inactividad, pero el borrado explícito y la baja completada de la cuenta eliminan conversaciones y adjuntos, incluidas las guardadas. Sólo se conservarán agregados de uso y costo anonimizados, sin texto ni vínculo personal, cuando sean necesarios. La exportación de conversación en Markdown o PDF y la futura exportación general de datos personales (Etapa 10) son entregas pendientes. Antes de publicar, verificá la implementación de retención, borrado y exportación y describí los plazos y condiciones reales del proveedor.
            """,
        (LegalDocumentKind.Terms, "en") => """
            DEMONSTRATION DOCUMENT. This template does not include final legal terms. Replace this text by publishing a new revision before launching.

            The Assistant uses Anthropic to process your queries and the attachments you send. Responses may contain errors: review them before using them. The artificial intelligence notice requires an explicit Understood action; registering, signing in or accepting these documents does not accept that notice.

            The planned feature retains conversations for 30 days without activity, with a period configurable by the platform. Saved conversations do not expire through inactivity; explicit deletion or completed account deletion also removes saved conversations and their attachments. Conversation export in Markdown or PDF will be delivered with that feature. General personal data export will be implemented in Stage 10. These pending features must be verified before making the service publicly available.
            """,
        (LegalDocumentKind.Privacy, "en") => """
            DEMONSTRATION DOCUMENT. This template does not include a final privacy policy. Publish a new revision tailored to your business before using it.

            The artificial intelligence provider is Anthropic. When you query the Assistant, your query text, the authorized conversation context and the attachments you send are processed to generate a response. Avoid attaching unnecessary personal information. Conversations are private to their author; Personal and Business access keep their contents separate.

            Planned retention is 30 days without activity, configurable by the platform. Reading a conversation does not renew this period. Saved conversations have no inactivity expiry, but explicit deletion and completed account deletion remove conversations and attachments, including saved ones. Only anonymized usage and cost aggregates, without text or personal links, will be retained when needed. Conversation export in Markdown or PDF and future general personal data export (Stage 10) are pending deliveries. Before launching, verify retention, deletion and export implementation and describe the provider's actual periods and conditions.
            """,
        _ => throw new InvalidOperationException($"No demonstration assistant legal text for language '{language}'."),
    };
}
