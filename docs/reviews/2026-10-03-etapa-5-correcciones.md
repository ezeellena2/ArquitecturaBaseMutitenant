# Correcciones de la revisión de E5

E5 sigue abierta. Este informe registra la revisión solicitada sobre lo implementado hasta `2e5a06b`; no reemplaza la puerta de cierre ni aprueba capturas.

| Punto | Resultado | Commit |
|---|---|---|
| 1 · Avisos | Solo rechazo y moderación. Dueños con membresía e identidad activas; principal seguro o alternativas del resolver, sin correo administrado ajeno ni registrante retirado. El contrato no admite el motivo interno. | `02184ca` |
| 2 · Versión | Pendiente: separar contadores de la fila versionada. | — |
| 3 · Ficha | Lecturas globales definidas en el plan; implementación y contrato pendientes en T21. | `b3ca50d` (definición) |
| 4 · Auditoría | Definición incorporada en T17; ajuste de los eventos existentes pendiente. | `b3ca50d` (definición) |
| 5 · Retry | Pendiente: motivo fijo sin campo escrito. | — |
| 6 · E2E operador | Pendiente: pasos de pantalla y permisos reducidos; rojo esperado hasta T21–T23. | — |
| 7 · Propuesta | Plan apunta al lienzo vigente v73 o posterior. Archivo de propuesta preparado como archivado; sin commit front mientras su suite tenga un rojo ajeno. | `b3ca50d` (plan) |
| 8 · Backlog | Comentario E9 de navigation/platform.ts registrado; no se modifica la navegación ni se agregan tests de documentación. Suite de R01 vigente (1473/1473), sin cambios ejecutables adicionales. | commit de esta corrección |

## Verificación del punto 1

- `PlatformOrganizationNoticeTests`: rojo real con 4/5 mensajes frente a los 2 esperados; verde 2/2 tras la corrección. Incluye principal administrado por otra organización activa, alternativa personal disponible, principal seguro con secundario, registrante retirado y Dueño con cuenta suspendida.
- `PublicPageModerationTests`: el outbox HTTP no contiene el motivo interno. `PlatformTenantAdministrationTests`: suspensión/reactivación/cierre no generan avisos; un registrante sin membresía tampoco los recibe.
- Build de solución: 0 advertencias y 0 errores.
- `dotnet test` completo: **1473/1473**, 0 omitidas, **6m22s564ms**. Salida local: `.artifacts/etapa-5/r01-full.log`.
- Front: **742/743**; el fallo ajeno es el import de `libphonenumber-js` en `PublicPagePage.tsx`, detectado por `format-usage.test.ts`. Ese archivo pertenece al trabajo de otra conversación y no se modifica aquí. Salida local: `.artifacts/e5-review-front-tests.log` del front.

## Decisiones tomadas

1. Un principal sirve para estos avisos si está verificado, tiene canal y no está administrado por otra organización. Si no sirve, se reutiliza el resolver de Organización y se vuelve a excluir ManagedByTenantId ajeno, porque el resolver permite métodos de otras organizaciones activas.
2. El rechazo tiene una variante propia del catálogo de avisos, con el nombre de la organización. Los textos de rechazo y moderación no reciben el motivo que registra el operador.

No se implementaron nuevas pantallas ni se modificó el lienzo.
