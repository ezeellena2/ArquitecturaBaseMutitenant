# Inicio de la organización

Lo primero que ve un miembro al entrar a `/org`. Arriba, una tarjeta por área con un dato real; abajo, «Para resolver»: lo que pide una acción y quien mira puede resolver. Los tableros aprobados son Inicio-Org (A), Inicio-Miembro (A) y M-Inicio-Org (C, app), del lienzo v77. La decisión está en la [ADR 0040](../decisions/0040-para-resolver.md).

## Conducta

- `GET /api/home` lo ve cualquier miembro (`[Access(Business)]`, sin permiso propio) y se calcula en un solo pedido de lectura. No hay tabla: nada se guarda ni se descarta, y una fila se va sola cuando cambia su estado.
- **Quién ve qué:**
  - con algún permiso de la organización, Inicio-Org: una tarjeta por área solo si tiene su permiso (`users.read`, `roles.read`, `companies.read`, `publicpage.manage`, `settings.read`, `audit.read`);
  - sin permisos de la organización, Inicio-Miembro: «Tus empresas», las empresas donde `IPermissionService.GetCompanyPermissionsAsync` le da algún permiso, con sus roles y los miembros activos si tiene `company.members.read`.
- **Los datos de cada tarjeta** salen del reader de su área, así el número es el mismo que muestra el área: usuarios y empresas, el total de sus filtros; roles, el total del listado; la página, Publicada, Borrador o Despublicada por la plataforma; el dominio verificado o, si no hay, el que espera verificación; la fecha del último cambio de la auditoría.
- **«Para resolver»** junta lo que aporta cada `IPendingItemSource`, con el tenant del claim y los permisos de quien mira:
  - `OrganizationUserPendingItems`: invitación vencida con `users.invite`; baja pedida con `users.manage`;
  - `OrganizationDomainPendingItems`: dominio sin verificar con `settings.manage`;
  - `PublicPagePendingItems`: página en borrador, o despublicada por la plataforma (urgente), con `publicpage.manage`.
- **Orden:** primero lo urgente, después por fecha, de la más vieja a la más nueva; lo que no tiene fecha va al final.
- `Area` y `Kind` son claves estables: el front arma el texto, el estado, el ícono y el enlace que lo resuelve.
- **Un módulo** registra su propia fuente en su registro, sin que el núcleo lo conozca. WhatsApp sin medio de pago llega con la Etapa 11.
- **Decisión 8 del usuario:** hasta B4, el Administrador de empresa ve solo «Tus empresas». Su fila por empresa se suma en la consolidación, junto con su ficha y sus permisos por empresa.

## Front

- `areas/business/home` del front pinta lo que devuelve la API. No deduce permisos: muestra las tarjetas y filas que vienen.
- Un `Kind` que el front no conoce no se muestra.
- **Enlaces de cada fila:**
  - «Reenviar» va a Usuarios filtrado por vencidas, como el tablero;
  - «Ver» de la baja va a la ficha del usuario;
  - «Verificar» va a Configuración;
  - «Ver» de la página va a Página pública.
- **Teléfono** (menos de 768 px): con tarjetas de la organización va la pantalla de app de M-Inicio-Org, con saludo, atajos redondos con el globito de sus pendientes y filas con botón cápsula. La barra de abajo y la de arriba son del marco de app (Paquete 4). Inicio-Miembro no tiene teléfono propio: se acomoda la de la compu.
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
- API: `HomeTests` (tarjetas iguales a cada área; sin permiso, ni la tarjeta ni la fila) y `HomeIsolationTests` (lo de otra organización nunca aparece), con Docker/Testcontainers.
- Front: `BusinessHomePage.test.tsx` (Inicio-Org, vacío, Inicio-Miembro y teléfono, con axe) y `formatters.test.ts` («hoy, 10:42»).
- E2E real: el Dueño ve las áreas y la página en borrador en «Para resolver» al entrar a `/org`.
