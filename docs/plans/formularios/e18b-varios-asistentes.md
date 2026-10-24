# E18b · Varios asistentes, uno por equipo (resumen)

**Estado:** resumen de una etapa nueva del bloque (decisiones 68, 69 y 77), justo después de la E18. No tiene equivalente programado en ArquitecturaBase, que lo dejó para una tercera etapa del asistente (anexo F, F2).

## Qué se construye

- **Back:**
  - **Varios asistentes por organización.** Cada uno tiene su nombre, su perfil, sus casos y sus límites por bandeja, como el de la E18. El conocimiento es de la organización y lo comparten todos (46).
  - **Quién contesta cada chat: por equipo** (68):
    - cada equipo de la bandeja de la E11 (Ventas, Soporte) puede tener un asistente; un equipo tiene como mucho uno;
    - **cómo llega un chat nuevo a su equipo** (77): con varios asistentes, el chat nuevo arranca con el menú del bot, aunque la bandeja esté en modo Asistente. La persona elige el equipo, y desde ahí le contesta el asistente de ese equipo;
    - un chat en modo Asistente lo contesta el asistente de su equipo. Un chat sin equipo, o de un equipo sin asistente, lo contesta el asistente principal;
    - si el chat cambia de equipo (por el menú del bot o porque un operador lo pasa), desde el mensaje siguiente contesta el asistente del equipo nuevo, con el mismo historial de esa conversación;
    - el chat muestra qué asistente contesta, y «Por qué respondió esto» lo nombra.
  - **La asignación** equipo → asistente la guarda el módulo WhatsApp, con el id del asistente como dato opaco. El asistente no conoce a los equipos.
  - **El modo de atención** (52) y los topes siguen siendo por bandeja y por chat. El tope mensual de IA es uno para toda la organización (ADR 0051).
  - **«Probar»** se elige por asistente.
- **Front:**
  - la lista «Asistentes» en el panel de WhatsApp, con «Nuevo asistente». Cada uno abre la pantalla de la E18;
  - en la configuración de cada equipo de la E11, «Asistente de este equipo»;
  - en el chat, qué asistente contesta.
- **Tableros que dibuja Diseño,** con nuestras piezas y la decisión 43:
  - `WA-Asistentes`: la lista de asistentes, con el equipo de cada uno;
  - `WA-Equipo-Asistente`: el asistente de un equipo;
  - en `AS4`, el nombre del asistente que contesta.

## Decisiones tomadas (no se vuelven a preguntar)

- **Por equipo** (68): usa los equipos que ya existen, es predecible y en cada chat se ve quién contesta.
- **El menú elige el equipo** (77): decide la persona, no la IA.
- **Etapa propia,** después de la E18, que ya es grande (69).
- **Sin permiso nuevo:** se configura con «Administrar WhatsApp» (28).

## Fuera de esta etapa

- Un asistente principal que lee el mensaje y elige a qué asistente pasarlo.
- Asistentes que se pasan el chat entre ellos.
- Un conocimiento propio de cada asistente.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con la IA y Meta**:
1. dos equipos con su asistente: Rosales elige «Soporte» en el menú y contesta el asistente de Soporte;
2. un operador pasa el chat a Ventas, y el mensaje siguiente lo contesta el asistente de Ventas, con el historial;
3. un chat sin equipo lo contesta el principal.

## Requisitos previos

La E18 cerrada.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** los asistentes y la asignación a equipos son `ITenantOwned`, con RLS.
- **Workers:** los de la E18.
- **Features:** `forms` y `assistant`.
- **WhatsApp (E11):** los equipos y el número de la organización.
