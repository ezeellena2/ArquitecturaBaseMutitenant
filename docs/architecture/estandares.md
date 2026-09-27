# Estándares transversales: qué está definido y qué falta

> Inventario de todo lo que tiene **una sola forma de hacerse en todo el sistema**, back y front. Cada estándar definido tiene su ficha en `docs/rules/` y su verificación. Los propuestos esperan la decisión del usuario. Estado al 2026-09-27.

## 1. Definidos (con ficha y verificación)

| # | Estándar | Qué fija | Librería | Dónde |
|---|---|---|---|---|
| 1 | **Fechas, horas y zonas** | UTC con `Z`, `DateOnly`, `TimeOnly`, zona IANA efectiva, `TimeProvider`, formato fijo por cultura (24 h en es-AR) | BCL `TimeZoneInfo`; `Intl` en el front | fechas-y-zonas · formatos |
| 2 | **Números y decimales** | `decimal` con precisión explícita, separadores por cultura, alineación a la derecha | BCL; `Intl.NumberFormat` | numeros-y-moneda |
| 3 | **Moneda** | `Money` (monto + ISO 4217), redondeo `AwayFromZero` en el back, el front no calcula | propio | numeros-y-moneda |
| 4 | **Porcentajes** | fracción (`0.125`) en la API, `12,5 %` en pantalla | propio | numeros-y-moneda |
| 5 | **Teléfonos** | E.164, país con bandera, formato al escribir, el 9 argentino, `PhoneUsage` | libphonenumber-csharp · libphonenumber-js · country-flag-icons | telefonos |
| 6 | **Idioma, región y traducciones** | cultura `es-AR`/`en-US`, resx + i18next con paridad, voseo, enums traducidos | .NET resx · i18next | textos-y-traducciones |
| 7 | **Paginado, orden y búsqueda** | 10 por defecto, cursor para tablas que solo crecen, búsqueda sin acentos, índices con tenant | Npgsql `unaccent` + `pg_trgm` | paginado-y-busqueda |
| 8 | **Errores** | `Result`, códigos `Area.Entidad.Motivo`, ProblemDetails, decidir por `code` | propio | result-y-errores · errores |
| 9 | **Validación** | un validador por request, mensajes traducidos, errores por campo | FluentValidation · zod | validacion · formularios |
| 10 | **Vacíos, enums, estados y booleanos** | `—`, `EnumText`, `StatusBadge` con tonos centrales, Sí/No | propio | formatos |
| 11 | **Zona horaria e idioma en pantalla** | "Buenos Aires (GMT−3)", "Español (Argentina)", nunca el código | `Intl.DisplayNames` | formatos |
| 12 | **Tamaños de archivo y duraciones** | `1,5 MB`, `2 h 15 min` | `Intl` | formatos |
| 13 | **Guardado y transacciones** | una sola forma de guardar | propio | guardado |
| 14 | **Multitenancy y perfiles** | `ITenantOwned`, RLS, `[TenantKind]` | EF + Postgres | multitenancy |
| 15 | **Permisos** | catálogos, atributos, nunca roles | ASP.NET Core authorization | permisos |
| 16 | **Auditoría** | marcas, rastro de cambios, eventos de seguridad | propio | auditoria |
| 17 | **Logs** | `[LoggerMessage]`, `OperationLog`, datos enmascarados | M.E.Logging + OpenTelemetry | logs |
| 18 | **Contratos de API** | contratos de entrada, tipos generados, status fijos | OpenAPI + openapi-typescript | api-http · datos-y-api |
| 19 | **Pantallas** | `Page`, listados, ficha, diálogo o pantalla, tokens | shadcn/ui + Tailwind | pantallas-y-ui |
| 20 | **Configuración y secretos** | mismas claves que ArquitecturaBase, user-secrets, validación al arrancar | Options pattern | operations/configuracion |

## 2. Adoptados el 2026-09-27 (P1 a P10)

El usuario los adoptó todos. Sus fichas:

| # | Ficha |
|---|---|
| P1 | back [concurrencia](../rules/concurrencia.md) · front `formularios.md` |
| P2 | back [persistencia-ef](../rules/persistencia-ef.md), "Orden alfabético" |
| P3 | back [emails](../rules/emails.md) · front `formularios.md` |
| P4 | back [textos-libres](../rules/textos-libres.md) |
| P5 | back [identificacion-fiscal](../rules/identificacion-fiscal.md) · front `formatos.md` |
| P6 | back [idempotencia](../rules/idempotencia.md) · front `datos-y-api.md` |
| P7 | back [datos-personales](../rules/datos-personales.md) |
| P8 | back [modulos-habilitados](../rules/modulos-habilitados.md) · front `permisos-y-perfiles.md` |
| P9 | front `accesibilidad.md` |
| P10 | front `responsive.md` |

Qué fija cada uno, y en qué etapa entra:

| # | Estándar | Qué fija | Librería | Etapa |
|---|---|---|---|---|
| P1 | **Concurrencia optimista** | Dos personas editan el mismo usuario o rol: hoy el segundo pisa al primero sin enterarse. Con esto, cada entidad editable lleva versión (`xmin` de Postgres); el front la manda con `If-Match` y un conflicto da 409 `General.ConcurrencyConflict` con "Otra persona cambió esto; recargá". | Npgsql (`UseXminAsConcurrencyToken`) | **Sí, en E2.** Las pantallas de edición con "Cambios sin guardar" lo necesitan |
| P2 | **Orden alfabético en español** | El Postgres del contenedor ordena mal "Ñ", los acentos y las mayúsculas ("Álvarez" queda después de "Zapata"). Se resuelve con la collation ICU `es-x-icu` en las columnas de nombres (configuración EF por convención). | Postgres ICU (incluido) | **Sí, en E2.** Es invisible hasta que alguien lo ve mal en producción |
| P3 | **Correos electrónicos** | trim + minúsculas + NFC, comparación por el normalizado (único global), dominios internacionales y el mismo validador en back y front | `MailAddress` + FluentValidation · zod `email()` | **Sí, en E3** (value object `Email`, que ya existe en ArquitecturaBase) |
| P4 | **Nombres y textos libres** | trim, espacios colapsados, Unicode NFC, largo máximo por tipo (nombre de persona 100, organización 120, descripción 500), sin caracteres de control | propio (`TextNormalizer`) | **Sí, en E1.** Barato, y evita duplicados como "Grupo  La Cosecha" |
| P5 | **Identificación fiscal y documentos** | `TaxId` con país y tipo (CUIT, CUIL, DNI; RUT o RUC después), validación del dígito verificador, formato `20-12345678-6`, guardado solo con dígitos | back: propio (la regla de módulo 11 es corta); front: `stdnum` (npm), que valida documentos de decenas de países | **Sí, en E6** (la empresa ya tiene CUIT en el lienzo) |
| P6 | **Idempotencia** | un `POST` que crea algo acepta `Idempotency-Key`; repetir el mismo pedido (doble clic, reintento de red) devuelve la misma respuesta sin duplicar | propio (tabla `platform.IdempotencyKeys` + filtro MVC) | **Sí, en E1**, al menos para altas e invitaciones |
| P7 | **Términos, privacidad y datos personales** | aceptación **versionada** de términos y privacidad al registrarse (quién, cuándo, qué versión), exportar mis datos y pedir la baja de la cuenta. Lo pide el B2C (Ley 25.326 en Argentina) | propio | **Sí, en E3** (registro) y E10 (exportar y dar de baja) |
| P8 | **Módulos habilitados por organización** | qué funcionalidades tiene prendidas cada organización o perfil (por plan, prueba o permiso de la plataforma); el menú y las rutas se apagan solos | `Microsoft.FeatureManagement` con un filtro por tenant | **Sí, en E5.** Es la base para vender módulos B2B y B2C por separado |
| P9 | **Accesibilidad verificada** | contraste AA y roles; un test automático por pantalla | `vitest-axe` (axe-core) | **Sí, en E0** (una línea por test de pantalla) |
| P10 | **Diseño adaptable (teléfono)** | cortes de pantalla, tablas que pasan a tarjetas en el teléfono, menú en cajón | Tailwind (ya está) | **Sí, antes de E4**, con los tableros del teléfono que faltan en el lienzo |

## 3. Propuestos: se suman **cuando el producto los necesite** (quedan escritos para que nadie invente otro)

| # | Estándar | Qué fijaría | Librería |
|---|---|---|---|
| P11 | **Archivos y adjuntos** | subida con límite de tamaño, tipo verificado por contenido (no por extensión), ruta `{tenantId}/…`, URL firmada con vencimiento, miniaturas | Azure Blob o S3 SDK · `react-dropzone` |
| P12 | **Exportar a Excel, CSV y PDF** | CSV con `;` y BOM (lo que abre bien Excel en es-AR), XLSX con los formatos de la cultura, PDF con el mismo `DisplayFormatter`, nombre `<recurso>-<fecha>.xlsx`, límite de filas y generación en segundo plano si es grande | ClosedXML (MIT) · QuestPDF (licencia comunitaria gratis hasta USD 1 M de facturación anual: verificar) |
| P13 | **Direcciones** | país ISO 3166-1, provincia ISO 3166-2, localidad y código postal; en Argentina, normalizadas contra el servicio Georef | Georef (datos.gob.ar, gratis) · `Intl.DisplayNames` |
| P14 | **Rangos de fechas y filtros predefinidos** | "Hoy", "Últimos 7 días", "Este mes", calculados en la zona efectiva con `GetDayRangeUtc`, y un `DateRangeField` | propio |
| P15 | **Días hábiles y feriados** | vencimientos y plazos "en días hábiles" de Argentina | Nager.Date (verificar licencia) o una tabla propia de feriados |
| P16 | **Cotizaciones y conversión de moneda** | tipo de cambio con fecha y fuente guardadas junto al importe convertido; nunca convertir en el front | API del BCRA |
| P17 | **Unidades de medida** | kg, litros, km: guardado en unidad base y mostrado con `Intl` `style: "unit"` | `UnitsNet` si hay conversiones |
| P18 | **Notificaciones dentro de la app y tiempo real** | campana con avisos, marcados como leídos, y avisos en vivo | SignalR |
| P19 | **Tareas programadas** | tareas recurrentes por tenant (resúmenes, vencimientos), con reintentos y registro | el `TenantJobRunner` propio o Quartz.NET |
| P20 | **Búsqueda global** | `Ctrl+K` para ir a cualquier pantalla o entidad | `cmdk` (lo usa shadcn) |
| P21 | **Deshacer** | "Deshacer" en el aviso después de una acción destructiva simple, en lugar de otro diálogo de confirmación | `sonner` (ya está) |
| P22 | **Identificadores legibles** | número correlativo por tenant (`#1024`) para lo que la gente nombra en voz alta (pedidos, comprobantes), además del Guid | propio (secuencia por tenant) |

## 4. Cómo se agrega uno

Decidido un estándar:
1. su ficha en `docs/rules/` (y en el front si corresponde);
2. su fila en el §1 de este documento;
3. su verificación (test o analizador);
4. sus casos en `format-cases.json`, si es de presentación;
5. la tarea en la etapa del plan que corresponda.

Todo en el mismo commit.
