# Correcciones de la revisión de E5

E5 sigue abierta. Este informe registra la revisión solicitada sobre lo implementado hasta `2e5a06b`; no reemplaza la puerta de cierre ni aprueba capturas.

| Punto | Resultado | Commit |
|---|---|---|
| 1 · Avisos | Solo rechazo y moderación. Dueños con membresía e identidad activas; principal seguro o alternativas del resolver, sin correo administrado ajeno ni registrante retirado. El contrato no admite el motivo interno. | `02184ca` |
| 2 · Versión | Contadores en proyección global 1:1; los triggers no modifican Tenants ni su xmin. Alta de un miembro entre lectura y Suspender no da 409; una edición real de estado sí invalida la versión anterior. Migración conserva los totales. | `0250037` |
| 3 · Ficha | T21 define detalle con Dueños desde índice/identidad, último estado desde SecurityEvents e historial global filtrado con cursor. Permisos, privacidad y pruebas especificados; implementación y contrato pendientes en T21. | commit de este informe y plan |
| 4 · Auditoría | Eventos existentes de configuración, módulos, estados y moderación guardan diff técnico, destino e IP enmascarada. Migración y pruebas verificadas. La lectura de Ingresos desde LoginAudits sigue pendiente en T17. | `7e56aba` |
| 5 · Retry | HTTP recibe solo version y el servicio asigna el motivo fijo traducido. Test real y contrato back/front verdes. | back `ad8c74a`, front `70a6bdc` |
| 6 · E2E operador | Ingreso real y pasos de pantalla preparados; términos reales aceptados en preparador. Rojo por listado Organizaciones ausente. Falta operador reducido después de R00; no está completo. | back `0a36f5f`, front `a0f094c` (incluido por otra conversación) |
| 7 · Propuesta | Plan apunta al lienzo vigente v73 o posterior. Informe de propuesta archivado; no se toca el lienzo ni sus generadores. | `b3ca50d` (plan), front `fe4cfbf` |
| 8 · Backlog | Comentario E9 de navigation/platform.ts registrado; no se modifica la navegación ni se agregan tests de documentación. Suite de R01 vigente (1473/1473), sin cambios ejecutables adicionales. | `4ad757c` |
| ADR 0039 | Tope 8 de negocio; excluye exactamente ILogger<T>, TimeProvider, IRequestValidator e IUnitOfWork. Controles de 0/8/9 y fichas alineadas. Sin reacomodar servicios de Plataforma ni ampliar excepciones transaccionales. | `0f4e5a3` |
| Helper autorizado | InvitationEmailAsync considera solo su destinatario y omite payloads inválidos intencionales de otras pruebas. Conserva la exigencia de un solo correo propio. | `d911d15` |

## Verificación del punto 1

- `PlatformOrganizationNoticeTests`: rojo real con 4/5 mensajes frente a los 2 esperados; verde 2/2 tras la corrección. Incluye principal administrado por otra organización activa, alternativa personal disponible, principal seguro con secundario, registrante retirado y Dueño con cuenta suspendida.
- `PublicPageModerationTests`: el outbox HTTP no contiene el motivo interno. `PlatformTenantAdministrationTests`: suspensión/reactivación/cierre no generan avisos; un registrante sin membresía tampoco los recibe.
- Build de solución: 0 advertencias y 0 errores.
- `dotnet test` completo: **1473/1473**, 0 omitidas, **6m22s564ms**. Salida local: `.artifacts/etapa-5/r01-full.log`.
- La primera verificación front dio **742/743** por un import ajeno de `libphonenumber-js` en `PublicPagePage.tsx`. La conversación dueña lo corrigió; la verificación posterior dio **747/747** (`.artifacts/e5-review-front-tests-3.log`). Ninguno de esos archivos fue modificado aquí.

## Verificación de las correcciones posteriores

- ADR 0039: control rojo 2/2 → verde 2/2; el detector acepta las cuatro dependencias técnicas y ocho de negocio, y rechaza nueve de negocio. Las tres fichas usan la misma regla; IOptions<T> sigue contando.
- Helper autorizado: `OrganizationUsersTests`, **5/5**.
- R02: `PlatformTenantAdministrationTests`, **6/6**, incluido el POST Suspender con versión anterior al alta del miembro; pruebas reales de contadores **2/2** y conflicto por estado conservado.
- R04: dominio **8/8**, configuración Application **4/4**, configuración HTTP **3/3**, módulos HTTP **1/1**, scope HTTP **2/2** y AuditTrail **10/10**. El interceptor reutilizado captura IP sin reemplazar el diff; las escrituras de bootstrap usan IRequestInfo sin IP. Ninguna evidencia histórica se inventa.
- Build completo (`review-build-5.log`): **0 advertencias, 0 errores**. Suite completa (`review-full-5.log`): **1485/1485**, 0 omitidas, **6m01s669ms**, antes de los commits ADR/helper/R02/R04.
- R05: HTTP rojo 400 frente a 204 esperado → `PlatformTenantProvisioningHttpTests` **8/8**; contrato front **1/1**, leyendo el OpenAPI del back y los tipos generados del front. La razón fija se verifica también en SecurityEvents.
- R06: `test:e2e:real` focal con PostgreSQL/pickup propios, sin mocks ni interceptaciones. Ingreso por código real y `/api/me` 200; fallo en **«muestra el listado real de Organizaciones»**, porque la pantalla aún no existe. Salida local del front: `.artifacts/e5-r06-ui-red-4.log`; duración **94,326 s**. Aspire apagado al terminar. El caso de permisos reducidos sigue pendiente: esta salida no aprueba R06 ni la puerta E5.
- Nueva verificación antes de los commits R05/R07/preparador y este seguimiento: build **0 advertencias/errores**, suite back **1486/1486**, 0 omitidas, **6m00s648ms** (`review-build-7.log`, `review-full-7.log`); front **751/751**, 158 archivos, **62,13 s** (`e5-review-front-tests-5.log`). Build front verde; lint termina con código 0 y avisos preexistentes de los bundles del lienzo-v35. Contrato R05 generado y comprobado antes de incluirlo; el trabajo T10 sin commit queda separado.
- La corrida anterior coincidió con cambios simultáneos del límite de organizaciones y dio un fallo de metadatos. Recompilación focal **12/12**, seguida de la suite completa verde citada. Los dos fallos de front de esa corrida también pasaron focalmente **10/10** y luego en la suite completa; no se editaron esos archivos ajenos. El cambio posterior de traducciones de Roles se verificó separado **2/2** (`foreign-roles-green.log`).

## Decisiones tomadas

1. Un principal sirve para estos avisos si está verificado, tiene canal y no está administrado por otra organización. Si no sirve, se reutiliza el resolver de Organización y se vuelve a excluir ManagedByTenantId ajeno, porque el resolver permite métodos de otras organizaciones activas.
2. El rechazo tiene una variante propia del catálogo de avisos, con el nombre de la organización. Los textos de rechazo y moderación no reciben el motivo que registra el operador.
3. La versión sigue usando xmin, pero solo sobre la fila de metadatos. TenantCounters recibe los deltas diferidos y tiene SELECT para runtime; el trigger del alta crea su fila 1:1. Up y Down conservan los agregados.
4. El resumen de seguridad acepta exclusivamente booleanos, cantidades, enums y UTC; conserva antes/después reales y omite iguales. El motivo interno va aparte. IPv4 se enmascara a /24 e IPv6 a /48; una entrada técnica sin cambios guarda `{}`.
5. La ficha de organización tendrá un contrato de detalle propio y un historial paginado por cursor. Dueños viene de accesos e identidad, no del registrante histórico; último estado se deriva de cambios efectivos de Status, no de entradas al alcance ni cambios de módulos. La implementación sigue en T21.
6. El preparador acepta los documentos legales vigentes del operador en la base aislada; los recorridos E3b conservan sus verificaciones por pantalla de aceptación y nuevas versiones. El E2E de Plataforma prueba la navegación y las acciones por pantalla.
7. ADR 0039 se aplica al código nuevo de E5. Los desvíos existentes se consolidan antes de E7; esta corrección no modifica la lista blanca de TransactionBoundaryTests ni los servicios ya hechos.

No se implementaron nuevas pantallas ni se modificó el lienzo.
