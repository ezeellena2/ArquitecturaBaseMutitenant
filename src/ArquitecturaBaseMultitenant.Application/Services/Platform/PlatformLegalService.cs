using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Plataforma › Documentos legales. Lista las versiones y muestra una; publicar crea una versión nueva e inmutable
/// (nunca se edita ni se borra otra), numerada por tipo bajo un lock global, con el texto en cada cultura habilitada.
/// La vigencia es el comienzo de la fecha elegida en la zona del operador (hoy, desde ahora) y no puede ser anterior a hoy. El tablero no
/// pide motivo: el evento usa uno fijo de los recursos. El texto nunca va a logs ni a la auditoría.
/// </summary>
internal sealed class PlatformLegalService(
    ILegalRepository legal,
    ILegalReader reader,
    ICultureCatalog cultures,
    IUserRepository users,
    ITimeZoneService timeZones,
    PlatformAudit audit,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<PlatformLegalService> logger) : IPlatformLegalService
{
    public Task<Result<IReadOnlyList<PlatformLegalDocumentGroupResponse>>> ListAsync(CancellationToken ct) =>
        OperationLog.RunAsync<IReadOnlyList<PlatformLegalDocumentGroupResponse>>(logger, clock, "ListPlatformLegalDocuments", async () =>
        {
            var versions = await reader.ListVersionsAsync(ct);
            var nowUtc = clock.GetUtcNow().UtcDateTime;
            return Result.Success<IReadOnlyList<PlatformLegalDocumentGroupResponse>>([.. Enum.GetValues<LegalDocumentKind>()
                .Select(kind =>
                {
                    var ofKind = versions.Where(version => version.Kind == kind).ToArray();
                    var current = Current(ofKind, nowUtc);
                    return new PlatformLegalDocumentGroupResponse(kind, current?.Version, current?.EffectiveAtUtc,
                        [.. ofKind.OrderByDescending(version => version.Version).Select(version =>
                            new PlatformLegalVersionResponse(version.Id, version.Version, version.EffectiveAtUtc,
                                version.Id == current?.Id))]);
                })]);
        });

    public Task<Result<PlatformLegalDocumentResponse>> FindAsync(Guid id, CancellationToken ct) =>
        OperationLog.RunAsync<PlatformLegalDocumentResponse>(logger, clock, "FindPlatformLegalDocument", async () =>
        {
            var versions = await reader.ListVersionsAsync(ct);
            if (versions.FirstOrDefault(version => version.Id == id) is not { } document) return LegalErrors.DocumentNotFound;
            var current = Current([.. versions.Where(version => version.Kind == document.Kind)], clock.GetUtcNow().UtcDateTime);
            return new PlatformLegalDocumentResponse(document.Id, document.Kind, document.Version, document.EffectiveAtUtc,
                document.Id == current?.Id, await reader.ListContentsAsync(id, ct));
        });

    public Task<Result<PublishedLegalDocumentResponse>> PublishAsync(PublishLegalDocumentRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<PublishedLegalDocumentResponse>(logger, clock, "PublishLegalDocument", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, ct) is { } invalid) return invalid;
            var supported = await SupportedCultures.LoadAsync(cultures, ct);
            var given = request.Contents!.Select(content => content.Culture!).ToArray();
            if (given.Length != supported.Codes.Count || given.Distinct(StringComparer.OrdinalIgnoreCase).Count() != given.Length
                || !supported.Codes.All(code => given.Contains(code, StringComparer.OrdinalIgnoreCase)))
                return FieldErrors.On(LegalPublicationErrors.CulturesMismatch, "contents");
            var zone = (await users.GetByIdAsync(audit.CurrentOperatorId(), ct))?.TimeZoneId ?? TimeZoneInfo.Utc.Id;
            var nowUtc = clock.GetUtcNow().UtcDateTime;
            var effectiveOn = request.EffectiveOn!.Value;
            var today = DateOnly.FromDateTime(timeZones.ConvertToLocal(nowUtc, zone));
            if (effectiveOn < today) return FieldErrors.On(LegalPublicationErrors.EffectiveDatePast, "effectiveOn");
            // Hoy rige desde ahora: el comienzo del día podría quedar antes que la versión vigente y no regir nunca.
            var kind = request.Kind!.Value;

            return await unitOfWork.ExecuteInTransactionAsync<Result<PublishedLegalDocumentResponse>>(async work =>
            {
                await legal.LockKindAsync(kind, work);
                // El reloj se lee con el lock tomado: dos publicaciones de hoy no quedan con vigencias invertidas.
                var effectiveAtUtc = effectiveOn == today ? clock.GetUtcNow().UtcDateTime
                    : timeZones.GetDayRangeUtc(effectiveOn, zone).StartUtc;
                var latest = await legal.GetLatestVersionAsync(kind, work);
                var document = LegalDocument.Create(kind, latest + 1, effectiveAtUtc);
                legal.AddDocument(document);
                foreach (var content in request.Contents!)
                    legal.AddContent(LegalDocumentContent.Create(document.Id,
                        supported.Codes.First(code => string.Equals(code, content.Culture, StringComparison.OrdinalIgnoreCase)),
                        content.Text!));
                var changes = new SecurityEventChanges();
                changes.AddEnum<LegalDocumentKind>("kind", null, kind);
                changes.AddCount("version", latest == 0 ? null : latest, document.Version);
                changes.AddUtc("effectiveAtUtc", null, effectiveAtUtc);
                audit.RecordLegalPublication(AuditTexts.LegalPublicationReason(), changes);
                return new PublishedLegalDocumentResponse(document.Id, document.Version);
            }, CommitPolicy.OnSuccess, ct);
        });

    /// <summary>La vigente: la de vigencia más reciente ya alcanzada y, a igual vigencia, el número mayor.</summary>
    private static LegalVersionRow? Current(IReadOnlyCollection<LegalVersionRow> versions, DateTime nowUtc) =>
        versions.Where(version => version.EffectiveAtUtc <= nowUtc)
            .OrderByDescending(version => version.EffectiveAtUtc).ThenByDescending(version => version.Version)
            .FirstOrDefault();
}
