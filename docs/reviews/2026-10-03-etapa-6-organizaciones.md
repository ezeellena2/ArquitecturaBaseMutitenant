# Informe de la Etapa 6 · Organización B2B

Fecha: 2026-10-03.

La implementación y las puertas automatizadas de E6 están completas. El recorrido manual con Gmail queda pendiente por indicación del usuario; por eso no declaro aprobada esa prueba ni cerrada la puerta manual. Los dos inicios de Organización y Miembro siguen «por refinar» en v64 y conservan el comportamiento existente.

## Trabajo y commits

| Tarea | Backend | Front |
|---|---|---|
| Plan E6, alta B2B, slug y provisionamiento idempotente | `ed3c6b42`, `813ff0c`, `bd91f9e`, `fb12872`, `b806e38`, `d3f6b42`, `bd1c560`, `79f2bcb` | `56bdf52` |
| Empresas, CUIT, membresías y roles por empresa | `234fc72`, `47e842b`, `34b8f4b`, `b005044`, `3c6cd3f`, `7fbc59a`, `7c3a3c3`, `a3e448d`, `f7834b6` | `e67f062`, `c2fdb09`, `550f660` |
| Usuarios, invitaciones y protección de Dueño/correos | `f77becd`, `d63e13c`, `654946c`, `9aab020`, `0a8d62f`, `bfea29f` | `047fe44`, `73e2c8f`, `313c361` |
| Configuración y dominios administrados | `02dae57`, `b805110`, `085cd5f`, `c5d2082`, `c37d0a6`, `63492f7` | `0b80232` |
| Página pública y moderación | `9d2338b`, `11b9d23` | `b386ed1` |
| Auditoría, AdminPanel, v64 y comparación visual | `7016b57`, `6e98bc1`, `232b95b` | `7d1bbea`, `53d1eae`, `b123247`, `550f660`, `5fa1943`, `88120c2`, `a7ef9f1`, `60e025c` |
| Etapas no contiguas y documentación de fichas | `b86d47b`, `fdc678a` | `5fc19df`, `7479dd3` |

Los commits de corrección relacionados incluyen `cf72799` (hallazgos de puerta), `9aab020` (consultas de usuarios), `654946c` (conservar roles de empresas inactivas) y `63492f7` (correo personal de miembro inactivo). Las correcciones de invitaciones anteriores —back `51f604f`, front `feb67ec` y `7c83835`— se preservaron.

## Puertas y evidencia

- `dotnet build ArquitecturaBaseMultitenant.slnx`: correcto, 0 advertencias y 0 errores.
- `dotnet test ArquitecturaBaseMultitenant.slnx`: correcto, 1387/1387, 0 fallos y 0 omitidos; integración con PostgreSQL incluida.
- Generador de catálogos: `node scripts/datos-de-referencia/generar.mjs` correcto; sus pruebas pasaron 30/30.
- Front: `npm test` 720/720; `npm run build` correcto; `npm run contracts:check` confirma que el esquema TypeScript coincide con OpenAPI. `npm run lint` termina con exit 0; informa avisos en assets antiguos de `lienzo-v35` y reglas existentes, sin error de lint.
- Arnés existente: `HarnessTests` 10/10 y `harness.test.ts` 5/5. Las etapas cerradas son `{0,1,2,3,4,6}`; E5 sigue abierta.
- E2E real: 13/13, 0 fallos y 0 omitidos, 885690 ms (~14m45s). Usó AppHost, PostgreSQL y `PickupDirectory` aislados, sin mocks. Incluyó el alta B2B por correo, `/org`, F5, cambio a Personal, emisión desde Usuarios y aceptación desde el `.eml` con y sin cuenta previa, empresa y rol por invitación, edición de Empresa y Configuración, página pública borrador/publicada y auditoría con persona y empresa afectadas. El proveedor OAuth local de Google no está configurado, así que ese canal no formó parte del recorrido de navegador.
- Capturas: 46 pares lienzo/app en escritorio 1440×900 y móvil 390×844 (92 PNG). `node scripts/capturar-etapa-6.mjs --verify` confirma los archivos.
- Aspire quedó apagado (`aspire ps --format Json` devolvió `[]`).

## Diferencias visuales con v64

La comparación detallada y las imágenes están en [`docs/design/capturas/etapa-6/comparacion.md`](../design/capturas/etapa-6/comparacion.md).

1. En móvil, la ficha Usuario no reproduce literalmente una lista compacta de empresas con ⋮ y los diálogos de roles/sumar empresa del escritorio; se implementaron los diálogos de guardado inmediato que pidió el usuario y se mantuvo intacto el tablero.
2. El diálogo «Quitar» explica que dejar de asociar correos nuevos conserva la protección de los métodos ya vinculados. El usuario aprobó este ajuste para impedir que un exmiembro recupere acceso Personal.
3. Configuración, Página pública, Error-Org y Avisos no tienen tableros móviles dedicados para esos estados. Se conservaron las referencias completas de escritorio y se capturó la aplicación a 390×844; no se reutilizó un tablero móvil de otra experiencia.
4. Auditoría no muestra un total del período porque usa cursor sobre una tabla append-only y no hace un conteo global. La diferencia queda en `docs/backlog.md`.
5. En móvil, correos/eventos largos pueden truncarse visualmente; el detalle completo sigue accesible desde la fila o su acción de detalle.

## Decisiones tomadas

- Se implementó el lienzo v64, incluidos sus móviles, con la aprobación explícita del usuario; esa confirmación prevalece sobre marcas de aprobación anteriores que quedaron en `canvas.json`.
- Los tableros Inicio siguen «por refinar». No se inventaron estados; se mantuvo el inicio actual y el recorrido real se comprobó por E2E.
- Quitar un dominio detiene futuras asociaciones automáticas, pero retiene `ManagedByTenantId` en métodos existentes. Así la salida de la organización no reactiva Personal para exmiembros.
- El alta B2B usa correo y Google según los canales existentes; WhatsApp queda fuera hasta E8. La puerta E2E local cubre correo porque no hay proveedor OAuth local configurado.
- TenantProvisioner es idempotente; la primera empresa da al Dueño una membresía activa y `CompanyAdmin`. Los roles de empresa se combinan con los de organización y requieren membresía activa.
- El logo se guarda en `public_site` como `bytea`, solo PNG/JPEG/WebP, con firma comprobada y límite de 512 KiB.
- Auditoría pagina por cursor estable, enmascara correo/teléfono/CUIT y devuelve nombre afectado y empresa. No calcula un total de toda la tabla.
- E6 puede cerrarse antes de E5: el arnés usa una lista explícita de etapas cerradas y mantiene E5 pendiente.

## Pendientes reales

- Por indicación del usuario, el recorrido manual Gmail queda pendiente: registrar una organización, ingresar y comprobar F5/cambio a Personal; invitar desde Usuarios a una cuenta existente y a otra nueva; recibir y aceptar ambos enlaces. No se guardaron direcciones y la prueba no se declara aprobada.
- Si se quiere rediseñar Inicio-Org o Inicio-Miembro, hace falta una definición aprobada de los tableros que hoy están «por refinar».
