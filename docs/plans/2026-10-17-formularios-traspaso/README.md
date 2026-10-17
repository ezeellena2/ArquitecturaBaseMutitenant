# Formularios por WhatsApp: traspaso desde ArquitecturaBase

Fecha: 2026-10-17. Autor: el chat coordinador de ArquitecturaBase.

Este paquete cuenta, con el máximo detalle, todo lo que se construyó en `../ArquitecturaBase` y `../ArquitecturaBaseFront` para los formularios por WhatsApp. Cubre las Etapas 1, 2, 2B, 3 y 4, y lo decidido de la 5. Es la base para que este proyecto arme **su** plan con el mismo producto, las mismas premisas y la misma disposición de pantallas, adaptado al modelo multitenant (organizaciones B2B, personas B2C y plataforma).

**No es un plan para copiar tal cual.** Es la referencia de qué se hizo, por qué y cómo se probó. El plan de este proyecto se arma después, con las reglas de acá (`AGENTS.md`, `docs/rules/`, `docs/architecture/multitenancy.md`) y sobre la [hoja de ruta del 2026-10-12](../2026-10-12-formularios-dinamicos-hoja-de-ruta.md). Esa hoja ya fija etapas (F1–F7) y cinco decisiones de producto.

## Cómo leerlo

| Archivo | Qué cuenta |
| --- | --- |
| [01 · Producto y recorrido](01-producto-y-recorrido.md) | Qué hace el sistema, quién lo usa y el recorrido completo de una visita, de punta a punta. |
| [02 · Forma de trabajo](02-forma-de-trabajo.md) | Cómo se trabajó cada etapa: tablero antes que código, resúmenes con decisiones cerradas, un chat de Codex, pruebas, code reviews y pilotos reales. |
| [03 · Arquitectura y premisas técnicas](03-arquitectura-y-premisas.md) | Módulos, puertos, capas, guardado, errores, robustez, archivos, idiomas, permisos y lo que cambia en multitenant. |
| [04 · Etapas](04-etapas.md) | Las etapas una por una: alcance, decisiones con fecha, reglas de negocio, backend, front, cierre y piloto. |
| [05 · Pantallas](05-pantallas.md) | La disposición de cada pantalla y diálogo, zona por zona, para replicarla con los componentes de acá. |
| [06 · Lecciones](06-lecciones.md) | Los hallazgos de los code reviews y los pilotos: errores a no repetir. |
| [07 · Pendientes y decisiones posteriores](07-pendientes.md) | Lo que quedó decidido para después de la Etapa 5 y lo que todavía no existe. |

**Anexos**, el relevamiento completo y exacto, verificado en el código al 2026-10-17:
- [A · Backend](anexo-a-backend.md): entidades y tablas, servicios, rutas, workers, Meta, archivos, permisos y configuración.
- [B · Front](anexo-b-front.md): navegación, pantallas y diálogos, estados y reglas del front.
- [C · Reglas, decisiones y lecciones](anexo-c-reglas.md): con la fuente de cada bloque.
- [D · Tableros](anexo-d-tableros.md): el texto de las notas de cada tablero del lienzo.

## Fuentes en ArquitecturaBase

- **Reglas implementadas:**
  - [`docs/features/formularios.md`](../../../../ArquitecturaBase/docs/features/formularios.md);
  - [`docs/features/whatsapp.md`](../../../../ArquitecturaBase/docs/features/whatsapp.md);
  - [`docs/features/inteligencia-artificial.md`](../../../../ArquitecturaBase/docs/features/inteligencia-artificial.md).
- **Etapas:** [`docs/plans/formularios/README.md`](../../../../ArquitecturaBase/docs/plans/formularios/README.md) y sus resúmenes. Los ya retirados están en Git, con su procedencia en `docs/history/README.md`.
- **Decisiones:** ADR 0012 (Azure Blob), 0014 (credenciales de IA), 0017 (asistente de Inicio y operaciones durables), 0018 (robustez proporcional), 0019 (SQL acotado a catálogos) y 0020 (motor único de IA), en `docs/decisions/`.
- **Pantallas:** el lienzo «Formularios por WhatsApp» (https://claude.ai/artifact/HPbmDPLnr8JZ9TxevtTqJJ), con su índice, todos los tableros y el recorrido guiado. Su registro está en [`ArquitecturaBaseFront/docs/design/formularios-whatsapp.md`](../../../../ArquitecturaBaseFront/docs/design/formularios-whatsapp.md), con capturas.
- **Generador del lienzo:** `C:\Users\ezequ\source\repos\lienzo-formularios`. Los tableros se arman en Python con piezas comunes; sirve como referencia de la disposición exacta.

## Equivalencias para adaptar

Lo que en ArquitecturaBase es una sola instalación, acá se reparte entre la organización, la plataforma y, más adelante, la persona. La propuesta de partida está en la hoja de ruta; la tabla resume lo que más cambia:

| En ArquitecturaBase | Acá |
| --- | --- |
| Una instalación con un número de WhatsApp | Cada organización con su número y su WABA (E11). El número de la plataforma (E8) no manda formularios. |
| Formularios, catálogos, respuestas, reglas, PDF y archivos de la instalación | Privados de la organización (`ITenantOwned`, RLS), con su feature `forms`. |
| Permisos globales (`formtemplates.*`, `formrecords.*`, `formcatalogs.*`, `formrules.manage`, `formapprovals.decide`, `whatsapp.*`) | Permisos de organización. La plataforma tiene uno propio para la biblioteca de plantillas. |
| Catálogo «Tomarlas de la base, con filtros» sobre Usuarios y Roles | Sobre los miembros y roles de la organización (y empresas, si aplica). Nunca datos de otra organización. |
| La conexión de IA de la instalación | El asistente con tope mensual por organización (ADR 0051 de acá). |
| Azure Blob privado de la instalación | Almacenamiento por organización, con el adaptador de E10 y prefijo o contenedor por organización. |
| — | Biblioteca de plantillas de la plataforma (F3), que la organización toma como copia independiente. |
| — | B2C, después de F7, con datos compartidos persona↔organización. |

Antes de programar, cada etapa de acá necesita su tablero, dibujado con los componentes de este proyecto y respetando la disposición descrita en [05 · Pantallas](05-pantallas.md).
