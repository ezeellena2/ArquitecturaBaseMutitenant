# Revisión de código de las etapas 1 a 6: guía para Codex

> **Para Codex, que hace la revisión:**
> - **Cuándo:** empezá recién cuando la Etapa 5 pasó su puerta (T25 de [2026-10-05-etapa-5-pantallas.md](2026-10-05-etapa-5-pantallas.md)) y los dos repos están limpios en `main`. No arranques la Etapa 7.
> - **No arreglás nada.** Solo reportás. Ni refactors ni cambios de estilo. El usuario decide qué se arregla y cuándo.
> - **Qué reportar:** solo defectos con una consecuencia concreta. Sirve un dato que se ve desde otra organización, una acción sin el permiso que corresponde, un correo completo en un log, un guardado que puede quedar a medias o un error que se traga. También una regla escrita que el código rompe. «Se podría escribir mejor» no es un hallazgo.
> - **Cada hallazgo, verificado:** antes de anotarlo, seguí el camino completo en el código, del controller a la base o de la pantalla a la API. Si podés, confirmalo con un test que falle, y borralo después: el informe dice cómo reproducirlo.
> - **Lo ya decidido no es un hallazgo:** las ADR, las «Decisiones tomadas» de los planes y lo anotado como «queda conocido». Por ejemplo, la ventana de milisegundos de T18b. Sí lo es si el código contradice lo decidido.
> - **Commits:** solo el informe, en español, con `git commit -m "…" -- <ruta>`. Todo en `main`, sin push.

## Alcance

- **Back:** `src/` y `tests/` de este repo.
- **Front:** `src/` y `scripts/` de `../ArquitecturaBaseMutitenantFront`.
- **De las migraciones** se miran solo las políticas RLS, los índices únicos y los triggers.
- **Afuera:** `docs/design/`, los archivos generados (`schema.d.ts`, `openapi.json`) y WhatsApp, que es de las etapas 8 y 11.

## Cómo se trabaja

1. Por área, en el orden de la tabla. Antes de cada área, leé sus fichas (`docs/rules/` de cada repo) y las ADR que nombran.
2. Usá los tests como mapa. Lo que una guarda ya asegura (`AccessDeclarationTests`, `TransactionBoundaryTests`, los de aislamiento de `Api.IntegrationTests/Tenancy/`, `harness.test.ts`, etc.) no se reporta, salvo que la guarda tenga un agujero. Si lo tiene, ese agujero es el hallazgo.
3. Al terminar cada área, sumala al informe y commitealo. Así el usuario ve el avance.

## Las áreas, por riesgo

| # | Área | Fichas | Qué buscar |
|---|---|---|---|
| 1 | **Aislamiento entre organizaciones** | back: `multitenancy`, `persistencia-ef`; `docs/architecture/multitenancy.md` | Ver la fila 1 abajo. |
| 2 | **Accesos y permisos** | back: `permisos`, `api-http`; ADR 0042; front: `accesos-y-permisos` | Ver la fila 2 abajo. |
| 3 | **Datos personales y logs** | back: `datos-personales`, `logs`, `emails`, `telefonos`, `auditoria` | Ver la fila 3 abajo. |
| 4 | **Guardado y transacciones** | back: `guardado`, `capas-y-flujo`; ADR 0039 | Ver la fila 4 abajo. |
| 5 | **Concurrencia e idempotencia** | back: `concurrencia`, `idempotencia` | Ver la fila 5 abajo. |
| 6 | **Errores y validación** | back: `result-y-errores`, `validacion`, `textos-libres`, `identificacion-fiscal`; front: `errores`, `formularios` | Ver la fila 6 abajo. |
| 7 | **Ingreso y sesión** | `docs/features/identidad.md`; ADR de ingreso | Ver la fila 7 abajo. |
| 8 | **Tiempo, números, formatos y textos** | back: `fechas-y-zonas`, `numeros-y-moneda`, `datos-de-referencia`, `textos-y-traducciones`; front: `formatos`, `textos-y-traducciones` | Ver la fila 8 abajo. |
| 9 | **El front** | front: `estructura-y-features`, `datos-y-api`, `paginado-y-listados`, `pantallas-y-ui`, `responsive`, `accesibilidad` | Ver la fila 9 abajo. |

**Qué buscar en cada área:**

1. **Aislamiento entre organizaciones:**
   - una entidad con datos de una organización sin su clase (`ITenantOwned`, `IPublishedByBusiness`, `IConsumerBusinessShared`) o sin su política RLS;
   - una columna de organización que se asigna a mano;
   - un `IgnoreQueryFilters` fuera de la lista blanca;
   - un tenant que sale de algo que no es el claim `tenant_id`;
   - el subdominio que resuelve algo privado;
   - `ITenantScope.Enter` sin autorizar antes, sin `SecurityEvent` o con una transacción abierta;
   - SQL a mano que se saltea RLS;
   - un worker (outbox, avisos) que procesa filas de una organización con otra;
   - una clave de caché sin la organización: es el error típico de datos cruzados;
   - un recurso ajeno que no responde 404.
2. **Accesos y permisos:**
   - una ruta sin `[Access]`, `[PublicSite]` o `[AllowAnonymous]`, o con el permiso equivocado;
   - un id de la ruta que no se controla contra la organización o la empresa de quien pide: una empresa ajena, un miembro de otra empresa (IDOR);
   - una política de empresa (`CompanyInvitationPolicy` y parecidas) que deja pasar de más;
   - un operador que puede actuar sobre sí mismo o entrar como persona;
   - el front que muestra una acción que el back rechaza, o al revés.
3. **Datos personales y logs:**
   - un correo, teléfono, código, token o enlace completo en un log, un `SecurityEvent`, un error o un correo saliente;
   - un log sin `[LoggerMessage]`;
   - un método público de servicio fuera de `OperationLog.RunAsync`;
   - una respuesta que muestra completo lo que el tablero tapa (`PersonalDataMask`);
   - una respuesta que deja saber si un correo tiene cuenta donde no debe: ingreso, registro, «Recuperar mi cuenta».
4. **Guardado y transacciones:**
   - un método que escribe sin `ExecuteInTransactionAsync`, o con dos;
   - una validación adentro de la transacción;
   - un orden distinto a locks → lecturas → reglas → escrituras;
   - caché o logs antes del commit;
   - un correo o una llamada externa fuera del outbox;
   - un `SaveChanges` suelto;
   - una carrera entre dos pedidos que deja datos inconsistentes: dos altas iguales, el último Dueño, el último administrador.
5. **Concurrencia e idempotencia:**
   - una escritura con `version` que no devuelve `VersionResponse`;
   - una pantalla que no toma como base la versión que devolvió el guardado;
   - un `POST` que crea o envía sin `[Idempotent]`, o el front sin `useIdempotentMutation` o que cambia la clave al reintentar lo mismo;
   - un 409 que pierde lo que el usuario escribió.
6. **Errores y validación:**
   - una excepción para una regla de negocio;
   - un error sin `code` o sin `traceId`;
   - un código que no sigue `Area.Entidad.Motivo`, o que el front no traduce y muestra crudo;
   - una violación de clave única o foránea que llega como 500 y no como su error;
   - un dato que el back acepta sin validar: largo, formato, CUIT, correo, teléfono.
7. **Ingreso y sesión:**
   - un código de ingreso reutilizable, sin vencimiento o sin tope de intentos;
   - una sesión que sigue viva después de suspender, dar de baja o cerrar sesiones;
   - una redirección después de ingresar que acepta una URL de afuera (open redirect);
   - Google que vincula una cuenta que no corresponde;
   - un `access` que se puede cambiar de `consumer` a `business` o `platform` sin pasar por su puerta.
8. **Tiempo, números, formatos y textos:**
   - `DateTime.Now` o fechas sin UTC;
   - `double` para dinero;
   - una lista de monedas, países o zonas escrita en el código;
   - un correo o WhatsApp formateado sin `DisplayFormatter`;
   - un texto al usuario sin recurso, o sin su versión en inglés;
   - «tenant» en pantalla.
9. **El front:**
   - una feature que importa de otra;
   - `fetch` fuera de la capa de API, o tipos generados editados;
   - un listado sin `usePagedQuery` o `useCursorList`;
   - un formulario sin React Hook Form y zod, salvo `AuthCodeStep`;
   - un botón de envío apagado por un dato que falta;
   - una pantalla sin estado de carga, error con «Reintentar» o vacío;
   - una pieza de `shared/` copiada con medidas a mano;
   - problemas de accesibilidad que axe no ve: foco al abrir y cerrar un diálogo, orden del teclado.

## El informe

En el back: `docs/reviews/<fecha>-revision-de-codigo.md`, con la fecha del día en que empezás. Lleva:

1. **Resumen:** cuántos hallazgos por severidad y por área, y qué áreas quedaron revisadas.
2. **Hallazgos**, del más grave al menos grave. Cada uno con:
   - **severidad:**
     - **Crítico:** datos de otra organización, acceso sin permiso, sesión que no se corta o datos personales expuestos;
     - **Alto:** datos inconsistentes o una regla de negocio que se puede saltear;
     - **Medio:** una regla escrita que no se cumple, con consecuencia visible;
     - **Bajo:** lo demás que tenga consecuencia concreta;
   - **área**;
   - `archivo:línea` de los dos repos si toca los dos;
   - la regla o la ADR que rompe;
   - **qué pasa:** el escenario concreto, con qué datos, quién ve o hace qué;
   - **cómo se reproduce**;
   - **la propuesta de arreglo**, sin aplicarla;
   - **la confianza:** «confirmado con un test» o «leído en el código».
3. **Revisado y sin hallazgos:** por área, qué miraste. Así se sabe qué quedó cubierto.
4. **Descartados:** lo que parecía un problema y no lo era, con el porqué en una línea.

## Después

La Guía verifica los hallazgos críticos y altos y le muestra al usuario cuáles se arreglan. Los arreglos van en un plan aparte, con rojo y verde como siempre, antes de la Etapa 7.
