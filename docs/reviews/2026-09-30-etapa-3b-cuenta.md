# Etapa 3b · La cuenta — informe de ejecución

Estado: en ejecución. La 3c queda pendiente. No declarar cerrada esta etapa sin puerta completa y E2E real verde.

## Tareas y commits

| Tarea | Commit back | Commit front | Evidencia |
|---|---|---|---|
| Plan previo | `c60cd18` | — | 30 tareas, TDD, alcance y matriz visual |
| T01 · recorridos reales rojos | — | `20e2e8b` | Los cinco fallaron antes de implementar /cuenta |
| T02 · reglas de métodos | `61ffe6c` | — | Rojo CS1501/CS1061; verde LoginMethodTests 6/6 |
| T03 · comprobante de reautenticación | `414cb47` | — | Rojo CS0103; verde ReauthTicketTests 4/4 |
| T04 · persistencia de cuenta | `5949ffb` | — | Rojo CS1061; Testcontainers LoginMethodsTests 3/3; modelo sin cambios pendientes; SQL revisado |
| T05 · avisos de cuenta | `5eebba1` | — | Rojo CS0234; Testcontainers AccountNoticeTests 2/2; contactos deduplicados, payload cifrado y aviso final principal |
| T06 · agregar y verificar correo | `0d6f226` | — | Rojo CS0246/CS0117; LoginMethodsTests 5/5 y plantillas: total 17/17; código Login rechazado, intento guardado, código propio consumido y reserva global |
| T07 · quitar y elegir principal | `ba150f2` | — | Rojo CS0246/CS1061; integración 8/8, inventario existente de identidad 3/3; contexto/replay/vencimiento, último método y principal/copias atómicos |
| T06 bis · aislar registro y correo pendiente | `2097c3b` | — | Rojo: 204 con cookie en lugar de 400; SignupTests 8/8; registro no confirma métodos pendientes agregados desde otra cuenta |
| T08 · vincular Google | `457f569` | — | Rojo CS0246; Google 13/13 + protocolo 1/1; sesión distinta/correo no verificado/conflicto rechazados; OAuth state protegido y antiforgery |
| T09 · HTTP de métodos y reautenticación | `38112e8` | — | Rojo: 404 en rutas inexistentes y código sin traducir bajo campo; HTTP + inventario 5/5; 401, 404 ajeno real, replay y verificación pickup |

| T10 · perfil versionado y aviso | `a533f5c` | — | Rojo: falta version y luego 204 en vez de 409; integración 19/19, unit 24/24; guardado viejo conserva datos e idioma; aviso usa disponibilidad |

| T11 · términos bloqueantes | `6d8be73` | — | Rojo: 200 en vez de 403; integración 8/8 y perfil unit 13/13; versión nueva entre lectura/aceptación devuelve 409, aceptación append-only y repetible |

| T12 · reglas de baja y gracia | `0fbce93` | — | Rojo CS1061/CS0103; AccountDeletionPolicyTests 6/6; operador, pendiente, suspensión, límite inclusivo y anonimización |

| T13 · participantes de eliminación | `e5bb79c` | — | Rojo CS0246/CS1061 y falta de grant 42501; runtime 1/1 + inventarios existentes 12/12; limpieza doble, auditoría única, mensajes ajenos intactos y DELETE B2B devuelve cero |

| T14 · pedir baja y revocar sesiones | este commit | — | Rojo: 404; integración 7/7; operador/sin ticket rechazados, módulo conserva estado y ticket, tokens Consumer y Business revocados y cookie cerrada |

## Evidencia del E2E real antes de programar

`npm run test:e2e:real`, 30/09/2026. AppHost y PostgreSQL efímero exclusivos; datos del preparador y de /registro en esa base. Registro/puerta empresa/F5/cambio Personal/logout de 3a se recorrieron antes de los casos nuevos.

```text
✖ 3b: sumar correo personal con código leído del .eml (18744.9227ms)
✖ 3b: quitar un método con código enviado a otro (19940.3442ms)
✖ 3b: pedir baja y cancelarla ingresando durante la gracia (19921.7423ms)
✖ 3b: cambiar el idioma a en-US desde /cuenta (20112.2356ms)
✖ 3b: aceptar versión nueva de términos que bloquea el ingreso (19950.0975ms)
Error en cada caso: La cuenta 3b: /cuenta no muestra la pantalla Mi cuenta.
tests 6 · pass 0 · fail 6 · cancelled 0 · skipped 0 · duration_ms 140946.0782
```

La sexta prueba es el padre, que falla porque sus cinco subrecorridos fallan. Se mantienen activas: el resultado final debe ser verde en todos. El runner cerró su AppHost y eliminó el pickup propio al terminar.

## Puerta

Pendiente de la implementación.

## Diferencias con el lienzo

Ocultaciones solicitadas: WhatsApp (E8), exportación/sugerencia de exportar (E10) y bloqueo del único Dueño (E4). Comparación visual todavía pendiente.

## Decisiones tomadas

- Ejecución directa en main, en este chat, según autorización expresa. Revisión del plan sin subagentes.
- Google conserva subject como clave y suma contacto Email verificado para visualización y avisos; la dirección no reemplaza el subject.
- La nueva versión legal del E2E la publica su preparador en la base propia por comando de archivo. No se agrega endpoint productivo ni se toca Development.
- La limpieza de una cuenta real respeta la gracia: pedir baja impide su ingreso normal y el worker anonimiza al vencer; no se borra Identity ni auditoría a mano.
- HarnessStage sigue en 2 hasta cerrar 3c; solo se actualizan inventarios existentes exigidos y se agrega el contrato cruzado funcional.
- T03 entrega entidad y puerto; los helpers de emisión/verificación se conectan en T07 después de la persistencia T04, para probar el flujo completo con sus códigos y locks reales.
- T04: índice parcial único de principal. Como PostgreSQL lo comprueba al emitir cada UPDATE, el repositorio retira la marca anterior dentro de la UoW antes de fijar la nueva; la fila no es auditable ni soft-deletable y rollback revierte todo. `xmin` usa la columna de sistema (el SQL de Npgsql no crea una columna manual).
- T05: el canal recibe CultureProfile ya resuelto y renderiza async dentro de la UoW llamadora. UserId nullable permite cancelar mensajes propios sin modificar mensajes anteriores o de otros destinos; cancelar borra el payload cifrado pendiente.
- T06: se reutiliza VerifyDestination ya definido en LoginCodePurpose, en lugar de crear un propósito equivalente. Los avisos de agregado/desvinculado adaptan el sustantivo del método en la plantilla aprobada; Google mantiene subject privado.
- T07: el código también conserva acción/respaldo/objetivo, antes del ticket; de otro modo, la misma prueba podría presentarse como una acción diferente al verificar. Los métodos administrados consultan un booleano de membresía/organización activa mediante un puerto específico, declarado en el inventario existente; no consultan Members fuera de RLS.
- T06 bis: un correo pendiente agregado desde Mi cuenta solo se confirma con VerifyDestination y su UserId. El registro anónimo no lo activa ni inicia una sesión de su propietario; devuelve el error existente de dirección asociada después de probar el código. Las invitaciones se implementan con su protocolo en 3c.
- T08: para iniciar OAuth desde una API bearer, el vínculo recibe JSON con redirectUrl generado por el middleware; la navegación Google sigue después. El callback existente compara el UserId del state protegido con la cookie Identity validada, sin usar query para cuenta o retorno. Locks externos antes del lock de cuenta. Contactos Google existentes sin dato se completan con el correo probado en su próximo ingreso.
- T09: el listado es lectura por puerto separado, devuelve acciones/respaldo calculados y días de gracia de PlatformSettings. El subject Google queda fuera del contrato. Los códigos de resources que Domain usa como errores de campo se traducen centralmente en ProblemDetailsMapper; los mensajes de validación ya traducidos siguen igual.

- T10: las preferencias y el nombre se guardan al confirmar la UoW con OriginalValue=xmin recibido. UserStore.Update vuelve a adjuntar y elimina la expectativa del cliente; no se usa para este guardado sin campos de Identity. El conflicto real devuelve 409 y conserva los primeros datos.

- T11: lector legal por UserId explícitamente autorizado en inventario existente. La prueba de publicaciones usa ApiFactory propia para conservar los documentos v1 de otras pruebas. El preparador E2E acepta v1 para su cuenta fixture; no cambia el seed ni registra aceptación ficticia del operador.

- T12: el estado y la fecha permanecen separados. Suspended con fecha solo vuelve a PendingDeletion al reactivar; nunca cancela suspendida ni al vencer. El cierre conserva Id/fechas para referencias y auditoría y limpia la razón de texto libre. Los cambios de estado quedan pendientes del único guardado UoW y rotan SecurityStamp al pedir baja.

- T13: el puerto queda en Integrations/Legal, como el documento canónico. La proyección de eliminación es un lector específico de TenantId/Kind sobre el índice técnico existente, sin reutilizar el lector reservado de perfiles. La limpieza Personal borra sus filas y su índice en la misma transacción; un reintento omite el alcance ya limpiado. Membresías B2B quedan Removed con motivo AccountDeleted y evento de auditoría único.
- T13: revisión automática rechazó el grant general DELETE por ampliar el alcance de mt_app. La alternativa aprobada agrega una política AS RESTRICTIVE solo para Personal, combinada con tenant_scope y FORCE RLS; el test demuestra que DELETE en Business no borra nada incluso con el alcance correcto. No se alteró Development.

- T14: se reutiliza SignInService.RevokeSessions para revocar cookie por SecurityStamp y autorizaciones/tokens OpenIddict de ambos accesos en la UoW. La API cierra la cookie después del commit. Los mensajes anteriores se cancelan antes de encolar el aviso de baja; la razón permanece únicamente en la identidad y se limpia al anonimizar.

## Recorrido manual

Pendiente de rutas y contratos finales.
