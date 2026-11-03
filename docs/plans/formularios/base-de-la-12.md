# Base de la 12 (spec)

**Estado:** pendiente · spec de lo que va antes de la E12, con el [formato](plantilla.md) de las etapas. Es lo que el usuario decidió el 25 y el 26/10 viendo la maqueta, y lo que era la F5 de la 10: reglas de todo el sistema, que el código de hoy todavía no cumple. Es lo primero del bloque, sin esperar a la E10 (123), y `/plan-etapa` la contrasta con el código real.

## Qué leer

Antes de empezar, en este orden:

1. **Decisiones** de la hoja de ruta: la 43 (botones y avisos), la 109, la 110, la 115, la 116 y la 122 (leyendas, ayuda, motivos y esperas), y los pedidos del usuario del 25 y el 26/10, en el [plan de desarrollo](../2026-09-27-plan-de-desarrollo.md#etapas-12-a-25-formularios-por-whatsapp).
2. **Fichas:** [textos-y-traducciones](../../rules/textos-y-traducciones.md), [api-http](../../rules/api-http.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [permisos](../../rules/permisos.md), [tests](../../rules/tests.md) y, del front, `pantallas-y-ui.md`, `formatos.md`, `formularios.md`, `paginado-y-listados.md` y `textos-y-traducciones.md`.
3. **Código para copiar:** la columna «Copia de» de los [pasos](#pasos).
4. **El lienzo:** `Campana`, `M-Campana`, `Campana-Plat`, `M-Campana-Plat`, `M-Campana-Personal`, `Otro-Acceso` y `Otro-Acceso-Persona`, y los tableros de cada pantalla que cambia.

## Qué se construye

**Botones, esperas y ayuda** (43, 109, 110, 115, 116 y 122), en todo el sistema:
- un botón está habilitado o grisado, sin textos al lado ni debajo;
- **mientras trabaja,** pone el aro y su gerundio («Guardando…»), con su color, en su lugar y sin aceptar otro clic. Un botón de ícono cambia su dibujo por el aro;
- **una confirmación** queda abierta hasta la respuesta, con su botón en gerundio;
- **una tabla que vuelve a leer** atenúa sus filas, que no se pueden tocar, y corre una línea bajo el encabezado. La primera carga conserva su aro;
- nunca aparece «Cambios sin guardar»;
- si falta un dato obligatorio, el botón queda habilitado, y al tocarlo se marca el campo;
- **lo apagado dice por qué:** el motivo de un botón, una casilla, la ✕ de un diálogo o una opción de menú va en su tooltip, si no es obvio. En el teléfono, tocarlo lo muestra en la franja oscura de abajo;
- **sin leyendas:** el renglón de abajo es solo para el error o para un dato. La ayuda que sirve va en una (i) con tooltip, y lo que no ayuda a decidir se saca. En el teléfono, tocar la (i) abre una hoja chica con el texto. Lo que no se puede hacer sigue en el aviso importante, con su ⓘ.

**La campana,** en el núcleo:
- lo que llegó, lo que falló en segundo plano y lo que espera a alguien, según los permisos de quien mira. Cada aviso lleva a donde se resuelve;
- los avisos se arman al leer, desde las fuentes de cada área: no se guardan. Se guarda solo cuáles abrió cada persona;
- en el teléfono, la campana lleva a la pantalla «Avisos»;
- **la versión nueva** se avisa ahí, sin franja: la anuncia el front, va primera, y abrirla recarga la página.

**Las tablas:**
- **un dato por celda, en un renglón:** `DataTable` deja de dibujar `detail` en la computadora, y cada pantalla pasa a columnas lo que hace falta, o lo saca. En el teléfono sigue el nombre y una línea;
- la columna ordenada muestra ▲ o ▼;
- «Más filtros» en `FilterBar`, con el buscador y hasta tres filtros en una fila. Los filtros de Chats, en una fila.

**Las fechas,** siempre con fecha y hora completas, en la zona de la cuenta: salen `relative`, `formatCalendar` y las fechas cortas, también en Usuarios y en la tarjeta de Auditoría de Inicio.

**Los carteles** de las pantallas cerradas pasan a la banda o a la campana: Usuario, Cuenta y Organización de Plataforma, Legales, Recuperaciones, Inicio personal y Mi cuenta.

**Las palabras:**
- «Dar de baja» y «Reactivar» en todo el sistema, con el estado «De baja», «Dados de baja» o «Dadas de baja». Salen «Deshabilitar», «Inactivar», y en los miembros de una empresa, «Quitar» y «Volver a sumar»;
- lo dado de baja es una opción del filtro «Estado», no una vista aparte;
- «Borrar», nunca «Eliminar». Borrar la cuenta propia dice «Borrar mi cuenta», con el estado «Por borrar», en lugar de «Baja pedida». Cambian también los correos y las plantillas de WhatsApp de la cuenta.

**Otro acceso:** un link del otro acceso abre esa pantalla, que ofrece entrar con ese acceso y después abre el link.

**El tamaño general:** ya está en el front (2026-11-02): las medidas en rem y la raíz al 75 % en la compu, desde 1024 con puntero fino, sin `zoom`, que corre los menús. El teléfono y la tablet quedan igual.

**Un error del sistema real:** la pestaña «Miembros» de una empresa no se dibuja sin el permiso.

## Pantallas

Los tableros están en el lienzo v145 y al día, salvo lo que diga cada fila. **Se dibujan antes de programar solo si falta alguno:** esta etapa no suma pantallas, salvo la campana y «Otro acceso», ya aprobadas.

| Tablero | Qué muestra |
| --- | --- |
| `Campana`, `M-Campana`, `Campana-Plat`, `M-Campana-Plat` y `M-Campana-Personal` | la campana de cada acceso, con los avisos de esta etapa, y la pantalla «Avisos» del teléfono. Cada etapa posterior dibuja su versión. El número es el de lo que lista esa campana |
| `Otro-Acceso` y `Otro-Acceso-Persona`, con sus teléfonos | ofrecer entrar con el otro acceso |
| Los de cada pantalla que cambia | los textos, las tablas y los filtros, según las fichas del front |
| `Criterio-Botones`, `Criterio-Formularios`, `Criterio-Tablas`, `Criterio-Dialogos` y `Criterio-Avisos` | el botón que trabaja, la (i), la tabla que vuelve a leer, la confirmación que espera y cuándo va la ⓘ. Ya están en el lienzo v148 |

**La galería de esta etapa** son las pantallas que cambian, una vez al final.

## Contrato

| Ruta | Acceso | Pide | Devuelve |
| --- | --- | --- | --- |
| `GET /api/notifications` | los tres | — | `{items[{id, kind, params, occurredAtUtc, link, read}], unread}`, hasta 50, de lo más nuevo a lo más viejo |
| `POST /api/notifications/{id}/read` | los tres | — | 204 |

`id` es estable y sale de la fuente: un aviso que cambia, como «3 por aprobar» que pasa a 4, tiene otro id, y vuelve a contar. `kind` es una lista abierta: cada área suma el suyo, con su texto.

**El puerto** va en `Application/Interfaces/Services/`:

```csharp
public interface INotificationSource
{
    Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context, CancellationToken ct);
}
```

Copia la forma de `IPendingItemSource` (ADR 0040): lee con el alcance de quien mira y devuelve solo lo que puede ver. **Fuentes de esta etapa:** `personalLoginMethod`, `organizationPending` y `recoveryRequested`. Las etapas siguientes suman las suyas. Una fuente que falla no tumba a las demás.

**Sin errores nuevos.** Un `id` que no corresponde a un aviso de quien mira se ignora.

**Otro acceso:** cambiar de acceso suma `returnTo`, con los destinos de `ReturnUrls.cs`. No amplía los retornos autorizados.

## Datos

Una migración, `<fecha>_NotificationReads.cs`.

**NotificationReads** (global, esquema `identity`, por cuenta):

| Columna | Tipo | Regla |
| --- | --- | --- |
| `UserId`, `NotificationId` | uuid, texto ≤160 | clave |
| `ReadAtUtc` | timestamptz | — |

Se limpian los de más de 90 días, y se borran con la cuenta. **Los textos** de los resources y de las plantillas no llevan migración de datos: los estados guardados no cambian, solo cómo se dicen.

## Pasos

Abreviaturas: `A/`, `I/` y `P/` son las capas del back, y `F/`, el front.

**Back:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| B1 | La campana | `A/Interfaces/Services/INotificationSource.cs`, `A/Services/Notifications/NotificationService.cs`, `P/Controllers/Identity/NotificationsController.cs`; la tabla y su migración | `IPendingItemSource.cs`, `HomeController` | por acceso y permisos; una fuente que falla; 50; aislamiento |
| B2 | Las fuentes del núcleo | `A/Services/Notifications/*Source.cs` | `OrganizationUserPendingItems.cs` | cada una con y sin permiso |
| B3 | Baja y limpieza | suma al participante de la baja y a un worker | `AccountDeletionWorker.cs` | se borran con la cuenta |
| B4 | Los textos | `*.resx` y `.en.resx`, `Permissions.resx`, las plantillas de correo y de WhatsApp de la cuenta; `docs/operations/whatsapp-plantillas.md` | esos mismos | `ResourceParityTests`; ningún «Deshabilitar», «Inactivar», «Eliminar» ni «Baja pedida» |
| B5 | Volver al link | suma a cambiar de acceso | `ReturnUrls.cs` | `FrontendReturnUrlTests` |

**Front:**

| # | Qué hace | Archivos | Copia de | Test |
| --- | --- | --- | --- | --- |
| F1 | Botones y esperas: `button.tsx` ya pone el aro y el gerundio, y solo pierde la atenuación; `ConfirmDialog` suma `pending` y `pendingLabel`, y `DataTable` y `Table`, `refreshing` con la línea | `F/src/shared/ui/button.tsx`, `ConfirmDialog.tsx`, `DataTable.tsx`, `useEditPage` y las pantallas que lo usan | el botón de hoy | aro y gerundio sin atenuarse; la confirmación abierta hasta la respuesta, también si falla; la tabla que vuelve a leer; sin «Cambios sin guardar»; el campo se marca al tocar |
| F2 | La campana | `F/src/layouts/notifications/`, `NotificationsPage.tsx` | los tableros de la campana | sin leer; marcar; la versión nueva; el teléfono; axe |
| F3 | Tablas | `F/src/shared/ui/DataTable.tsx`, `FilterBar.tsx` y cada pantalla | los tableros «Criterio» | sin `detail` en la compu; ▲ ▼; «Más filtros» |
| F4 | Fechas | `F/src/shared/format/` y sus usos | `formatos.md` del front | `format-usage.test.ts` prohíbe `relative` y `formatCalendar` |
| F5 | Carteles | las seis pantallas | sus tableros | ninguna franja; el aviso en la banda o en la campana |
| F6 | Palabras | `locales/` y `enums/` | — | ninguna de las viejas, con una guarda de textos |
| F7 | Otro acceso | `F/src/areas/public/errors/pages/OtherAccessPage.tsx`, rutas y `returnTo` | `Otro-Acceso` | con cada acceso; un link ajeno |
| F8 | El tamaño | `F/src/index.css` y las clases en px | — | los menús no se corren; capturas de la galería |
| F9 | Miembros de una empresa | `CompanyDetailPage` | — | sin el permiso, la pestaña no aparece |
| F10 | Ayuda y motivos: la (i) con tooltip, el motivo de lo apagado y los usos de hoy de `hint` y `description`, que pasan a la (i) o se sacan | `F/src/shared/ui/InfoTip.tsx`, `help` en `FormField`, `Section`, `Page` y `CheckboxField`, y un tooltip común para lo apagado; una prueba de arquitectura | `InfoButton.tsx`, `Sheet.tsx` y `RowAction.disabledReason` | la (i) es descripción accesible y no toma el foco al abrir un diálogo; en el teléfono, la (i) abre la hoja y lo apagado avisa en la franja; la prueba frena un texto fijo en `hint` o `description`; axe |

**Al cerrar:** las fichas del front y `docs/features/` quedan al día, y `docs/architecture/backend.md` suma la campana.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E10] | Si la F5 de la 10 quedó sin hacer. | Pasó entera a esta etapa. |
| Usuario | Las plantillas de WhatsApp de la cuenta, con textos nuevos. | Las vuelve a aprobar en Meta, con el mismo nombre: **eso lo hace el usuario.** |
| Front | Cuántas pantallas usan `detail`. | Se pasan todas a columnas. |
| Front | Cuántos textos fijos quedan en `hint` y `description`. | Unos 30 usos, en 23 archivos, el 2026-11-02. |
| Otro chat | Lo que tenga sin commitear en el front. | Se mira `git status` antes de tocar. |

## Decisiones tomadas (no se vuelven a preguntar)

- **La raíz al 75 % en la compu,** decisión del usuario del 2026-11-02, como en ArquitecturaBase. Reemplaza el 85 % del 25/10.
- **Los avisos no se guardan:** salen de las fuentes, como «Para resolver», y se guarda solo qué abrió cada persona.
- **Ningún rótulo explica un botón** (43).
- **El botón que trabaja dice qué hace** (115), y **la ayuda va en la (i)**, con el aviso importante para lo que no se puede hacer (116).

## Fuera de esta etapa

- Los avisos de los formularios, que suma cada etapa.
- Avisos por correo de lo que está en la campana.
- Marcar todos como leídos.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. No hay prueba con Meta, salvo que las plantillas de la cuenta estén aprobadas antes de cerrar. El recorrido prueba: la campana con los tres accesos; una tabla, una fecha y un botón en cada área; un link del otro acceso; dar de baja y reactivar un usuario, y borrar la cuenta propia.

## Requisitos previos

Ninguno: la E10 queda para después (123).

## En el multitenant

- **Datos:** `NotificationReads` es global, por cuenta, como los métodos de ingreso. Lo que muestra cada aviso se lee con el alcance de quien mira.
- **Los tres accesos** tienen campana, cada uno con sus avisos.
- **Workers:** solo la limpieza.
- **Cuotas:** las rutas cuentan en las generales.
