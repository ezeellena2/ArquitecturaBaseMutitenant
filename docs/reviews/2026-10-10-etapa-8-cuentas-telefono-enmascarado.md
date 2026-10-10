# E8 N2 · Teléfono principal enmascarado en Cuentas

Correctivo de Cuentas de Plataforma sobre `df462de9d19b2e415590518f632105e2b5abbcff`, después de N1. HEAD documental benigno al verificar: `33634ecfe16b8759c33b6a2399bab088349bf5fd`. La autorización humana de máscara y el acuerdo técnico Back/Front están en `.local/reviews/e8b-contract/e8-primary-phone-mask-contract-proposal.md` y en el plan E8, commit `c5c563d1`. El handoff combinado N2/N3 se divide: este informe corresponde únicamente a N2.

## Cambios al contrato

`PlatformAccountResponse` y `PlatformAccountDetailResponse` incorporan `maskedPrimaryPhone: string | null`. Conserva la convención nullable vigente: opcional en OpenAPI y null explícito en JSON cuando no corresponde. Ejemplo: `•••• 4521`. Se documenta únicamente la propiedad nueva en el contribuidor de identidad; no cambia el transformer general.

La comparación recursiva del documento entero contra BASE tiene exactamente dos adiciones, ambas bajo `components.schemas.<Response>.properties.maskedPrimaryPhone`, con su descripción y ejemplo. No cambian rutas, permisos, cuerpos de entrada, required ni otros schemas. `MeResponse` es idéntico; `ProfileSnapshotBuilder` no se modifica. No hay migración ni cambio en Front.

- OpenAPI BASE SHA256: `fb006c3a326231e09676970a075419cd68969877372b1af4b4352f6ce66da077`.
- OpenAPI N2 SHA256: `9c0cdd86173b8fe2723259680262c4c6cf607ee0ba721c66cf585d89bcecc49f`.

## Semántica y decisiones tomadas

La fuente es el método de la identidad global: `Type=Phone`, `IsPrimary=true` y `VerifiedAtUtc` presente. `PersonalDataMask.Phone` aplica la máscara en Application y conserva sólo los últimos cuatro dígitos. El texto es un string de presentación; nunca se convierte en un `PhoneNumber` ni se formatea como E.164.

Phone principal sin nombre ni correo, o con Email de respaldo, muestra la máscara. Email/Google principal con Phone de respaldo, Phone pendiente, ausencia de método y cuenta eliminada devuelven null. Una copia histórica de `ApplicationUser.PhoneNumber` no se usa. La verificación y el principal determinan identidad; la disponibilidad del canal y la admisión de altas por país determinan otras capacidades y no la ocultan.

El listado agrega una subconsulta correlacionada a la proyección paginada de `PlatformAccountReader`, sin consultas de métodos por cada fila. La row técnica conserva `ToString()` sin contactos; el DTO HTTP sólo recibe la máscara. El detalle reutiliza la lectura existente de métodos. No se agregan dependencias, recorridos por organizaciones ni `ITenantScope`. Búsqueda y orden conservan exactamente su conducta previa.

## Autorización y privacidad

Las rutas actuales `GET /api/platform/accounts/paged` y `GET /api/platform/accounts/{id}` conservan acceso Plataforma y `platform.accounts.read`; no se agrega permiso ni ruta. Las pruebas verifican ausencia de E.164 en los cuerpos completos del listado y detalle, incluyendo los métodos de la ficha. El campo nuevo no se propaga a respuestas públicas ni a otras fichas de Plataforma.

## Validación

Comandos originales, stdout y exit están en `.local/reviews/e8b-contract/e8-accounts-primary-phone-mask-*`. El inventario ligero está en `e8-accounts-primary-phone-mask-files.json`.

| Comprobación | Resultado real |
|---|---|
| RED HTTP/PostgreSQL, clase nueva | 10 ejecutados, 10 errores por campo ausente, 0 omitidos; exit 2 |
| Build de API, capas y proyecto de integración | 0 advertencias, 0 errores; exit 0 |
| Primer GREEN | 10 ejecutados, 8 correctos; dos fallos de preparación del test, corregidos: sello de Identity y nonce base64url |
| Build final de tests sobre capas ya compiladas | 0 advertencias, 0 errores; exit 0 |
| GREEN final HTTP/PostgreSQL | 13 ejecutados, 13 correctos, 0 errores, 0 omitidos; exit 0 |
| Comparación completa OpenAPI | Sólo las dos propiedades nuevas; Me idéntico; exit 0 |

Los 10 casos nuevos cubren Phone sin nombre/correo, Phone principal con Email de respaldo, Email y Google principales con Phone secundario, pendiente histórico marcado principal, eliminada con método histórico y copia de Identity sin método. También cubren canal disponible con altas cerradas por país; relecturas después de cambiar principal en ambos sentidos y retirar Phone; recuperación pública de Phone a Email con código real del pickup y aprobación HTTP del operador.

Los tres casos existentes pertinentes verifican 401/403 y permiso de lectura, filtros/conteos/paginado/búsqueda de Cuentas y coincidencia del contrato versionado con la API real. PostgreSQL se ejecutó en Docker con las migraciones productivas. El runner dentro del sandbox no accedió al named pipe y ejecutó cero tests; las pasadas RED/GREEN válidas usaron la ejecución autorizada fuera del sandbox. El exportador OpenAPI registró una limitación de acceso al key ring del host dentro del sandbox, pero terminó correctamente; la comprobación HTTP del contrato también pasó.

## Limitaciones y continuación

Las capacidades Phone sintéticas del test implementan únicamente el puerto neutral del núcleo y no envían mensajes; esta evidencia verifica identidad/privacidad HTTP y persistencia real, sin afirmar entrega Meta. No se ejecutaron suites completas, E2E de Front, revisiones independientes ni puerta final de E8: corresponden al cierre de etapa.

N3 (`maskedPrimaryPhone` de la propia cuenta en `/api/me`) queda para el corte posterior. Root publica N2 y coordina inmediatamente el consumo de Front. Esta entrega no hace stage, commit ni push y conserva el cambio ajeno de `Role.cs`.
