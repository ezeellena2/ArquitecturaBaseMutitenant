# E12b · Ciclo de vida: dar de baja, reactivar, eliminar y copiar (resumen)

**Estado:** resumen de la segunda etapa del bloque (decisión 34). En ArquitecturaBase está programado y revisado. Se planifica con `/plan-etapa` cuando le toque, después de la E12.

**Dónde está el detalle:** en el traspaso 2 de ArquitecturaBase (`../ArquitecturaBase/docs/plans/2026-10-23-traspaso-multitenant/`), el anexo C §4 (V-01 a V-07) y el anexo D (BA1-A a BA6).

## Qué se construye

- **Back:**
  - **Dar de baja:**
    - es un dato propio de la definición (quién y cuándo), no un estado de la publicación. Conserva la versión activa, que usan la planilla y las aprobaciones;
    - con una publicación abierta responde 409;
    - un solo filtro de disponibilidad, compartido por el chat (E12), el asistente y las sugerencias (E18), deja de ofrecer los dados de baja y los eliminados;
    - los envíos abiertos conservan su versión y se responden hasta vencer. Respuestas, archivos, numeración, aprobaciones y reglas siguen disponibles;
    - las sugerencias de otros formularios que apuntan a él dejan de ofrecerse, y vuelven si se reactiva.
  - **Reactivar** es la publicación de siempre con el borrador actual: versión nueva y Flows nuevos. Sigue dado de baja hasta que se confirma la versión nueva, y la numeración continúa.
  - **Copiar un formulario** (74): crea uno nuevo en borrador con el borrador del original, con un prefijo nuevo propuesto con la regla de la E12. Pide `forms.manage`. No copia versiones, respuestas ni publicaciones. Las etapas siguientes suman lo suyo a la copia: con la E13, las preguntas de catálogo apuntan a los mismos catálogos, sin duplicarlos; con la E16, se copian el diseño del PDF y las reglas vigentes, como su primera revisión.
  - **Eliminar:**
    - es lógico, y solo se puede sin respuestas, sin envíos abiertos ni respuestas en proceso, y sin publicación en curso. Se comprueba bajo el mismo bloqueo que las otras escrituras;
    - no borra versiones ni evidencias del canal;
    - el prefijo queda reservado dentro de la organización.
  - **Deprecar los Flows en Meta**, como operación durable:
    - Forms declara por un puerto neutral los pares definición/versión que ya no se usan: versiones reemplazadas, dadas de baja o eliminadas. Una publicación abierta suspende esa autorización;
    - WhatsApp es el dueño de la operación, por cada WABA de la organización y con su token (E11). Un worker descubre el trabajo al vencer el último envío y entra con `ExecutionActor`;
    - cada binding se deprecia con lease, fence y estado. Lo incierto queda «Sin confirmar» y se consulta por id, nombre técnico y WABA antes de repetir;
    - cada respuesta de Meta se clasifica en definitiva, transitoria o desconocida. Un rechazo no se repite para siempre: después de esperar 5, 15, 30 y 60 minutos queda «No se pudo», a la vista en el editor, y no frena nada del formulario;
    - otro puerto, solo de lectura, le devuelve a Forms el último vencimiento y el estado de la deprecación.
  - **«Diseñar formularios»** filtra Activos (publicados y borradores), Publicados, Borradores y Dados de baja, con la columna Respuestas. Sin `formrecords.read`, la cantidad de respuestas es `null`.
- **Front:**
  - las acciones en la fila de «Diseñar formularios» y en el ⋮ del editor, según el permiso y el estado;
  - el editor de un dado de baja, con quién y cuándo, y lo que pasa en WhatsApp;
  - los diálogos de dar de baja, reactivar y eliminar.
- **Tableros que dibuja Diseño** (evaluación, §5.2), con nuestras piezas y la decisión 43:
  - `BA1-A` · Las acciones en la fila;
  - `BA2` · El editor;
  - `BA3` · El diálogo «Dar de baja»: qué deja de pasar, los envíos abiertos, WhatsApp y lo que se conserva;
  - `BA4` · Reactivar y eliminar;
  - `BA5` · El circuito de punta a punta;
  - `BA6` · Una sugerencia hacia un dado de baja. Se programa con la E18, que trae el editor de sugerencias;
  - `FRM-Copiar` · el diálogo de copiar, con el nombre y el prefijo propuesto, desde el ⋮ de «Diseñar formularios» y del editor (74).
- **Mandan sobre este resumen en:**
  - **una acción que no se puede usar** queda grisada, con el motivo en su tooltip, también en el ⋮ (43). Sin el permiso, no se dibuja;
  - **con respuestas, Eliminar no aparece;**
  - **si no se sabe si la baja o la eliminación salió,** la pantalla vuelve a leer sola, sin pisar lo escrito (43).

## Decisiones tomadas (no se vuelven a preguntar)

- **El ciclo de vida entra al bloque** como E12b (34).
- **Permisos:** dar de baja y reactivar piden `forms.publish`; eliminar pide `forms.manage`. Los confirmó la Guía el 2026-10-24.
- **La deprecación es por WABA de la organización,** con su token (evaluación, tema 18).
- **No se borran respuestas desde un formulario.**

## Fuera de esta etapa

- Borrar respuestas por antigüedad: rige la decisión 23.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba y `WhatsApp:FlowSessionHours` en 1:
1. dar de baja un formulario con un envío abierto, y responder ese envío;
2. al vencer, ver los Flows deprecados en el administrador de Meta;
3. reactivar: versión nueva, Flow nuevo y el número de respuesta siguiente;
4. eliminar un borrador, y eliminar uno publicado sin respuestas, que también deprecia sus Flows.

## Requisitos previos

La E12 cerrada.

## Ajustes ya decididos

- **Idioma:** el contenido no tiene idioma (35); los nombres de los formularios se ven como se escribieron.
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** el dato de la baja va en `FormDefinitions`, `ITenantOwned` con RLS.
- **Workers:** el de deprecación descubre trabajo en un registro técnico global sin datos de negocio, como el de publicación de la E12, y entra con `ExecutionActor`.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **WhatsApp (E11):** la deprecación usa la WABA y el token de cada organización.
