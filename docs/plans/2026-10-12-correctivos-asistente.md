# Correctivos del asistente antes de cerrar la 8b

Alcance autorizado: cumplir las reglas existentes, sin funcionalidad nueva. Trabajo en main de ambos repos; commits chicos verificados con pathspec, sin push. Se conservan los verdes de la puerta y sólo se repite el alcance afectado.

| Tarea | Archivos principales | Verificación | Estado |
|---|---|---|---|
| Contrato y límites compartidos | AssistantTechnicalLimits, validadores, AssistantContractContributor, contratos HTTP | validación, contrato y build Api; contracts Front | Hecha |
| Controllers, entidades y persistencia sin duplicación | Domain/Assistant, controllers de chat, AssistantTurnRepository y configuraciones | dominio, arquitectura, integración y aislamiento Assistant | Hecha |
| Runtime, tiempo y proveedor | AssistantOptions, AssistantTurnWorker, AnthropicChatClientFactory, TestFeatures | plazo persistido/reloj falso, proveedor y fake aislado | Hecha |
| Mantenimiento por Application | baja, retención y puertos EF; TenantJobRunner | baja, retención y TenantScopeUsageTests | Hecha |
| Help, formatos, locks y correo | reader y recursos dentro del módulo, VersionWireFormatter, AssistantLockKeys, plantilla | ayuda, formatos, archivos y alertas | Hecha |
| Front autocontenido y hooks/paneles | src/modules/assistant, registration y contribuciones neutrales | módulos, Home, configuración, Retry; lint y TypeScript | Hecha |
| Integración y documentación | quitar-asistente y fichas afectadas | contrato, detector y E2E Assistant verdes sobre main | Hecha |

Contrato primero: conservar rutas, permisos, códigos reales y formas JSON. Retirar declaraciones 501 transitorias, publicar límites ya vigentes y regenerar tipos. Back y Front se implementan en paralelo conforme etapa-en-paralelo; sin revisores por tarea. Los estados persisten los mismos nombres mediante enums con conversión string; una abstracción de dominio no unifica ni cambia el aislamiento de las tablas.

Decisiones técnicas: extender el formateador técnico existente para enteros largos y decimales canónicos, preservando la representación; mover el fake exclusivamente a TestFeatures y sustituir su fábrica con ConfigureTestServices; los plazos y duraciones existentes pasan a opciones validadas, sin alterar sus valores. El entorno real espera también la disponibilidad HTTPS del front; la fixture de cantidad de adjuntos usa una conversación nueva para no mezclar ese borde con el presupuesto de contexto del historial.

| Verificación sobre main con los correctivos | Resultado |
|---|---|
| Build Back | Solución completa: 0 errores y 0 advertencias |
| Unitarios Back | 107 del área/formato y 11 de dominio verdes; después sólo focos afectados |
| Arquitectura | 82 casos del alcance estructural y guardas transversales dirigidas verdes; arnés 32/32 |
| Integración Assistant | 294/294 acumulados, PostgreSQL real; después sólo los 15 casos afectados del contexto |
| Front | 3068/3068 acumulados; una suite y focos de reparación. Build, TypeScript, lint y contrato verdes; 6 advertencias de lint preexistentes. Pruebas Node dirigidas de arranque, continuación y fixture verdes |
| E2E Assistant | 42 hitos, 24 bloques técnicos y 8 cruces en 1440/390, acumulados sin repetir los verdes; entorno apagado |
| Documentación | Detector 0/0/0 en ambos repos; receta de retiro actualizada |

Código medido en main: Back `c7611f45`, Front `c1917c24`. Galería, recorrido, retiros y Anthropic de la puerta anterior se conservan: los correctivos no cambian pantallas ni agregan funcionalidad; el flujo real afectado quedó verificado. Todos los cambios están en commits con pathspec, sin push.
