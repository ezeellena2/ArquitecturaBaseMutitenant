# Alineación lienzo ↔ código ↔ documentación · Etapas 0 a 6 (2026-10-03)

**Alcance.** Back `a12edb7`, front `44bf951`. Etapas cerradas: 0, 1, 2, 3a, 3b, 3c, 4 y 6. Lienzo vigente: `docs/design/lienzo/` del front, **versión 71**, 152 tableros. Lo que manda son los tableros sin marca (validados) y los 8 `Criterio-*`. La página «archivo» no cuenta.

**Método.** Cuatro revisiones de solo lectura, en paralelo y con áreas separadas:
1. ingreso, cuenta e invitaciones;
2. Organización en escritorio, con la API;
3. teléfonos y marco;
4. documentación y criterio en el código compartido.

Los puntos marcados ✔ los volví a verificar en el código.

**Seguimiento de ejecución (03/10):** ✔ identifica un hallazgo comprobado por el informe original; no significa que esté corregido. Fuente temporal copiada en `cc2835b`. Corrección 0.7: Usuarios devuelve ahora `Tenancy.Member.LastTenantAdmin` («Tiene que quedar al menos un Dueño activo.») y `Tenancy.Member.CannotDisableSelf`; la baja de cuenta conserva su error legal. Verificación: rojo focal y luego Usuarios 3/3, baja de cuenta 6/6, recursos 6/6, build Api sin advertencias. El front del Paquete 0 lo está corrigiendo otro chat; sus commits no se atribuyen a este cambio.

**Rutas:** F = repo del front, B = repo del back.

## Resumen

| Área | Alineación aprox. | Lo principal |
|---|---|---|
| Ingreso, registro, cuenta e invitaciones (escritorio) | 80 % | Términos y entradas a «Registrá tu empresa» siguen como en el lienzo viejo (v35); a Invitación le falta lo que destrabó la E6. |
| Organización en escritorio | 60 % | Usuario, Página pública y Verificar dominio no siguen el tablero; textos y avisos al 50 %. |
| Marco de escritorio (menú, panel, migas, banda) | 75 % | Panel y migas no marcan dónde estás; la banda de Empresa muestra datos crudos. |
| Teléfonos | 35 % contra los tableros, 10 % contra el criterio de app | Ninguna de las 14 pantallas de Organización sigue el formato app. Tres se ven rotas a 390 px. |
| Documentación | — | frontend.md, responsive.md, formularios.md, tema.md, arbol.md, el plan y los AGENTS.md describen reglas que el lienzo ya archivó. |

## Por qué se desalineó (proceso)

1. **Se comparó contra lienzos viejos.** Las capturas de la 3a y la 3b usan `lienzo-v35`, y las de la E6 la v64, anterior al teléfono como app. El lienzo vigente refinó Ingreso y registro (v56), sumó el criterio y pasó los teléfonos a app. Ninguna etapa del plan lleva esos cambios al código ya hecho.
2. **Lo oculto «hasta E4/E6» no se volvió a mostrar** al cerrar esas etapas: «Registrá tu empresa», Empresa y Roles en la invitación, y el bloqueo de la única Dueña.
3. **Nadie miró las capturas.** Codex generó los pares y justificó diferencias que contradicen decisiones del usuario. Ejemplos: el subtítulo de Auditoría, y «alineado» en comparacion.md:17 cuando no lo estaba.
4. **Los docs que lee quien programa están desactualizados:** botones de 34, filas de 42, pastillas, web achicada y «lienzo v64/v65». Codex los siguió.

## Paquete 0 · Errores visibles, rápidos (S a M)

| # | Qué | Dónde | Corrección |
|---|---|---|---|
| 0.1 ✔ | Textos crudos en pantalla: «states.saving», «back» y «empty.missing» no existen en los locales | F/src/areas/business/users/pages/UserDetailPage.tsx:85-87,156,216,235 · UsersPage.tsx:233 · CompaniesPage.tsx:221 · CompanyDetailPage.tsx:259,277,304 | Crear las claves en es y en, con un test que falle si se usa una clave inexistente. |
| 0.2 ✔ | Un clic afuera cierra los diálogos (Radix por defecto); el criterio lo prohíbe | F/src/shared/ui/dialog.tsx:50-73 | Prevenir `onInteractOutside` en `DialogContent`, con test. |
| 0.3 | A 390 px, Configuración, Ficha de empresa y Editar usuario muestran el título cortado («C..», «D.», «T..») y los botones apretados en la banda | SettingsPage, CompanyDetailPage, UserDetailPage (capturas de etapa-6 a 390) | Arreglo rápido ya; el definitivo va en el paquete 4. |
| 0.4 ✔ | No hay ninguna entrada a «Registrá tu empresa»: la puerta Empresa, «sin empresa», «acceso deshabilitado», Crear cuenta y la portada no tienen el enlace; solo se llega escribiendo la URL. Los tests exigen que no esté | LoginPage.tsx:174-182,200-202 · SignupPage.tsx:126-128 · LandingPage.tsx:29,36,40 · tests LoginPage.test.tsx:124,136,266, SignupPage.test.tsx:47, LandingPage.test.tsx:35 | Sumar las entradas del lienzo e invertir esos tests (frontend.md:101,159 ya lo pide). |
| 0.5 ✔ | Términos como en v35: Google, «Crear cuenta», «Continuar» y «Aceptar y seguir» siguen apagados hasta marcar la casilla. En Registro-Empresa, el error rojo se ve desde que abre la pantalla | SignupPage.tsx:117,124 · GoogleButton.tsx:57 · BusinessSignupPage.tsx:229,236 · AcceptTermsPage.tsx:57-59 | Botones habilitados; al tocar sin marcar, «Aceptá los Términos y la Política de privacidad.» bajo la casilla, siempre desde i18n. Agregar `accept.acceptRequired`. |
| 0.6 | La banda de Empresa dice «AR-CUIT 30712345678 · America/Argentina/Buenos_Aires · Activa» | CompanyDetailPage.tsx:200-201 | «● Activa · CUIT 30-71234567-8 · Buenos Aires (GMT−3)», con `TaxIdText`, `TimeZoneText` y el estado primero. |
| 0.7 | El último Dueño responde con el texto de la baja de cuenta («Antes de pedir la baja…») | B/…/OrganizationUserAdministratorGuard.cs:29 → AccountDeletionPolicy.cs:15 · Errors.resx:53,140 | Crear un error propio (por ejemplo `Users.Member.LastTenantAdmin`) con «Tiene que quedar al menos un Dueño activo.», y «No podés deshabilitarte a vos misma.». |
| 0.8 | «Espera aprobación» sale «…te avisamos a  cuando…»: el correo está vacío si se entró con Google o con sesión | BusinessSignupPage.tsx:41,220 | Tomar el correo de /api/me, o que la API lo devuelva. |

## Paquete 1 · Pantallas de escritorio contra su tablero (M a L)

| # | Tablero | Diferencia | Dónde |
|---|---|---|---|
| 1.1 | Usuario | El tablero tiene la caja «Acceso a la organización» con Estado + roles, ⋮ «Más acciones», y «Descartar cambios» y «Guardar cambios» apagado sin cambios. El código tiene botones sueltos en la banda (Deshabilitar, Revocar y Quitar en rojo relleno), datos de solo lectura y Guardar solo con cambios. La API separa el estado (`PUT …/status`) y no da la fecha de eliminación | UserDetailPage.tsx:156-216 · B/…/OrganizationUsersHttpRequests.cs:20-28 · OrganizationUsersController.cs:112-123 |
| 1.2 | Configuración: Dominio y Verificar | Falta el diálogo «Verificar delta.ejemplo.com» con el TXT, «Copiar» y «Comprobar» después de Agregar. El código guarda, cierra y pone el TXT en la fila | SettingsPage.tsx:116-136 |
| 1.3 | Página pública y Despublicar | Falta la caja «Publicación» con el interruptor (trabado con ⓘ si está despublicada). El Logo va en la caja «Página»; el teléfono, como país + número. Falta el aviso importante con motivo, fecha, «Qué podés hacer» y «Entendido». El código tiene Publicar y Despublicar en la banda, otras cajas, teléfono en texto libre y el aviso sin fecha | PublicPagePage.tsx:139-215 · B/…/PublicPageHttpRequests.cs:10,21-24 |
| 1.4 | Invitación (pantalla y correo) | Faltan quién invita con su rol («Lucía Fernández · Dueño · hace 1 día»), Empresa y Roles (en pantalla y en el correo). Sobra la casilla de términos: el tablero tiene la leyenda «Al aceptar creamos tu cuenta…» o «Grupo Delta se suma a tus perfiles.». Sobra la pantalla intermedia después de aceptar. El panel lateral no es el de Ingreso | InvitationPage.tsx:24-41,163-226 · B/…/InvitationPreviewResponse.cs:6-9 · InvitationNotice.cs · Templates/Invitation.html |
| 1.5 | Mi cuenta: única Dueña | El tablero tiene la ⓘ y el botón apagado, que abren «No podés dar de baja tu cuenta» con «Qué podés hacer». El código lo deja activo y el 409 llega recién al final | AccountPage.tsx:74-78 · AccountLoginMethodsResponse.cs |
| 1.6 | Rol y Roles | «Vale en» debe ofrecer «Toda la organización» o cada empresa por nombre, y el filtro igual. El ⋮ de un rol propio lleva solo «Editar» e «Inactivar» | RoleEditorPage.tsx:211,232-250 · RolesPage.tsx:99-100,133-145 |
| 1.7 | Usuarios: Invitar | Una sola empresa, roles opcionales, botón «Invitar» sin ícono, sin bajada ni ayudas | UsersPage.tsx:200-235 |
| 1.8 | Empresa y Empresas | Banda: «Editar empresa», más «Agregar miembro» o «Crear rol» según la pestaña. La pestaña Roles con Rol, Usuarios, Permisos y ⋮. «Quitar de la empresa» también con baja pedida. «Volver a sumar» directo. En Empresas, el ⋮ lleva «Inactivar» en rojo después de la franja | CompanyDetailPage.tsx:57,182,190-204,233 · CompaniesPage.tsx:160 |
| 1.9 | Usuarios (listado) | Columna Empresa: la primera más «y N más», con los roles debajo. Filtro con «Sin empresa» y conteos. «N invitaciones» cuenta pendientes y vencidas | users/columns.tsx:26-32 · UsersPage.tsx:133,176 |
| 1.10 | Auditoría | Tocar «Qué pasó» abre el detalle, sin el ojo. El detalle va con fecha, Quién (nombre · correo), Dónde, «antes → después» y «Cerrar». Sin subtítulo en la banda | AuditPage.tsx:63,70,120-142 · B/…/OrganizationAuditService.cs:138 |
| 1.11 | Marco: migas y panel Administración | Las migas resuelven solo Gestión de usuarios: Empresas, Empresa, Configuración, Página pública y Auditoría muestran solo «Inicio». El panel arranca cerrado aunque la ruta sea de administración (F5 en /org/usuarios), no marca la sección activa y no muestra íconos | Breadcrumbs.tsx:10-30 · AdminPanel.tsx:14-36 · PrivateLayoutFrame.tsx:41 |
| 1.12 | Inicio-Personal | Falta la banda «Inicio». «Agregar» abre el diálogo de Cuenta-Agregar, no navega a /cuenta | PersonalHomePage.tsx:4-7 · PersonalLoginMethodNotice.tsx:9 |
| 1.13 | Registro-Empresa | Faltan los textos del tablero: cerrado, límite («…3 organizaciones propias…», en rojo), «Escribí el nombre de…», «Revisá el CUIT: son 11 números.», el panel «Quedás como Dueño…» y los asteriscos. El enlace «¿Tu empresa ya está registrada?» va solo en el paso Correo | auth.json:83-113 · BusinessSignupPage.tsx:260-291 · AuthLayout.tsx:63 |

## Paquete 2 · Criterio en las piezas compartidas (M)

Todo en `F/src/shared/ui`; arregla muchas pantallas de una vez.

- **Botones:** solo principal, secundario, peligro con borde (relleno solo en el diálogo que confirma) y enlace. Quitar `secondary`, `ghost`, `outlineSurface` con sombra, `xs`, `icon-xs` e `icon-lg`. ‹ y ⋮ a 32. Sin íconos de adorno salvo el «+». Se oculta lo que no aplica; solo «Guardar cambios» se apaga. Decidir el «Verificar» apagado del código (ver decisión 2). (button.tsx:10-33, Page.tsx:57,87)
- **Menús:** opción de 36, ancho mínimo de 220, una línea fina entre opciones y una franja antes de las de peligro, ícono gris en cada opción, sin opciones grises. (dropdown-menu.tsx:44-76,172 · RowActions.tsx:51 · AccessMenu.tsx:96,131-141)
- **Avisos y banda:**
  - `Page` con lugar para un cartel *debajo* de la banda (hoy el `ConcurrencyBanner` queda encima);
  - estado primero en la segunda línea, con punto y palabra;
  - «Cambios sin guardar» y «¿Salir sin guardar?» en todas las fichas;
  - 5xx con «Copiar código» y 429 con cuenta regresiva en el botón;
  - Página pública también con `ConcurrencyBanner`.
  (Page.tsx:50-103 · AppShell.tsx:70-74)
- **Formularios:**
  - dos columnas iguales a todo el ancho de la caja (regla del 02/10, que reemplazó «ancho de su dato»): Rol y «Tus datos» hoy usan 360/260/720 fijos;
  - código de 44 con radio 8, asterisco rojo, `--radius-control` a 8;
  - sin permiso, mostrar texto en lugar de campos grises;
  - un solo control por campo (Configuración hoy muestra buscador + lista nativa).
  (RoleEditorPage.tsx:226-252 · AccountProfileForm.tsx:78-81 · OtpInput.tsx:215 · index.css:43-45,153 · FormField.tsx:91 · SettingsPage.tsx:95-98)
- **Diálogos:** borrar el maquillaje `.account-dialog` (index.css:141-165) y usar el `Dialog` compartido: encabezado arena de 48, título de 15, sin bajada, sin desenfoque.
- **Estados y listas:**
  - estado = punto + palabra, sin pastillas: borrar `StatusDot`/`VerificationBadge` sin uso y la pastilla de UserDetailPage.tsx:156;
  - pieza `List` compartida (hasta 10 elementos, sin encabezado, filas de 44 o 56) frente a `DataTable`;
  - encabezado de tabla con alto fijo de 40 (table.tsx:70).
- **Sin textos explicativos:** quitar bajadas y ayudas.
  - Auditoría: «Acciones realizadas…».
  - Usuario: «Esta persona pidió la baja…», «Los roles quedan…», «Correo administrado…».
  - Diálogos: «Enviamos un enlace…», «Completá los datos…», «Verificá que administrás…».
  - Ayudas: «Podés asignar roles…», «Elegí una dirección…».
  - Mi cuenta: «Tenés {{days}} días para arrepentirte.».
  - Sumar a shared/ui el ⓘ y el diálogo «Aviso importante» (hoy PublicPagePage arma el suyo).

**Agregado al cerrar los paquetes 1 y 3 (2026-10-03).** Estos puntos van con el Paquete 2, en el front de la Etapa 5:
- **AuthLayout:** el kicker «Sin contraseñas» como texto plano, y una prop para el párrafo del panel de Registro-Empresa. Con eso cierra la parte de front del punto 1.13; el correo de ventas sale de la configuración de la plataforma (decisión 6).
- **PhoneField** compacto: «AR +54» más el número.
- **Banner** sin borde, y lugar para «Todos» en **FormField**.
- **Botón de peligro** con borde, y un aviso importante compartido.
- **FilterBar** con un texto propio para el botón vacío («Vale en: todos», con «Todos» marcado).
- **Tabs:** píldora con el número.
- **`AddLoginMethodDialog`** compartido, para que «Agregar» del Inicio-Personal abra Cuenta-Agregar (cierra el punto 1.12).

## Paquete 3 · Textos es/en (S a M)

- **Locales del front:**
  - Avisos en pasado y plural, con el nombre: «Guardamos la configuración.», «Invitamos a …», «Deshabilitamos a Tomás Acosta.».
  - Confirmaciones del tablero:
    - «¿Inactivar el rol X?»;
    - «No se puede inactivar: 1 usuario tiene el rol X.», con el singular bien;
    - «¿Quitar a X de Grupo Delta?» con su frase de correos;
    - «Revocar»;
    - «¿Quitar delta.ejemplo.com?»;
    - «¿Despublicar la página?».
  - Rótulos:
    - «Invitar usuario»;
    - «Editar»;
    - «Quitar de la empresa»;
    - «Editar empresa»;
    - «Miembro»;
    - «Idioma y región predeterminados»;
    - «Pendiente»;
    - «Sin dominio»;
    - «Agregar dominio»;
    - «Fecha y hora»;
    - «Buscar en la auditoría»;
    - «Página» y «Contacto»;
    - vacíos con «Limpiar filtros»;
    - «3 empresas · 3 activas».
  (detalle en la revisión de Organización, puntos 14 a 16)
- **Permisos (back, Permissions.resx es/en):** «Ver la configuración», «Cambiar la configuración», «Ver la auditoría» y el área «Miembros» con «Ver miembros» y «Administrar miembros». Rol Dueño: «Tiene todos los permisos de la organización.» (hoy sale «Sin descripción»: el front busca «TenantAdmin»).
- **Correos (Notifications.resx):**
  - «email» → «correo» en el texto y en el pie fijo;
  - «Membresía terminada» con el texto y el botón «Revisar mis métodos» del tablero Mensajes;
  - pie de la invitación completo.
- **Invitación (invitations.json):**
  - panel lateral del Ingreso;
  - sin el antetítulo;
  - «…Entrá con tu código y la invitación te espera.»;
  - «Agregá un correo personal o tu WhatsApp…».

## Paquete 4 · El teléfono como app (L, antes de E5, E7 y E11)

Hoy, a 390 px, todo es la web achicada:
- arriba, ☰ + marca + avatar, con la banda debajo;
- un cajón lateral;
- tablas con encabezado;
- hojas beige con botones de 34.

Ninguna pantalla tiene barra de abajo, «+» flotante, filas de borde a borde, grupos de ajustes, `safe-area` ni `theme-color`. Hay 11 tests que fijan la web achicada.

Orden propuesto:
1. **Lienzo primero (diseño, con el usuario, de a una pantalla):**
   - corregir Criterio-Menus y Criterio-Botones, que todavía dicen ☰ en el teléfono y contradicen a Criterio-Marco;
   - dibujar como app M-Inicio-Personal, M-Cuenta y sus hojas;
   - un «Más» personal y uno de Organización sin WhatsApp;
   - la hoja de filtros y el paginado en el teléfono («Cargar más»);
   - Error-Org, Perfil-Suspendido y Avisos en el teléfono;
   - decidir si las pantallas de Ingreso conservan la marca (propuesta: sí, con tamaños de app).
   Los teléfonos de Plataforma se dibujan antes de la E5.
2. **Marco de app compartido:** `AppBar`, `BottomNav` con «Más», `Fab`, `BottomSheet` (manija, título de 18, botones de 48, confirmación sin ✕), `FixedActions`, `ListRow` (3 líneas, ⋮ opcional), `SettingsGroup`, tokens de teléfono y zonas seguras. Todo sale de la misma declaración que el escritorio, sin pantallas duplicadas.
3. Navegación (`PrivateLayoutFrame`, «Más» desde `navigation/*.ts`) y un E2E a 390.
4. Hojas: todos los diálogos a la vez.
5. Listas: `DataTable` con `mobileRow`, filtros con íconos y hoja. Orden: Usuarios, Roles, Empresas, Empresa y Auditoría.
6. Edición: ‹ + título de 20, ⋮ en la barra y acción fija, en Usuario, Rol, Configuración y Página pública.
7. Perfil personal e Ingreso, según lo dibujado en el paso 1.
8. Docs, tests y capturas nuevas a 390×844 contra los M-* vigentes.

**Agregado (lienzo v78, front `f2eff7d`, `generadores/faltan_1004.py`).** Estos teléfonos van con el marco de app, en el front de la Etapa 5:
- **M-Sesion:** 5 estados, como el arranque de la app.
- **M-Perfil-Suspendido:** 3 estados.
- **M-Error-Org:** 2 estados, con la barra de abajo del miembro (Inicio y Más).
- **M-Avisos:** 9 estados, sobre M-Configuracion.
  - Lo pasajero va en una franja oscura abajo.
  - Lo que sigue va debajo de la barra.
  - «¿Salir sin guardar?» va en una hoja sin ✕.
  - «Reintentá en 0:39» va en el botón fijo.

## Paquete 5 · Documentación (M)

- **Front:**
  - reescribir desde los 8 Criterio-* §4 «UI y pantallas» y «Teléfono» de frontend.md (o dejar solo un puntero), responsive.md, formularios.md:15,27, tema.md:16,39-47,62, arbol.md:92,113,117,440,458-462, pantallas-y-ui.md («8 tableros», v71, el ⓘ y la regla lista/tabla) y paginado-y-listados.md;
  - copiar la navegación aprobada de Plataforma a frontend.md:84-85;
  - fijar las rutas de E11 y del directorio;
  - en los AGENTS.md (raíz, carpetas de áreas, shared/ui y layouts): «lienzo vigente» sin número de versión, «solo tableros validados», «el teléfono es una app», «mandan los 8 Criterio-*» y «el detalle va en ⓘ»;
  - corregir «M-Landing» → Landing-Movil, que están «por refinar», y «v60/v64/v65» en cada carpeta.
- **Back:**
  - el plan maestro pasa a v71, con una columna de estado por etapa;
  - un paso «refinar y aprobar» antes de programar E5, E7 y la parte de plataforma de E11;
  - una tarea nueva «teléfono como app» (paquete 4);
  - la E5 se describe por datos y acciones, sin fijar pestañas;
  - aclarar que lo aprobado es WhatsApp de organización y que «Plataforma › WhatsApp» falta dibujar;
  - corregir multitenancy.md:35 (suspendida: gris con motivo, y al tocarla abre la explicación), backend.md:335-336 (las rutas reales son `api/organization/*`), identidad.md:47,51,63, roles.md:32 y estandares.md P10;
  - B/AGENTS.md: una fila «si vas a hacer una pantalla» que lleve al README del lienzo.
- **Lienzo:**
  - README: los teléfonos de Perfil personal pasan a «· por refinar»;
  - Inicio-Org e Inicio-Miembro, «para repensar»;
  - nota `guia` del mapa;
  - línea 75 (listas);
  - tres scripts de menú sin listar.
- **Informes de capturas:** corregir las justificaciones vencidas (3a: «hasta E6», botón apagado; E6: subtítulo de Auditoría, «alineado» de comparacion.md:17) y volver a capturar contra la v71.

## Paquete 6 · Proceso (para que no vuelva a pasar)

1. **Las capturas se comparan siempre contra `docs/design/lienzo/` vigente.** `lienzo-v35` solo se usa para reproducir capturas viejas. Un test del script de capturas que falle si una etapa nueva apunta a otra copia.
2. **Al cerrar una etapa, se muestra lo que estaba oculto «hasta esa etapa».** El backlog lista cada control oculto con su etapa, y el cierre lo revisa.
3. **Las capturas las mira alguien que no es quien programó**, antes de cerrar la etapa: el guía o el usuario, por muestreo. No se justifica una diferencia que contradiga una decisión del usuario.
4. **Cuando el lienzo cambia algo ya programado** (criterio, teléfono app), se agrega la tarea al plan en ese momento.

## Decisiones del usuario (de a una)

1. **Etapa de alineación antes de la E5.** Recomendado: sí. Paquetes 0 a 3 y 5 ahora; el 4 apenas estén dibujados los teléfonos que faltan.
2. **«Verificar» apagado hasta tener los 6 números del código.** El criterio lo prohíbe y Mi cuenta ya muestra el aviso. Recomendado: alinear al criterio (habilitado, con «Escribí los 6 números del código.»).
3. **Rol «Cada empresa».** La API lo permite y el tablero no lo tiene. Recomendado: quitarlo de la pantalla y dejarlo en la API para más adelante.
4. **Invitar a varias empresas a la vez.** El tablero tiene una sola. Recomendado: una, como el tablero.
5. **Empresa: CUIT fijo o tipo + número, y la zona «La de la organización» por defecto.** Recomendado: CUIT como el tablero, guardado como tipo + número por debajo, y la zona heredada.
6. **El contacto de ventas y soporte («ventas@ejemplo.com», «soporte@ejemplo.com»).** Recomendado: que salga de la configuración de la plataforma.
7. **El correo «Membresía terminada».** El texto del código es más preciso desde la E6. Recomendado: el del tablero, corrigiendo el tablero si hace falta.
8. **Teléfono de las pantallas de Ingreso.** Recomendado: conservan la marca, con tamaños de app.
9. **Inicio-Org e Inicio-Miembro.** Se dejan vacíos como están y se repiensan más adelante.
