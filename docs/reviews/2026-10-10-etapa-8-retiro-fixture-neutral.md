# E8 M2 — fixture neutral de canal para ayuda del Asistente

Correctivo focal sobre Back `8e056f2a751319489d7246d13e3cb397db80d3a5`. La retirada física coordinada de WhatsApp había fallado con CS0234 en `AssistantDynamicAuthorizationTests.cs:23`: el árbol del Asistente importaba el módulo opcional para reemplazar dos `IOptions<WhatsAppOptions>`. El rojo real previo está en `.local/puerta-8/20261010-1/29-removal-whatsapp-build.log`; no se recreó ni se modificó esa copia.

## Cambio y decisiones tomadas

Los dos hosts de ayuda registran ahora un fixture local de `ILoginCodeChannel`: key sintética `whatsapp`, método Phone y disponibilidad explícita. Se reemplazan los registros de ese puerto sólo en esos hosts de herramientas directas, por lo que no hay dos canales con la misma key cuando WhatsApp está instalado. El registro se sigue envolviendo con `GuardedChannel`; validar, renderizar o encolar incrementa `ChannelProbe.SendCalls` y falla. No existe ni se agregó un método Send al puerto.

Se conservaron el lector embedded real, el contribuidor de feature del catálogo, las cuentas y permisos reales, la aceptación de aviso, los scopes, la pausa tras lectura, la revocación de permiso y feature, las cuatro variantes es-AR/en-US con canal disponible/no disponible y los asserts de cero proveedor/cero operaciones de envío. La prueba de disponibilidad dejó de prometer configuración real en su nombre.

El puente real de `WhatsAppOptions` a `WhatsAppChannelSettings` y `WhatsAppLoginCodeChannel.IsAvailable` se prueba dentro del árbol quitable de WhatsApp: número null, vacío, whitespace y presente; cada caso sustituye el valor de opciones después de construir el mismo adaptador y comprueba el cambio de disponibilidad. No se ejercen renderer ni outbox en esos casos; se mantiene la cobertura previa de país E.164, teléfono público y lifetime.

Las dos recetas usan `<ULTIMA_MIGRACION_DEL_CORTE>` y explican obtener el ID completo del inventario real antes del scaffolding. Se preservaron historia/Designers, grants y RLS, normalización cerrada de administradores, preflight de asignaciones y resguardos de copia. No se cambió producción, DI, núcleo de ayuda, contrato, Front ni migraciones.

Se eligió no ampliar `ModuleIsolationTests` en este correctivo: su guarda vigente inspecciona los cuatro assemblies productivos, no el assembly de Integración; por eso su verde no acreditaba la independencia CLR de este fixture. No se agregó parser, whitelist, Skip ni relajación de la guarda productiva. La prueba física coordinada sigue siendo el control integral de este límite.

## Verificación focal

Ejecución serial, sin suite global:

| Verificación | Resultado |
|---|---|
| Build API, exportación OpenAPI desactivada | 0 errores, 0 advertencias |
| Build Api.IntegrationTests, exportación OpenAPI desactivada | 0 errores, 0 advertencias |
| AssistantDynamicAuthorizationTests, Docker real | 9 correctos, 0 fallos, 0 omitidos |
| WhatsAppChannelSettingsTests | 13 correctos, 0 fallos, 0 omitidos |
| WhatsAppStartupTests | 10 correctos, 0 fallos, 0 omitidos |

Son **32 casos distintos** en tres clases sin solapamiento. Los cuatro casos nuevos están incluidos en los 13 de settings, no se suman nuevamente.

Dos invocaciones previas de Assistant ejecutaron **0 casos** y quedan en el inventario como fallos de tooling: una con opciones de reporte salió con código interno 5; otra falló con `UnauthorizedAccessException` al conectar NamedPipe de Microsoft.Testing.Platform. La ejecución estándar autorizada fuera del sandbox pasó los 9 casos. Estos intentos no son rojos de conducta ni aumentan los contadores de tests. La consulta operativa previa `dotnet test --help` tampoco se cuenta: su evaluación imprimió un fallo, sin ejecutar suite.

Comandos literales, UTC, salidas, SHA-256 de evidencias y bytes de fuentes/base están en `.local/reviews/e8b-contract/e8-removal-fixture-neutral-files.json`. Role.cs conserva el SHA-256 protegido `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`; los OpenAPI de docs y obj conservan `86428a7254f5e7f6f412fb06c63e05287655e03f81e17c49ce4a8c88f81928a0`. Role.cs ya estaba modificado por trabajo ajeno y no integra este paquete.

## Límite y entrega

Este informe entrega cuatro fuentes modificadas y este documento; sin stage, commit ni push. La estabilidad final registra índice vacío y cero procesos de build/tests del correctivo. Integración debe publicar y trasladar el cambio al corte fijo, y reintentar allí retiradas WhatsApp/Asistente/ambos con bases vacía e histórica, suites y E2E. Estos focales no certifican retirada física, cierre de etapa ni transporte Meta.
