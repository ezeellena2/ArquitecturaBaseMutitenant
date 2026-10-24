# E13c · Datos de las personas y filtros por destinatario (resumen)

**Estado:** resumen de la quinta etapa del bloque (decisión 34). Es la segunda mitad de lo que ArquitecturaBase llama E2. Se planifica con `/plan-etapa` cuando le toque, después de la E13b.

**Dónde está el detalle:** en el traspaso 2 de ArquitecturaBase, el anexo C §11 (C2-05, C2-06 y C2-09), el anexo D (CE4-A) y el anexo E §2.7 (review R1023, puntos 18 a 20).

## Qué se construye

- **Back:**
  - **Los datos de las personas,** como «Zona»:
    - los define cada organización: clave reservada, nombre (un solo texto, 35), tipo (texto o elegir de un catálogo a mano), fuente, posición, estado y versión. Hasta 20, contando los inactivos (42);
    - son de la organización y del miembro: `ITenantOwned`, con clave por membresía y no por cuenta (42). Una persona puede ser «Centro» en una organización y no tener el dato en otra;
    - **los valores:** una fila por miembro, con su propia versión y un `PUT` delta. Un texto tiene hasta 300 caracteres; uno de catálogo exige una opción activa de la fuente, y el guardado que apunta a una opción dada de baja se conserva;
    - un dato que usa el borrador o la versión activa de un formulario no cambia de tipo ni de fuente, ni se desactiva. Si hay personas con el dato cargado, cambiar el tipo o la fuente pide confirmarlo y borra esos valores;
    - los bloqueos van en orden: definiciones, datos, fuentes; y para los valores, datos, persona, fuentes.
  - **Los filtros por destinatario,** en una pregunta de catálogo a mano:
    - filtros de igualdad, uno por campo de papel «Filtro»: con un valor fijo, o con un dato de quien recibe (el campo y el dato eligen de la misma fuente);
    - la preparación de la oferta resuelve al miembro real y filtra en SQL, antes del tope de 200 o 20;
    - **a quien no tiene el dato no se le manda, y se dice el motivo** (`Forms.Record.RecipientDataMissing`). Un filtro que dejó de funcionar da `Forms.Record.CatalogFilterUnavailable`. **Nunca se retira el filtro ni se manda la lista entera;**
    - el envío desde el chat, el asistente, el prellenado y las sugerencias usan esa misma preparación;
    - **los datos de las personas son de los empleados** (42): un cliente no los tiene. Una pregunta con filtro por destinatario no se puede mandar a un cliente, y la comprobación de publicar lo marca en un formulario que es para clientes (50).
  - **La vista previa «Como si lo recibiera»:** con una persona de ejemplo, pide `formcatalogs.read`, no guarda ni toma bloqueos, y distingue «Lista», «Le falta el dato» y «El filtro no funciona».
  - **Al publicar:** separa los problemas, que frenan, de dos avisos que no frenan (37):
    - el máximo de opciones por combinación de datos, contra 200 o 20;
    - las personas activas con `formrecords.create` que no tienen el dato.
  - **Un permiso nuevo, `formpersondata.manage`:** va en el catálogo del núcleo, en `Permissions.resx` y su `.en`, y en el seed de los roles de sistema, con su migración.
    - definir los datos pide `formpersondata.manage`, y ver su tabla de valores pide además `users.read`, sin correos;
    - cargar los valores desde la ficha del usuario pide `users.manage`.
  - **La baja de la cuenta:** el participante del módulo borra los datos del miembro. No van en «Exportar mis datos», porque son datos de la organización. Al terminar una membresía, sus valores se borran con ella.
- **Front:**
  - la pantalla «Datos de las personas», en Administración, con la tabla de valores;
  - el diálogo para definir un dato;
  - la pestaña «Datos» en la ficha del usuario. La aporta el módulo por un registro neutral del front, se guarda aparte y solo si cambió. Sin el módulo, no existe;
  - «Qué opciones ve cada persona» en una pregunta de catálogo, con la vista previa.
- **Tableros que dibuja Diseño** (evaluación, §5.2), con nuestras piezas y la decisión 43:
  - `CE4-A` · Filtrar por persona: datos de la persona;
  - el diálogo para definir un dato de las personas, que allá falta: un solo campo de nombre.

## Las lecciones del review de allá

Cada una lleva su test, escrito durante la etapa:
- **un filtro fijo que deja más de 20 opciones en «elegir varias»** frena al publicar (R1023 #18);
- **el valor fijo de un filtro** se valida contra su fuente (R1023 #19);
- **la vista previa «Como si lo recibiera»** exige `formcatalogs.read`, y no nombra opciones de un catálogo que la persona no puede leer (R1023 #20).

## Decisiones tomadas (no se vuelven a preguntar)

- **Por organización y por miembro** (42). Un dato no depende de la empresa.
- **Hasta 20 datos de las personas,** iguales para todas las organizaciones (42).
- **Los avisos de publicar** no frenan (37).
- **`formpersondata.manage`** es uno de los dos permisos nuevos del bloque; el otro es `formrecords.correct` (58).

## Fuera de esta etapa

- B2C: datos para personas clientes.
- Un dato que dependa de la empresa.
- Varios valores de un mismo dato por persona, y operadores distintos de «es igual a».
- Una columna de datos en la lista de usuarios.

## Cómo se cierra

La puerta de la etapa, con la lista de control de `/puerta`, más una **prueba real con Meta** con el número de una organización de prueba:
1. dos personas con distinta «Zona» reciben el mismo formulario, cada una con los proveedores de su zona;
2. una persona sin «Zona» no lo recibe, y el operador ve el motivo;
3. una persona que es miembro de dos organizaciones, con «Zona» en una sola, recibe según cada organización.

## Requisitos previos

La E13b cerrada.

## Ajustes ya decididos

- **Idioma:** un solo nombre por dato (35).
- **Botones:** según la decisión 43.

## En el multitenant

- **Datos de la organización:** definiciones y valores son `ITenantOwned` con RLS, y los valores van por membresía. Cada ruta nueva lleva su test de aislamiento.
- **Workers:** no hay.
- **Feature `forms`:** las rutas llevan `[FeatureGate("forms")]`.
- **El núcleo no conoce a Forms:** la pestaña «Datos» de la ficha del usuario entra por un registro neutral del front y por rutas del módulo, sin que el núcleo referencie al módulo.
