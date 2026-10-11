# E8 · Correctivo del preparador legal del E2E 3b

El recorrido 3b de la ejecución 53 esperaba que F5 pidiera aceptar términos nuevos, pero el preparador no publicaba nada: Assistant ya aportaba Términos v2 al seed, el registro aceptaba v2 y la orden fija `version: 2` terminaba sin insertar otra revisión. El marcador `.done` no distinguía ese resultado. El caso 5d anterior publicaba Privacidad, no Términos.

`LegalVersionCommand` publica ahora la siguiente revisión: toma el lock del tipo, lee el número máximo incluyendo revisiones programadas y crea `última versión + 1` vigente en el instante leído después del lock. Usa los puertos legales y una sola UoW; el recibo se escribe después del commit y aparece completo mediante un archivo temporal y su renombrado.

## Protocolo privado del runner

- Orden: reemplazar la extensión `.ready` del archivo de control por `.legal.json`, con `{"kind":"Terms"}`. Se conserva la compatibilidad con `{"kind":"Terms","version":2}`; ese campo antiguo no determina la revisión publicada.
- Recibo: agregar `.done` al archivo `.legal.json`, con `{"id":"UUID de la revisión","version":3}` y el número real confirmado. Con una revisión v4 programada, la nueva es v5.
- Front compara el recibo con `GET /api/legal/terms` y con los documentos pendientes de `GET /api/me` antes de F5. La aceptación continúa por `POST /api/legal/accept`.

No hay delta del contrato HTTP. El cambio se limita al preparador E2E, su prueba y el enlace de compilación de la fuente real en el proyecto de integración. La fixture prepara v2 explícitamente si falta, como dato del caso independiente de los módulos instalados. No cambia el seed productivo ni el gate legal.

## Verificación focal

Sobre `main` con base `68ed72f2bc4d98a58ae7ccf660f771c438184cb5`, con Docker y la fuente ajena de `Role.cs` conservada:

| Verificación | Resultado | Duración del proceso |
|---|---|---|
| Rojo contra el comando original preservado fuera del producto | 4 fallas esperadas, 0 omitidos; código MTP 2 | 77,18 s |
| `LegalVersionCommandTests`, versión final de la fixture | 4/4 correctos, 0 omitidos | 79,85 s |
| `LegalAcceptanceTests` | 4/4 correctos, 0 omitidos | 58,69 s |
| `AssistantLegalSeedTests` | 3/3 correctos, 0 omitidos | 63,82 s |
| Build API/integración final | 0 advertencias, 0 errores | 24,03 s |
| Build Tools final | 0 advertencias, 0 errores | 10,64 s |

Son **11 casos distintos verdes**. El primer verde de los mismos cuatro casos también pasó antes de explicitar el dato v2 en la fixture; esa repetición no aumenta el total de casos distintos. El rojo comprobó tanto las dos órdenes nuevas rechazadas por el comando anterior como las dos órdenes antiguas que no publicaban v3/v5.

Los cuatro casos finales combinan orden nueva/antigua y seed vigente v2/revisión v4 programada. Verifican persistencia, recibo, lectura de Términos, pendientes de Me, 403 `Legal.AcceptanceRequired`, aceptación 204, ruta nuevamente 200 y repetición de la orden sin otra publicación. Los builds de exportación conservaron en sus logs un `UnauthorizedAccessException` de DataProtection; finalizaron con código 0 y 0 advertencias/errores, y generaron el OpenAPI correctamente.

El contrato publicado y el export generado son idénticos: SHA256 `96e0f10b7aefdf085ea8ceca0cd2bf57e83261e9eb452ba4f5ad379522575a45`. Evidencias locales: `.local/reviews/e8-legal-version-20261010/`, con fuente original, override de compilación para el rojo, comandos, logs, fechas UTC, códigos de salida e inventario de entrega.

## Entrega a Integración

Trasladar únicamente este correctivo y el correctivo Front `8b963664` al corte E8 y repetir el recorrido 3b en navegador real. Esta verificación focal no cierra E8 ni acredita el recorrido Meta real. Si la prueba de retiro de módulos falla, registrar archivo y causa y esperar la respuesta del usuario, sin modificar código o fixtures para corregirla automáticamente.
