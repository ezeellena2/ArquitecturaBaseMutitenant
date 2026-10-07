# Etapa 7 · Personal, sitio público e interacción

Inicio: 2026-10-07. Plan: [público y personal](../plans/2026-10-04-etapa-7-publico-y-personal.md). Estado: en implementación; la etapa todavía no está cerrada.

## Alcance y base

Back en `main`, base `2d5f87f`; front en `main`, base `fec2a86`. Ambos arneses declaran cerradas las etapas 0 a 6. La consolidación y las piezas comunes de edición, permisos y navegación ya existen. El lienzo vigente es v112 y sus tableros de esta etapa están aprobados.

La copia se comparte con los arreglos A8–A11 y R01/R03. Sus cambios en ingreso, sesiones, invitaciones, Empresas, datos fiscales y runner quedan fuera de los commits de esta etapa. Se integra el ingreso después de los commits A8/A9. No se detiene el entorno de la otra conversación.

## Forma de trabajo aplicada

Tareas en orden, commits por rutas propias, sin push. Tests de negocio, permisos, aislamiento y nuevos recorridos reales junto al código; una sola puerta completa al final, sin aprobación ni capturas por avance. La galería se arma al cerrar la implementación. No se inicia la Etapa 8.

## Decisiones tomadas

9. La sección inicial «Forma de trabajo de esta etapa» y el encargo del 2026-10-07 reemplazan los pasos antiguos de rojo previo, evidencia por tarea, visto bueno y capturas en cada avance. La base compartida puede tener cambios ajenos: se preservan y no se exige limpiar ni apagar el trabajo de otro chat. Las verificaciones completas y los detectores se ejecutan en la puerta final.
10. Las ADR 0043–0047 son el contrato vigente del ingreso y la cuenta: errores explícitos de cuenta, cookies públicas y de Plataforma separadas, acciones de Mi cuenta sin reautenticación adicional, nombre derivado en el back y aceptación de invitaciones por código. La etapa reutiliza esos comportamientos y no restaura las reglas anteriores del plan.
11. El contexto público conserva `BusinessTenantId` y `Slug` anulables en un objeto por pedido. Solo las rutas `[PublicSite]` consultan el reader y solo aceptan una etiqueta válida sobre el host y puerto del issuer. La consulta exige publicación, ausencia de bloqueo, organización empresarial activa y slug no reservado. El filtro HTTP usa ese contexto y el error existente; nunca modifica el tenant de la sesión ni agrega una caché antes de la Etapa 9.
12. Las versiones de URLs y ETags usan `VersionWireFormatter`, con una excepción nominal en la guarda de formato, igual que los serializadores HTTP existentes. El tipo solo acepta `uint` y produce dígitos invariantes sin separadores; sus tests cubren culturas distintas y el límite del tipo. `DisplayFormatter` conserva la presentación de datos para personas. Esto corrige los dos llamados locales de T02 que detectó la guarda durante las verificaciones de A9.

## Cierre

Pendiente: resultado de comandos, commits, decisiones técnicas adicionales, galería y recorrido manual con sesiones anteriores.
