# 0050 · Auditoría global de módulos por SecurityEvent

**Estado:** aceptada como decisión técnica, 2026-10-09.

## Contexto

La configuración y el catálogo globales de un módulo deben conservar sello de auditoría y evidencia del cambio sin exigir una organización activa. El interceptor del núcleo ya separa `PlatformSettings` y `TenantFeature` del historial privado y les exige un `SecurityEvent` en la misma transacción. Agregar excepciones con tipos del Asistente en el núcleo rompería el límite del módulo quitable ([0048](0048-asistente.md)). `Domain/Common/AGENTS.md` exige un ADR para cada marca nueva.

## Decisión

`IGlobalSecurityAudited`, en `Domain/Common`, extiende `IAuditable` y declara el `SecurityEventType` y el `SecurityEventTargetTenantId` que requiere una entidad global. No importa tipos del módulo ni crea una segunda canalización de auditoría.

El sello `CreatedAtUtc/CreatedBy/ModifiedAtUtc/ModifiedBy` sigue a cargo de `AuditableEntityInterceptor`. `AuditTrailInterceptor` exige un evento agregado con tipo y destino coincidentes y motivo no vacío en la misma UoW; rechaza guardar si falta. El caso de uso lo registra dentro de su única `IUnitOfWork.ExecuteInTransactionAsync`, antes del commit. El bootstrap o seed usa actor `System` conforme las factories existentes, con la misma evidencia y sin exenciones.

La configuración y el catálogo de modelos del Asistente requieren `PlatformSettingsChanged` con destino global; la política de una organización requiere `TenantFeaturesChanged` con esa organización como destino. Se reutilizan las factories de `SecurityEvent` existentes. El evento describe metadatos y motivo del cambio; no contiene apiKey, cipher, texto de chat, títulos, nombres de archivo ni argumentos/resultados de herramientas. Las claves de configuración conservan `NotAudited`.

La marca no autoriza tablas ni cambia su clasificación: cada global del módulo sigue declarado por tipo exacto en su `IPersistenceModelContributor`, con esquema `platform` o `identity`. No puede combinarse con `ITenantOwned`, `IPublishedByBusiness` ni `IConsumerBusinessShared`; el validador del modelo rechaza esa combinación. Estas entidades no generan `TenantAuditTrail`, porque su evidencia global es el evento requerido. Las reglas existentes de `PlatformSettings`, `TenantFeature` y asignaciones de rol se conservan.

## Consecuencias y verificación

El núcleo no conoce ningún tipo de Assistant y al quitar el contribuidor desaparecen sus tablas del modelo. T03 debe registrar la evidencia al escribir configuración, catálogo y políticas; T02 establece la obligación y acredita el rechazo sin evento y la escritura global con evidencia, sin acceso Business activo.

- `AssistantPersistenceTests.Global_settings_require_security_evidence_without_an_active_tenant` verifica el rechazo y la escritura válida por la UoW real.
- `AssistantPersistenceTests.Catalog_provider_and_model_id_are_unique_and_prices_survive_runtime_round_trip` verifica la escritura global y su sello/modelo con el rol runtime.
- `AssistantPersistenceModelTests.A_global_audit_contract_cannot_remove_the_audit_trail_from_a_private_entity` verifica la combinación prohibida.
- `DataClassificationTests`, `ModuleIsolationTests` y la comparación runtime/design/bootstrap conservan los controles de clasificación exacta y retirada del módulo.

La regla operativa está en [auditoría](../rules/auditoria.md) y el alcance de esta etapa en [Asistente](../features/asistente.md). No cambia accesos, permisos ni contrato HTTP.
