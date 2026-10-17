# 03 · Arquitectura y premisas técnicas

Lo que sigue es cómo está armado en ArquitecturaBase. **Cuando una regla de este proyecto (`AGENTS.md`, `docs/rules/`, `docs/architecture/`) dice otra cosa, manda la de acá.** El detalle por clase, tabla y ruta está en el [anexo A](anexo-a-backend.md) y el del front en el [anexo B](anexo-b-front.md).

## Módulos y puertos

- **Tres módulos opcionales**, en `Modules/<Módulo>` de cada capa:
  - **BusinessForms:** formularios, catálogos, respuestas, archivos, reglas, aprobación y PDF;
  - **WhatsApp:** canal, cola de salida, Flows, captura y medios;
  - **ArtificialIntelligence:** conexión, motor del asistente y transcripción.
- **El núcleo no nombra a ningún módulo,** y los módulos no se referencian entre sí: hablan por **puertos neutrales** en `Application/Interfaces/Channels` y modelos en `Application/Models/StructuredData`. Ejemplos:
  - la fuente de formularios (`IStructuredFormSource`), que prepara las opciones y valida;
  - el destino de la captura (sink);
  - el origen de una respuesta;
  - la fuente de archivos;
  - el canal que manda los avisos de las reglas.

  Cada puerto tiene una versión «apagada» en `Application/Channels/` para cuando el módulo no está instalado.
- **Quitar un módulo** está probado: hay guías `quitar-whatsapp`/`quitar-ia` y tests de arquitectura que controlan las referencias.

**En multitenant:** el módulo quitable `Forms` con su feature `forms` (hoja de ruta), y el mismo patrón de puertos hacia WhatsApp (E11) y el asistente (E8B).

## Recorrido de una petición

`Controller → interfaz de servicio → servicio → repositorio, lector o integración → Infrastructure`.

- **Borde HTTP:** controllers MVC en `api/<recurso>`, sin versión. La entrada es un contrato propio (`*HttpRequest` o `*Query`), con `ToString()` redactado si lleva datos personales o valores. Los mapeos se hacen a mano.
- **Sin** `ICommand`, handlers, repositorio genérico ni Minimal APIs de negocio.
- **Permisos,** nunca roles: `[HasPermission(...)]`, 401 sin sesión y 403 sin permiso. El servicio vuelve a verificar el permiso **después de leer** los datos privados, para no devolver nada si se revocó en el medio.
- **Rate limits por política:** lectura de formularios, archivos con cupo propio y exportación con una a la vez por usuario.

## Guardar, errores y concurrencia

- **Una sola forma de guardar:** `IUnitOfWork.ExecuteInTransactionAsync(trabajo, CommitPolicy, ct)`, una vez por método público que escribe.
- **Reglas de negocio con `Result`,** sin excepciones. Los errores se declaran en `<Entidad>Errors` dentro de Domain, con códigos estables `Area.Entidad.Motivo` traducidos en `.resx`, y salen como ProblemDetails con `title`, `detail`, `code`, `traceId` y `errors` por campo.
- **Configuración editable** (borradores, catálogos, reglas, diseño del PDF): con `expectedRevision`. Si alguien guardó antes, 409 con la revisión actual, y el front conserva el borrador. «Reintentar» después de un error de red reusa la misma revisión.
- **Repositorios y lectores:** `Get` (entidad seguida), `Find` (proyección), `List`, `Exists`, `Count`, `Lock`. `AsNoTracking` solo en lectores. `IQueryable` no sale de Infrastructure.
- **Entidades:** heredan de `Entity` (Guid v7), sin eventos de dominio. Auditoría y borrado lógico los completan interceptores.

## Robustez proporcional (ADR 0018)

| Efecto | Tratamiento |
| --- | --- |
| **Fuera del sistema:** Meta (publicar, mandar), descarga de medios, almacenamiento, LLM y transcripción | Operación durable con recibo, lease y fence; estados inciertos («Sin confirmar») que se **consultan** antes de reintentar y nunca se repiten si ya salieron confirmados; idempotencia por Id (de ejecución, de pedido o de sesión); pruebas de varios hosts. |
| **Lectura** (planilla, filtros, Excel) | Sin recibos ni leases; presupuesto de tiempo y topes. |
| **Configuración interna** | Límite, revisión y 409. |

- **La cola de salida de WhatsApp es única para el operador, las reglas y el asistente.** Cada salida lleva `Origin` y `Kind`, se deduplica por el Id de su ejecución y respeta las 24 horas.
- **La captura de una respuesta es todo o nada:**
  - usa un recibo con huella, así un reintento no duplica ni salta números;
  - si un archivo falla de forma definitiva, la respuesta se rechaza con motivo;
  - los reintentos están acotados por cantidad y por tiempo: 5 fallas y al menos 15 minutos desde la primera, para que una caída corta de Redis no pierda respuestas.

## Archivos (Etapa 2B, ADR 0012)

- **Contenedor privado** en Azure Blob (Azurite en desarrollo y en los tests). Nunca URL públicas ni SAS al navegador: se ven y descargan solo por la Api, con el mismo permiso que los valores.
- **Claves determinísticas** por respuesta, pregunta, posición y hash: reintentar sobreescribe lo mismo.
- **Verificación antes de subir:** hash, tamaño, tipo real por contenido (PDF, Office, JPEG/PNG; nunca la extensión), límites de dimensión antes de decodificar y nombre de descarga armado con la extensión del tipo verificado.
- **Imágenes** en línea con `nosniff`; documentos siempre como descarga. Miniaturas livianas al guardar; el original se baja solo al abrir el visor.
- **Los archivos siguen a la respuesta,** no a la retención de los mensajes de WhatsApp. Los huérfanos se limpian.

**En multitenant:** el adaptador de nube de E10, aislado por organización.

## Idiomas

- **El sistema es bilingüe** (es/en): pantallas, mensajes y errores en `.resx` y archivos de i18n, en español rioplatense con voseo.
- **El contenido de un formulario** (nombre, preguntas, ayudas, opciones y botón) son mapas por idioma, con un **idioma original** inmutable. **Regla nueva del 2026-10-16:** solo el idioma original es obligatorio. Si el idioma de quien recibe no está completo, el formulario, el PDF, los mensajes y las opciones de catálogo salen enteros en el original, sin mezclar. Se aplica a lo nuevo de las Etapas 4 y 5; lo existente cambia después de la 5.
- **Una opción de catálogo** se guarda en la respuesta con su texto **por idioma**, para que la copia fija no mezcle idiomas (lección del review de la Etapa 4).

## Otras reglas fijas

- **Fechas:** UTC desde `TimeProvider` inyectado; propiedades terminadas en `Utc`; `DateOnly` si no hay hora; tests con `FakeTimeProvider`.
- **Logs** con `[LoggerMessage]`. Nunca valores de respuestas, nombres de archivo, teléfonos enteros, códigos ni secretos.
- **Build con advertencias como errores.** Versiones de paquetes centralizadas, y la auditoría de vulnerabilidades no se apaga: se sube el paquete.
- **Redis** es el único caché compartido.
- **Secretos** en user-secrets o variables de entorno, nunca en el repo.
- **Tests:**
  - unitarios sin dependencias;
  - de arquitectura (capas, rutas inventariadas, errores declarados, paridad de recursos es/en);
  - de integración con Testcontainers y `ApiFactory`;
  - los cuerpos HTTP reales se exportan para que el front valide sus esquemas zod estrictos.

  Lo que existe solo para probar va en `TestFeatures/`.
- **Front:**
  - una feature nunca importa otra;
  - TanStack Query, con claves por actor, cultura y permisos y limpieza al perder el contexto;
  - datos privados nunca en la URL ni en el storage;
  - un único Toaster y validación junto al campo;
  - piezas comunes en `shared/ui`.

## Integración con Meta

- **Flow JSON 7.3, con perfiles de compilación versionados** (r3, r4, r5):
  - una publicación queda sellada con su perfil;
  - los formularios publicados con perfiles viejos siguen mandándose y capturándose;
  - un rechazo definitivo de Meta termina en «No se pudo», visible.
- **Elección de catálogo:** Dropdown con `data-source` dinámico. Las opciones viajan en `flow_action_payload.data` al mandar, selladas en la sesión, sin endpoint de intercambio cifrado.
- **Archivos:** PhotoPicker y DocumentPicker, una pregunta de archivo por pantalla, nunca los dos juntos y hasta 10 archivos por respuesta.
- **Medios de pago:** sin medio de pago en el WABA, Meta rechaza el envío (error 141006).

## IA (Etapa 5, ADR 0020)

- **Motor único:** toda llamada al modelo pasa por el motor del módulo de IA, con canales: modo Asistente, sugerencia al operador y chat administrativo.
- **Capacidades por módulo,** aportadas por BusinessForms y WhatsApp: tipadas, con permiso y presupuesto, y **solo de lectura o propuesta**. Ejecutar es siempre del módulo dueño (mandar un formulario, registrar un pedido).
- **Casos de evaluación** con datos ficticios antes de habilitar un modelo o un perfil. Un modelo solo se usa si su perfil está declarado y calificado con pruebas reales; listar o probar la conexión no lo habilita.
- **Transcripción de audios** con el mismo proveedor (OpenAI en ArquitecturaBase). Sin transcripción, el audio queda para el operador.

**En multitenant:** el asistente de E8B con tope mensual por organización (ADR 0051), y la transcripción dentro de ese tope.
