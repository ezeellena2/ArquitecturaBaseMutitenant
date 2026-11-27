# Estándar de los tickets de Jira

Vale para cualquier agente que cree o trabaje tickets, sea Claude o Antigravity.

- **Dónde:** el sitio es `ezequielellena0003.atlassian.net` y el proyecto, `KAN` («Mi espacio de Kanban»).
- **Columnas del tablero:** To Do, In Progress, In Review y Done.
- **Modelo:** el ticket KAN-12. Leelo antes de crear el primero y copiá su forma.
- **Antes de crear un ticket,** buscá con JQL si ya existe uno igual.

## Épicos: cada ticket va adentro de uno (`parent`)

| Épico | Tema |
|---|---|
| KAN-1 | Seguridad, identidad y datos personales |
| KAN-2 | WhatsApp y Meta: canal, webhook, plantillas, conexión y difusiones |
| KAN-3 | Chat de atención: bandeja, asignación, operadores, bot e hilos |
| KAN-4 | Asistente de IA |
| KAN-5 | Escala y rendimiento: viajes a PostgreSQL, cola de envíos, trabajos de fondo y tablas que crecen |
| KAN-6 | Multitenancy, aislamiento y vecino ruidoso |
| KAN-7 | Arquitectura y código: capas, módulos, duplicaciones, tests y arnés |
| KAN-8 | Despliegue y operación (E10) |
| KAN-9 | Front |
| KAN-10 | Documentación desactualizada: un ticket que solo cambia documentos va acá |
| KAN-11 | Decisiones de producto pendientes |

Si un ticket encaja mejor en otro épico que el de su tema, va en el que encaja. No se crean épicos nuevos sin el visto bueno del usuario.

## Campos

- **Tipo de ticket:**
  - `Error`: algo falla o hace lo que no debe, como un hueco de seguridad, un dato que se pierde o un envío que no sale.
  - `Tarea`: una mejora, un ajuste de diseño, algo que falta, un cambio de documentación, una comprobación o una decisión.
- **`priority`:** `High` si la gravedad es alta, `Medium` si es media y `Low` si es baja.
- **`labels`, siempre:**
  - la gravedad: `gravedad-alta`, `gravedad-media` o `gravedad-baja`;
  - `back` y/o `front`.
- **`labels`, cuando corresponde:**
  - el origen, por ejemplo `revision-2026-11-27`, y una `antigravity-T0N-MM` por cada hallazgo de esa revisión;
  - `ya-en-backlog`: ya estaba anotado en `docs/backlog.md`, y el ítem va en «Origen»;
  - `ya-decidido`: una decisión (ADR o regla) lo deja para después;
  - `necesita-prueba`: no se confirma sin correr algo;
  - `sin-verificar`: lo cargó alguien y todavía nadie lo comprobó contra el código;
  - `descartado`: se cerró sin arreglo, con el motivo en un comentario;
  - `decision-de-producto`: lo decide el dueño;
  - `e10`, `e12` o la etapa que sea: se arregla dentro de esa etapa.
- **`summary` (el título):** una frase en español llano que diga qué pasa, sin jerga ni números de hallazgos. Por ejemplo: «Un correo corporativo revocado sigue sirviendo para entrar, por Google y por las altas».

## Descripción (markdown, en este orden)

```
## Qué pasa
Qué está mal, en palabras simples, y por qué importa. Si solo una parte es real, solo esa parte, y una línea con lo que no se confirmó.

## Cómo reproducirlo            (en una Tarea: «## Cómo verlo»)
**Preparación:** los datos que hacen falta (organizaciones, personas, configuración), con nombres de ejemplo.
1. Paso concreto.
2. Paso concreto.
   - **Esperado:** lo que debería pasar.
   - **Pasa:** lo que pasa hoy.

## Dónde está
- `Archivo.cs:línea`: qué hace ahí, en una línea.

## Impacto
**Gravedad alta|media|baja.** A quién afecta y cuánto. Si otra barrera ya lo acota, decilo.

## Arreglo sugerido
- Lo que habría que cambiar, concreto.

## Cómo se comprueba
El test que lo prueba (de unidad o de integración) y qué afirma.

## Origen
De dónde salió (revisión, prueba, usuario), con la fecha y el commit contra el que se comprobó. Más el ítem del backlog o la decisión, si corresponde.
```

Una decisión de producto lleva otras secciones: **Qué hay que decidir**, **Ejemplo**, **Opción recomendada**, **Alternativa** y **Origen**.

## Calidad

- **Comprobá cada archivo y método en el código de hoy** con `grep -n`. Si ya está arreglado, no crees el ticket.
- **Los pasos de «Cómo reproducirlo»** los tiene que poder seguir una persona: la pantalla o la ruta, los datos de ejemplo, lo esperado y lo que pasa.
- **Un ticket por problema.** Si varios hallazgos son el mismo problema, va un solo ticket. Si uno mezcla dos problemas, van dos.
- **Contenido prohibido:**
  - links relativos como `[x](docs/...)`: las rutas van entre comillas invertidas;
  - secretos;
  - correos o teléfonos reales: de ejemplo, `nombre@acme.example`;
  - datos personales del dueño.
- **Cada ticket se crea una sola vez.** Si `createJiraIssue` devuelve una clave, no lo vuelvas a crear.
- **Español rioplatense con voseo.** «Organización», nunca «tenant».

## Si cargás varios tickets a la vez

Al terminar, devolvé:
- una línea por cada ticket creado: clave, título y prioridad;
- una línea por cada hallazgo que no llevó ticket, con el motivo.
