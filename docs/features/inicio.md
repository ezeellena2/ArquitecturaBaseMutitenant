# Inicio de la organización

Lo primero que ve un miembro al entrar a `/org`. Arriba, una tarjeta por área con un dato real; abajo, «Para resolver»: lo que pide una acción y quien mira puede resolver. Los tableros aprobados son Inicio-Org (A), Inicio-Miembro (A) y M-Inicio-Org (C, app), del lienzo v77, y M-Inicio-Miembro, del v106. La decisión está en la [ADR 0040](../decisions/0040-para-resolver.md).

## Conducta

- `GET /api/home` lo ve cualquier miembro (`[Access(Business)]`, sin permiso propio) y se calcula en un solo pedido de lectura. No hay tabla: nada se guarda ni se descarta, y una fila se va sola cuando cambia su estado.
- **Quién ve qué:**
  - con algún permiso asignado por roles de la organización, Inicio-Org: una tarjeta por área solo si tiene su permiso (`users.read`, `roles.read`, `companies.read`, `publicpage.manage`, `settings.read`, `audit.read`);
  - sin permisos asignados por roles de la organización, Inicio-Miembro: «Tus empresas», las empresas donde tiene algún permiso según `IPermissionService.GetPermissionsByCompanyAsync`, con sus roles y los miembros activos si tiene `company.members.read`.
  El modo usa `GetAssignedOrganizationPermissionsAsync`; las tarjetas y pendientes reciben los permisos efectivos de `GetOrganizationPermissionsAsync`. Una proyección desde empresa no cambia el modo, aunque habilite una ruta de organización. Un rol organizacional explícito que solo concede `assistant.use` conserva Inicio-Org. `HomeTests` comprueba ambos casos y al Administrador de empresa con la feature apagada y encendida.
- **Los datos de cada tarjeta** salen del reader de su área, así el número es el mismo que muestra el área: usuarios y empresas, el total de sus filtros; roles, el total del listado; la página, Publicada, Borrador o Despublicada por la plataforma; el dominio verificado o, si no hay, el que espera verificación; la fecha del último cambio de la auditoría.
- **«Para resolver»** junta lo que aporta cada `IPendingItemSource`, con el tenant del claim y los permisos de quien mira:
  - `OrganizationUserPendingItems`: invitación vencida con `users.invite`; baja pedida con `users.manage`; sin permisos de la organización, la invitación vencida de cada empresa donde tiene `company.members.manage` y que es únicamente de esa empresa (membresía activa en ella, sin roles de la organización ni otra membresía activa: la que puede reenviar desde la ficha, `CompanyInvitationPolicy`), una fila por empresa, con `companyId` y `companyName`;
  - `OrganizationDomainPendingItems`: dominio sin verificar con `settings.manage`;
  - `PublicPagePendingItems`: página en borrador, o despublicada por la plataforma (urgente), con `publicpage.manage`.
- **Orden:** primero lo urgente, después por fecha, de la más vieja a la más nueva; lo que no tiene fecha va al final.
- `Area` y `Kind` son claves estables: el front arma el texto, el estado, el ícono y el enlace que lo resuelve.
- **Un módulo** registra su propia fuente en su registro, sin que el núcleo lo conozca. WhatsApp sin medio de pago llega con la Etapa 11.
- **B4 (consolidación, reemplaza la decisión 8 del Paquete 1 de la alineación):** el Administrador de empresa sin roles de la organización ve en «Para resolver» las invitaciones vencidas de su empresa, con la empresa, y puede abrir la ficha de esa empresa: `GET /api/companies/{companyId}` pide `companies.read` o, en esa empresa, `company.members.read`. Desde la ficha reenvía o revoca las invitaciones que son únicamente de su empresa, y solo esas le dan fila: así «Reenviar» siempre lleva a algo que puede hacer ([organizaciones](organizaciones.md#empresas-y-membresías)).

## Front

- `areas/business/home` del front pinta lo que devuelve la API. No deduce permisos: muestra las tarjetas y filas que vienen.
- Un `Kind` que el front no conoce no se muestra.
- **Enlaces de cada fila:**
  - «Reenviar» va a Usuarios filtrado por vencidas, como el tablero; con `companyId`, a la ficha de esa empresa, como Inicio-Miembro y M-Inicio-Miembro;
  - «Ver» de la baja va a la ficha del usuario;
  - «Verificar» va a Configuración;
  - «Ver» de la página va a Página pública.
- **Teléfono** (menos de 768 px): con tarjetas de la organización va la pantalla de app de M-Inicio-Org, con saludo, atajos redondos con el globito de sus pendientes y filas con botón cápsula. La barra de abajo y la de arriba son del marco de app (Paquete 4). Sin permisos de la organización va M-Inicio-Miembro (lienzo v106), también en «billetera»: la cabecera de color, «Hola, …», la organización y «Tus empresas» en filas; si administra su empresa, abajo va su «Para resolver».
- **La página en borrador no tiene «Desde»:** la página no guarda cuándo dejó de estar publicada.

## Carpetas

| Carpeta | Responsabilidad |
|---|---|
| `src/ArquitecturaBaseMultitenant.Application/Models/Home` | respuesta del inicio, filas de «Para resolver» y su contexto |
| `src/ArquitecturaBaseMultitenant.Application/Services/Home` | servicio, tarjetas de la organización, «Tus empresas» y orden |
| `src/ArquitecturaBaseMultitenant.Api/Controllers/Organization` | `HomeController` |
| `tests/ArquitecturaBaseMultitenant.Application.UnitTests/Services/Home` | orden de «Para resolver» |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Organization` | datos reales y permisos |
| `tests/ArquitecturaBaseMultitenant.Api.IntegrationTests/Tenancy` | aislamiento entre organizaciones |

## Lo verifica

- Application: `PendingItemOrderTests`.
- API: `HomeTests` (tarjetas iguales a cada área; sin permiso, ni la tarjeta ni la fila; el Administrador de empresa ve la fila de su empresa y abre su ficha, pero no la de una invitación que además da roles de la organización o entra a otra empresa), `CompanyAdminTests` (desde la ficha reenvía y revoca las invitaciones de su empresa; una a la que le sacaron la empresa ya no da fila; una que además da roles de la organización o entra a otra empresa la resuelve solo quien tiene `users.invite`) y `HomeIsolationTests` (lo de otra organización nunca aparece), con Docker/Testcontainers.
- Front: `BusinessHomePage.test.tsx` (Inicio-Org, vacío, Inicio-Miembro y teléfono, con axe) y `formatters.test.ts` («hoy, 10:42»).
- E2E real: el Dueño ve las áreas y la página en borrador en «Para resolver» al entrar a `/org`.
