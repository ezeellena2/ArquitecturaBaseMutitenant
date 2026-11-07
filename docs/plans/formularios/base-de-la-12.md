# Base de la 12 (spec)

**Estado:** pendiente · spec de lo que va antes de la E12, con el [formato](plantilla.md) de las etapas. Es lo que el usuario decidió el 25 y el 26/10 viendo la maqueta, lo que era la F5 de la 10 y lo que el traspaso 10 cambia en la app ya programada: reglas de todo el sistema, que el código de hoy todavía no cumple. Es lo primero del bloque, sin esperar a la E10 (123), y `/plan-etapa` la contrasta con el código real.

**Partes.** Los pasos, con sus archivos y sus tests, siguen en la [parte de pasos](base-de-la-12-parte-pasos.md); lo demás está acá.

## Qué leer

Antes de empezar, en este orden:

1. **Decisiones** de la hoja de ruta: la 43 (botones y avisos), la 109, la 110, la 115, la 116 y la 122 (leyendas, ayuda, motivos y esperas), y del traspaso 10, la 137, la 143 y la 144 (tablas y filtros), la 160 (lo que carga), la 161 (decidir a propósito), la 164 (lo nuevo vale para lo que ya existe), la 173 (que no parezca plantilla), la 174 (el texto largo, entero), la 175 (el estado, una vez), la 179 (el ingreso y el consentimiento por WhatsApp) y la 180 (la escala). Los pedidos del usuario del 25 y el 26/10 están en el [plan de desarrollo](../2026-09-27-plan-de-desarrollo.md#etapas-12-a-25-formularios-por-whatsapp).
2. **Fichas:** [textos-y-traducciones](../../rules/textos-y-traducciones.md), [api-http](../../rules/api-http.md), [fechas-y-zonas](../../rules/fechas-y-zonas.md), [permisos](../../rules/permisos.md), [datos-personales](../../rules/datos-personales.md), [tests](../../rules/tests.md) y, del front, `pantallas-y-ui.md`, `formatos.md`, `formularios.md`, `paginado-y-listados.md`, `textos-y-traducciones.md` y `tema.md`.
3. **Código para copiar:** la columna «Copia de» de los [pasos](base-de-la-12-parte-pasos.md).
4. **El lienzo:** `Campana`, `M-Campana`, `Campana-Plat`, `M-Campana-Plat`, `M-Campana-Personal`, `Otro-Acceso` y `Otro-Acceso-Persona`, los ocho `Criterio-*` y los tableros de cada pantalla que cambia (abajo, «Pantallas»).
5. **Referencia de producto,** solo para leer: en `../ArquitecturaBaseFront/docs/design/visual-baseline.md`, «Escala y forma», «El estado, una vez» y «Esqueletos de carga».

## Qué se construye

**Botones, esperas y ayuda** (43, 109, 110, 115, 116 y 122), en todo el sistema:
- un botón está habilitado o grisado, sin textos al lado ni debajo;
- **mientras trabaja,** pone el aro y su gerundio («Guardando…»), con su color, en su lugar y sin aceptar otro clic. Un botón de ícono cambia su dibujo por el aro;
- **una confirmación** queda abierta hasta la respuesta, con su botón en gerundio;
- **una tabla que vuelve a leer** atenúa sus filas, que no se pueden tocar, y corre una línea bajo el encabezado;
- **lo que carga no salta** (160): la primera carga de un listado dibuja su encabezado y el aro centrado en el alto de ocho filas, sin filas esqueleto; una página que espera su ruta o sus permisos muestra su banda, con el título como bloque de carga, y tres barras (`PageLoading`), y un ajuste dibuja sus zonas, sin los botones de la banda (`FormSkeleton`);
- nunca aparece «Cambios sin guardar»;
- si falta un dato obligatorio, el botón queda habilitado, y al tocarlo se marca el campo;
- **lo apagado dice por qué:** el motivo de un botón, una casilla, la ✕ de un diálogo o una opción de menú va en su tooltip, si no es obvio. En el teléfono, tocarlo lo muestra en la franja oscura de abajo;
- **sin leyendas:** el renglón de abajo es solo para el error o para un dato. La ayuda que sirve va en una (i) con tooltip, y lo que no ayuda a decidir se saca. En el teléfono, tocar la (i) abre una hoja chica con el texto. Lo que no se puede hacer sigue en el aviso importante, con su ⓘ;
- **decidir a propósito** (161): una acción que no se deshace y dispara avisos pide confirmar nombrando lo que se decide («¿Aprobar Grupo Delta?»), con la principal y el foco en «Cancelar»; el rojo lleno queda para lo que destruye. En el núcleo son aprobar o rechazar una organización o un pedido de recuperación, bloquear una página pública, dar de baja una cuenta desde Plataforma, y dar de baja o quitar a un usuario.

**El estado, una vez** (175): lo que dice la etiqueta del encabezado no se repite debajo. En Usuario y en la Cuenta y la Organización de Plataforma sale el cartel que repite el estado («Pidió la baja de su cuenta…», «Suspendida el …»): la etiqueta lo dice una vez, cuándo y por qué van en una frase en su ⓘ («El 03/11/2026. Motivo: falta de pago.»), y en el teléfono se va el renglón «Estado» que lo repetía.

**La campana,** en el núcleo:
- lo que llegó, lo que falló en segundo plano y lo que espera a alguien, según los permisos de quien mira. Cada aviso lleva a donde se resuelve;
- los avisos se arman al leer, desde las fuentes de cada área: no se guardan. Se guarda solo cuáles abrió cada persona;
- en el teléfono, la campana lleva a la pantalla «Avisos»;
- **la versión nueva** se avisa ahí, sin franja: la anuncia el front, va primera, y abrirla recarga la página;
- sus filtros nacen sin cantidades (143 y 164).

**Las tablas:**
- **un dato por celda:** `DataTable` deja de dibujar `detail` en la computadora, y cada pantalla pasa a columnas lo que hace falta, o lo saca. En el teléfono sigue el nombre y una línea;
- **el texto largo se ve entero** (174): la celda baja de renglón en lugar de cortar con «…» y su `title`. Un código con guiones («30-71458236-9») no se parte, y una zona horaria se corta solo en sus barras;
- la columna ordenada muestra ▲ o ▼;
- **una línea fina entre columnas,** también en una columna fija al desplazar, los números a la derecha y la tabla que cierra abajo (137);
- **los filtros** (143, 144 y 164): el buscador y hasta tres filtros en una fila, sin botón para aplicar, y lo demás en un solo «Filtros», nunca «Más filtros», que se despliega como «Columnas» y aplica cada cambio, con lo aplicado en chips y «Limpiar todo». Un filtro de opciones es un desplegable simple, sin cantidades y con la elegida tildada; un segmentado conserva su número. Hoy muestran la cantidad Usuarios, Roles, Cuentas, Organizaciones, Empresas, los miembros de una empresa y Recuperaciones, en la compu y en el teléfono. Los filtros de Chats, en una fila;
- **lo elegido** (142): el segmentado rellena la elegida con la marca, una tarjeta elegida suma un borde de 2 px, un desplegable de filtro tilda la elegida, y la fila abierta y la conversación abierta llevan una barra de 3 px. En el teléfono, cada opción de un segmentado mide 44 × 44.

**Que no parezca plantilla** (173), en seis piezas del front, con la paleta Pradera:
- **la marca** es el nombre, sin el cuadrado con degradé;
- **el avatar** es un cuadrado chico con la inicial, sin círculo ni degradé;
- **`Page`** no tiene ícono: sale el cuadradito de la banda;
- **el activo del menú** lleva una barra de la marca y su peso, no la pastilla blanca con sombra;
- **`FilterBar`:** los filtros, las etiquetas y los contadores, con el radio de un control, sin pastillas;
- **el botón principal** es sólido, sin degradé ni sombra.

Las sombras quedan solo en menús, desplegables y tooltips: salen todas las demás, como las de las tarjetas, las superficies, los diálogos, los segmentados y los botones.

**La escala** (180), sobre los tokens de tamaño y junto con la forma:
- la letra, un 18 % más chica, sin bajar de 10,5 px en pantalla; el cuerpo queda en unos 11 px;
- las filas, los controles y las bandas, un 26 % más bajos: una fila de tabla, unos 33 px; un botón o un filtro, de 24 a 27 px, y la barra de arriba, unos 39 px;
- los rellenos y las separaciones, un 28 % más chicos;
- las esquinas, de 3 px como mucho, también en las pastillas y los avatares, y sin sombras.

Las medidas concretas las fija `tema.md` del front, que deja la sesión de las piezas del lienzo, y se comparan con los tableros publicados. La raíz sigue al 75 % en la compu, con las medidas en rem (2026-11-02): lo que se ve en pantalla tiene que dar lo que muestra el tablero. En el teléfono, lo que se toca sigue midiendo 44 × 44.

**Las fechas,** siempre con fecha y hora completas, en la zona de la cuenta: salen `relative`, `formatCalendar` y las fechas cortas, también en Usuarios y en la tarjeta de Auditoría de Inicio.

**Los carteles** de las pantallas cerradas pasan a la banda o a la campana: Usuario, Cuenta y Organización de Plataforma, Legales, Recuperaciones, Inicio personal y Mi cuenta.

**Las palabras:**
- «Dar de baja» y «Reactivar» en todo el sistema, con el estado «De baja», «Dados de baja» o «Dadas de baja». Salen «Deshabilitar», «Inactivar», y en los miembros de una empresa, «Quitar» y «Volver a sumar»;
- lo dado de baja es una opción del filtro «Estado», no una vista aparte;
- «Borrar», nunca «Eliminar». Borrar la cuenta propia dice «Borrar mi cuenta», con el estado «Por borrar», en lugar de «Baja pedida». Cambian también los correos y las plantillas de WhatsApp de la cuenta;
- las altas dicen «Agregar …»: en Usuario, «Agregar empresa» y, en su diálogo, «Agregar», en lugar de «Sumar empresa» y «Sumar».

**El WhatsApp de la cuenta** (179):
- **ningún mensaje lleva un enlace secreto.** El bot de Plataforma deja de mandar el enlace de un solo uso de la E8: «Entrar a mi cuenta» es un botón que lleva al ingreso (`/login`), donde el código se pide desde la web. Con las consultas apagadas, cualquier mensaje recibe ese botón. Se retiran el canje de enlaces, su página `/login/enlace` y su tabla;
- **«Cargar teléfonos» pide marcar «La persona aceptó recibir mensajes por WhatsApp»** para invitar: sin la casilla no sale ninguna invitación, y cada invitación guarda quién lo confirmó y cuándo, como en ArquitecturaBase.

**Otro acceso:** un link del otro acceso abre esa pantalla, que ofrece entrar con ese acceso y después abre el link.

**Un error del sistema real:** la pestaña «Miembros» de una empresa no se dibuja sin el permiso.

**Lo técnico:**
- `Logging:Debug` en `Warning` en el `appsettings.Development.json` de la Api: con el depurador enganchado, el log Debug la frena;
- un test mide cada cadena de `AGENTS.md`, desde la raíz hasta cada carpeta, y la deja por debajo de 30 KB (la más larga mide hoy unos 16,6 KB);
- `frame-src` no cambia: `https://*.facebook.com`, de `MetaContentSecurityPolicySource`, ya cubre `https://business.facebook.com`, y la E14 lo prueba con la vista oficial;
- **sin worktrees ni ramas:** la puerta y `realinear-docs` trabajan sobre una copia de `HEAD` en `.local/`. La puerta ya lo hace desde el 2026-11-07; falta la corrida completa del arnés, que hoy arma un worktree en la rama `arnes/<arnés>/<fecha>`. Copia, verifica y commitea en la copia; fusionar trae ese commit a `main` con `--ff-only`, sin crear una rama;
- **las capturas suman 320 de ancho:** cada pantalla nueva o cambiada se ve entera, sin desplazamiento lateral, a 1440, 390 y 320;
- **«Desvincular» sin WhatsApp no da 404 acá** (revisado el 2026-11-07): en «Mi cuenta» es el de Google, del núcleo, y sin el canal los teléfonos no aparecen; el de «Mensajes» es del módulo y se va con él.

## Pantallas

Los tableros del 25 y el 26/10 están en el lienzo v145 y al día, salvo lo que diga cada fila; la campana y «Otro acceso» ya están aprobados. **Los del traspaso 10 se redibujan antes de programar,** con el pase de las piezas compartidas del lienzo, y se aprueban en el lienzo.

| Tablero | Qué muestra |
| --- | --- |
| `Campana`, `M-Campana`, `Campana-Plat`, `M-Campana-Plat` y `M-Campana-Personal` | la campana de cada acceso, con los avisos de esta etapa, y la pantalla «Avisos» del teléfono. Cada etapa posterior dibuja su versión. El número es el de lo que lista esa campana. Se redibujan sin cantidades en sus filtros (164) |
| `Otro-Acceso` y `Otro-Acceso-Persona`, con sus teléfonos | ofrecer entrar con el otro acceso |
| `Usuarios`, `Roles`, `Cuentas`, `Organizaciones`, `Empresas`, `Empresa`, `Recuperaciones` y `Usuario`, con sus teléfonos, y `M-Usuario-Empresa` | se redibujan: los filtros sin cantidades, el estado una vez, «Agregar empresa», la forma y la escala |
| `Plat-WA-Bot`, `Plat-WA-Bot-Recorrido` y `M-Plat-WA-Bot` | se redibujan: el bot sin enlace, con «Entrar a mi cuenta» (179) |
| `WA-Telefonos` y `M-WA-Telefonos` | se redibujan: «Cargar teléfonos» con la casilla del consentimiento (179) |
| `Enlace` y `M-Enlace` | se retiran: la página del enlace de un solo uso (179) |
| `Criterio-Botones`, `Criterio-Formularios`, `Criterio-Tablas`, `Criterio-Dialogos`, `Criterio-Avisos`, `Criterio-Marco`, `Criterio-Menus` y `Criterio-Decisiones` | el botón que trabaja, la (i), la tabla que vuelve a leer, la confirmación que espera y cuándo va la ⓘ, en el lienzo v148, con las reglas del traspaso 5 en la v159. Se redibujan con las del traspaso 10: la forma, la escala, la primera carga, decidir a propósito, el estado una vez y el texto entero |

**La galería de esta etapa** son las pantallas que cambian, una vez al final, a 1440, 390 y 320.

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

Copia la forma de `IPendingItemSource` (ADR 0040): lee con el alcance de quien mira y devuelve solo lo que puede ver. **Fuentes de esta etapa:** `personalLoginMethod`, `organizationPending` y `recoveryRequested`. Las etapas siguientes suman las suyas. Una fuente que falla no tumba a las demás. Un `id` que no corresponde a un aviso de quien mira se ignora.

**Otro acceso:** cambiar de acceso suma `returnTo`, con los destinos de `ReturnUrls.cs`. No amplía los retornos autorizados.

**Rutas que cambian** (179):
- `POST /api/organization/whatsapp/phone-imports` suma `consentConfirmed`, obligatorio en `true`; la vista previa no lo pide;
- salen `POST /api/auth/login-link/preview` y `POST /api/auth/login-link/redeem`, de la E8, con su verificador de repetición.

**Un error nuevo:** `WhatsApp.PhoneImport.ConsentRequired` (Validation, campo `consentConfirmed`), «Confirmá que la persona aceptó recibir mensajes por WhatsApp.».

## Datos

Tres migraciones:
- **`<fecha>_NotificationReads.cs`:** `NotificationReads`, global, en el esquema `identity` y por cuenta: `UserId` y `NotificationId` (uuid y texto ≤160) como clave, y `ReadAtUtc`. Se limpian los de más de 90 días, y se borran con la cuenta;
- **el consentimiento:** la invitación que arma `InviteByPhoneAsync` suma `ConsentConfirmedByUserId` y `ConsentConfirmedAtUtc`, nulos en las invitaciones por correo y en las anteriores;
- **sin enlaces:** se borra la tabla `LoginLinks` de la E8, con su vuelta atrás.

**Los textos** de los resources y de las plantillas no llevan migración de datos: los estados guardados no cambian, solo cómo se dicen.

## Qué verificar al arrancar

| Marca | Qué hay que confirmar | Qué asume esta spec |
| --- | --- | --- |
| [E10] | Si la F5 de la 10 quedó sin hacer. | Pasó entera a esta etapa. |
| Usuario | Las plantillas de WhatsApp de la cuenta, con textos nuevos. | Las vuelve a aprobar en Meta, con el mismo nombre: **eso lo hace el usuario.** |
| Front | Cuántas pantallas usan `detail`. | Se pasan todas a columnas. |
| Front | Cuántos textos fijos quedan en `hint` y `description`. | Unos 30 usos, en 23 archivos, el 2026-11-02. |
| Front | Las seis piezas de la forma, la escala de `index.css` y las sombras. | Las del 2026-11-07: `BrandMark` con degradé, `Avatar` redondo, `icon` en 38 pantallas, la pastilla de `menuRow.ts`, las cantidades de `FilterBar` y el degradé de `button.tsx`. |
| Front | Las medidas de la escala en `tema.md`. | Las deja la sesión de las piezas del lienzo; la tarea las toma de ahí y las compara con los tableros publicados. |
| Núcleo | Que el enlace de un solo uso lo use solo el bot. | `LoginLinkIssuer` exige un teléfono verificado y no lo usa nadie más (2026-11-07). |
| Núcleo | La invitación que arma `InviteByPhoneAsync`. | Ahí se guarda el consentimiento. |
| Front | «Desvincular» sin WhatsApp. | Como lo revisado el 2026-11-07 («Lo técnico»). |
| Otro chat | Lo que tenga sin commitear en el front. | Se mira `git status` antes de tocar. |

## Decisiones tomadas (no se vuelven a preguntar)

- **La raíz al 75 % en la compu,** decisión del usuario del 2026-11-02, como en ArquitecturaBase. Reemplaza el 85 % del 25/10. Sobre ella va la escala de la 180.
- **Los avisos no se guardan:** salen de las fuentes, como «Para resolver», y se guarda solo qué abrió cada persona.
- **Ningún rótulo explica un botón** (43).
- **El botón que trabaja dice qué hace** (115), y **la ayuda va en la (i)**, con el aviso importante para lo que no se puede hacer (116).
- **La forma y la escala de ArquitecturaBase,** con la paleta Pradera (173 y 180).
- **Ningún mensaje lleva un enlace secreto,** y el consentimiento queda guardado con la invitación (179).
- **Sin worktrees ni ramas,** también en el arnés: pedido del usuario del 2026-11-07, que ya está en `AGENTS.md`.

## Fuera de esta etapa

- Los avisos de los formularios, que suma cada etapa.
- Avisos por correo de lo que está en la campana.
- Marcar todos como leídos.
- Lo de WhatsApp que allá existe y acá no se pidió: el estado de entrega de una invitación, el historial de accesos y desvincular un teléfono robado.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`. No hay prueba con Meta, salvo que las plantillas de la cuenta estén aprobadas antes de cerrar; el botón «Entrar a mi cuenta» del bot se abre igual en un celular. El recorrido prueba: la campana con los tres accesos; una tabla, una fecha y un botón en cada área; un link del otro acceso; dar de baja y reactivar un usuario, y borrar la cuenta propia; «Cargar teléfonos» sin la casilla y con ella; y una pantalla de cada área a 320.

## Requisitos previos

Ninguno: la E10 queda para después (123).

## En el multitenant

- **Datos:** `NotificationReads` es global, por cuenta, como los métodos de ingreso. Lo que muestra cada aviso se lee con el alcance de quien mira. El consentimiento queda en la invitación de la organización.
- **Los tres accesos** tienen campana, cada uno con sus avisos.
- **Workers:** solo la limpieza.
- **Cuotas:** las rutas cuentan en las generales.
