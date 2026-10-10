# E8 N3 · Teléfono principal enmascarado de Mi cuenta

Correctivo aprobado por el usuario y acordado entre Back y Front. Base publicada de N2: `ee2990ceef0f42cef088aab4c1c8cf3dee46078d`, OpenAPI SHA-256 `9c0cdd86173b8fe2723259680262c4c6cf607ee0ba721c66cf585d89bcecc49f`. El avance posterior `d6553482d2f24cf9fa31aaa987f87d4adf74b3f7` es documentación de coordinación. Este informe cubre exclusivamente N3 del backend; la publicación la realiza Root.

## Cambios al contrato

`MeResponse`, usado por `GET /api/me` en Consumer, Business y Platform, agrega `maskedPrimaryPhone`, opcional y nullable (`string|null`), con ejemplo `•••• 4521`. El serializador entrega `null` cuando no aplica. No cambia los campos requeridos ni las otras propiedades, rutas o schemas. El contrato entero contra N2 tiene exactamente una diferencia: `/components/schemas/MeResponse/properties/maskedPrimaryPhone` (160 rutas, 274 schemas).

OpenAPI regenerado: SHA-256 `0ae765f28aab2dd6498b57e111b02ef8e2ba2fe5291065d7b74618eef28bdbf3`. `IdentityContractContributor` es el nombre real del contribuidor existente; sólo extiende la descripción y el ejemplo de la máscara a Me. Front regenera sus tipos después de recibir la publicación por coordinación.

## Conducta y decisiones tomadas

- La fuente es el método global de la cuenta con `IsPrimary`, `Type=Phone` y `VerifiedAtUtc`; se aplica `PersonalDataMask.Phone`, que conserva los últimos cuatro dígitos. La respuesta nunca contiene el E.164.
- Phone principal con Email de respaldo conserva su máscara. Email/Google principal con Phone de respaldo, o Phone pendiente/sin verificación, devuelve `null`. Cambiar principal, retirar Phone o aprobar recuperación se refleja en la siguiente lectura.
- El canal apagado y el país cerrado a nuevas altas no ocultan la identidad principal. La disponibilidad sigue controlando la advertencia de método personal utilizable por separado.
- `ProfileSnapshotBuilder` reutiliza `ListByUserIdAsync`, ya necesario para otras reglas. Conserva sus ocho dependencias y no agrega puertos, consultas, N+1 ni transformadores globales.
- `ManagedEmail` conserva exactamente su selección Business: correo verificado administrado por la organización activa y `account.PrimaryEmail` como respaldo. No cambian nombres, acceso activo, permisos, versión del perfil, capacidades, legales ni creación de Personal. No se cambian los operadores exclusivos.
- Una identidad eliminada recibe el rechazo de autenticación vigente. La prueba HTTP prepara también revocación e invalidación del estado, como el flujo real; una prueba unitaria protege además la composición contra un Phone verificado residual.
- No se modifican los modelos, reader ni servicio de Cuentas N2, el front, WhatsApp, Asistente, cuota, bot, recuperación productiva, permisos o persistencia. No se agrega una migración.

## Verificación

Evidencia original en `.local/reviews/e8b-contract/`, con comandos y códigos de salida en `e8-profile-primary-phone-mask-files.json`. No se suman ejecuciones superpuestas.

| Comprobación | Resultado | Evidencia |
|---|---|---|
| RED HTTP dirigido | 5 casos fallan por la propiedad ausente (`KeyNotFoundException`); exit 2 | `me-mask-red-real.log` |
| Build API e integración con analyzers | 0 advertencias, 0 errores; exit 0 | `me-mask-build-it-recovery.log` |
| Build unitarios con analyzers | 0 advertencias, 0 errores; exit 0 | `me-mask-build-unit.log` |
| GREEN HTTP PostgreSQL focal | 20/20: 12 N3 + 6 acceso/Me + 2 versión del perfil; exit 0 | `me-mask-green-http-final.log` |
| Unitarios `ProfileServiceTests` | 52/52, incluidos correo administrado, lectura única y Deleted defensivo; exit 0 | `me-mask-unit.log` |
| Comparación completa OpenAPI | Única propiedad Me agregada; exit 0 | `me-mask-openapi.log` |

Las pruebas HTTP propias cubren Phone principal solo/con Email de respaldo, Email/Google principal con Phone de respaldo, pending principal sin verificar, los tres accesos, conservación de campos y correo Business administrado, canal apagado/altas cerradas, cambio de principal/remoción, aprobación real de recuperación e ingreso posterior, y rechazo de Deleted sin exposición. Las regresiones focales cubren 401, permisos Consumer/Business/Platform, organización/país, rol traducido y versión de perfil.

## Incidencias previas y límites

El primer intento sandbox del runner terminó por `NamedPipeClientStream` con exit `-532462766` y cero casos; no cuenta como RED. El relanzamiento escalado autorizado ejecutó PostgreSQL real. La primera selección GREEN con `*MeTests` incluyó `HomeTests` por el comodín: se conserva su salida y se corrigió el selector a clases exactas.

La ejecución exacta de las tres clases incluyó los dos casos de `MeTests.Current_legal_document_is_public_in_the_requested_culture`: ambos esperan versión 1 y reciben 2. La base N2 ya contiene esa expectativa y `AssistantLegalDocumentSeed.Version=2`. N3 no toca ninguno; quedan informados al coordinador para la puerta final. Esa corrida tuvo 22 casos, 19 correctos y 3 fallidos: el tercer fallo era el cooldown del fixture recuperación→nuevo ingreso, corregido desplazando los códigos de preparación, como `AccountJourney`.

El cambio ajeno `Domain/Authorization/Role.cs` se conserva byte a byte (SHA-256 `3260a9378a8bb68317e1d92ce81e290435d29b7d030624d72dfed470a114039a`), fuera del inventario propio. No hay stage, commit ni push desde el implementador. No se ejecuta puerta completa, E2E, galería, recorrido Meta ni cierre E8 en este correctivo; pertenecen al cierre integral autorizado.
