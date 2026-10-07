# Propuesta: el asistente (un LLM en el inicio) — borrador para decidir

Estado: borrador del 2026-10-07, para pensar con el usuario. Todavía no es un plan ejecutable ni una ADR. Lo pidió el usuario: un chat como Claude o Codex en el inicio de Plataforma, de la empresa (B2B) y de la persona (B2C), que responda solo cosas del sistema. La plataforma lo provee, y cada organización puede usar el de la plataforma o configurar el suyo.

## 1. Qué es, en una frase

Un **módulo quitable** (como WhatsApp, [modulos](../rules/modulos.md)) que pone en el inicio de cada lado un chat. El chat responde preguntas sobre los datos que esa persona ya puede ver, usando **herramientas de solo lectura** que llaman a los mismos servicios de la aplicación, con sus permisos y su aislamiento. Nunca toca la base de forma directa ni escribe nada.

## 2. Quién lo usa y con qué modelo

| Lado | Quién paga y configura | Qué puede consultar |
|---|---|---|
| **Plataforma** | La plataforma (el operador), con su clave | Organizaciones, cuentas, recuperaciones y auditoría, según los permisos del operador |
| **Empresa (B2B)** | Por defecto, la plataforma. La organización puede **traer su propia clave** (su proveedor y su modelo) | Lo de su organización, según los permisos de quien pregunta: usuarios, roles, empresas y auditoría |
| **Persona (B2C)** | Siempre la plataforma | Lo suyo: su cuenta, sus métodos de ingreso y, desde la Etapa 7, sus interacciones con empresas |

**Para decidir:** ¿la organización elige entre «usar el de la plataforma» y «el mío», o la plataforma decide quién puede traer su clave, por plan o por módulo habilitado?

## 3. Cómo se conecta al LLM

- **Un puerto propio:** `IAssistantModel` en `Application/Interfaces/Integrations/Assistant/`, con un adaptador por proveedor en Infrastructure: Anthropic primero, y OpenAI o compatible después. El núcleo no conoce al proveedor.
- **Configuración por alcance:** proveedor, modelo, clave y topes.
  - La de la plataforma va en user-secrets o variables de entorno, nunca en la base sin cifrar.
  - La de una organización se guarda cifrada (Data Protection) y nunca vuelve a mostrarse completa.
- **Respuesta en streaming** al front (SSE), con «Detener».
- **Herramientas («tools») de solo lectura,** declaradas en un catálogo y cada una con su permiso. Por ejemplo: `contar_usuarios`, `listar_roles` y `permisos_de_un_rol`. Cada una llama a un servicio de la aplicación **en nombre de quien pregunta**, con su tenant y sus permisos. El modelo nunca recibe datos que esa persona no podría ver en pantalla.
- **Las «consultas fijas»** del ejemplo («¿Cuántos usuarios activos hay?») son sugerencias por lado y por permiso, de un catálogo. Cada una puede usar una herramienta concreta.

## 4. Qué responde y qué no

- **Solo cosas del sistema:** un prompt de sistema por lado, más las herramientas. Lo demás se rechaza con un texto fijo.
- **Nunca escribe ni ejecuta acciones** en esta primera versión. A lo sumo, un enlace a la pantalla donde se hace.
- **Contra la inyección de instrucciones:** los datos que devuelven las herramientas van marcados como datos, y el modelo no recibe secretos.
- **Datos personales:** los correos y los teléfonos salen tapados como en pantalla, según [datos-personales](../rules/datos-personales.md).

## 5. Dónde se guardan las conversaciones

- **Una conversación y sus mensajes,** dueños de quien la creó:
  - en Plataforma, del operador;
  - en B2B, `ITenantOwned` y además de esa persona; ni el Dueño las ve;
  - en B2C, de la persona (`Personal`).
- **Historial a la izquierda,** como en el ejemplo: «Nueva conversación», renombrar y borrar.
- **Borrado:**
  - quien la creó la borra y se borra de verdad;
  - la baja de la cuenta borra todo;
  - **para decidir:** ¿un vencimiento automático, por ejemplo a los 90 días?
- **Lo que se guarda aparte, para medir el uso y no el contenido:** tokens de entrada y de salida, modelo, costo estimado, herramientas usadas y duración. Nunca el texto en los logs.

## 6. Topes y uso

- **Topes configurables:** mensajes por persona por día y tokens por organización por mes. Los fija la plataforma y, con su propia clave, la organización. Al llegar al tope, un aviso claro, no un error.
- **«Uso del asistente»:**
  - en Plataforma: por organización, por mes y su costo;
  - en la organización: su consumo, con los topes.
- **Para decidir:** ¿el asistente es un módulo que se vende, como habilitado o no por plan ([modulos-habilitados](../rules/modulos-habilitados.md)), o viene incluido para todos?

## 7. Pantallas (las dibuja Diseño)

- **El inicio de cada lado,** con el chat como el ejemplo:
  - historial a la izquierda;
  - «¿Qué querés consultar?», las sugerencias y «Detener»;
  - si no está configurado, «El asistente todavía no está configurado» y «Revisar configuración», solo para quien puede configurarlo.
- **Configuración del asistente:**
  - en Plataforma: proveedor, modelo, clave, topes y sugerencias;
  - en la organización: «Usar el de la plataforma» o «El nuestro», con su clave y sus topes.
- **El uso:** en Plataforma y en la organización.
- **El teléfono:** el chat a pantalla completa, con el historial en una hoja.
- **Para decidir:** ¿el chat **es** el inicio, como en el ejemplo, o una pestaña o un botón dentro del inicio actual? Hoy cada inicio tiene su contenido: «Para resolver», tarjetas y la billetera del teléfono.

## 8. Cómo entra en el plan

- **Una etapa nueva, «Asistente»,** **después de la Etapa 8** (WhatsApp como módulo). La 8 deja armado el patrón de módulo quitable con puertos, y el asistente lo reutiliza tal cual.
- **Las etapas existentes casi no se tocan.** Las herramientas reusan los servicios de lectura que ya hay. Solo hace falta que cada área exponga sus lecturas por servicio, lo que ya cumplen gracias a las capas.
- **Choca con:**
  - el inicio de cada lado (Etapas 4, 6 y 7) y la billetera del teléfono, según lo que se decida en el punto 7;
  - Plataforma (E5), que suma Configuración y Uso.
- **Se programa con la guía [etapa-en-paralelo](../guides/etapa-en-paralelo.md):** el contrato (conversaciones, mensajes en streaming, configuración y uso), después el back y el front en paralelo, y la integración al final.
- **Una ADR** con todo lo decidido: el módulo, el puerto, las herramientas de solo lectura, el dueño de cada conversación y el borrado.

## 9. Lo que hay que decidir con el usuario

1. ¿El chat es el inicio o una parte del inicio? (punto 7)
2. ¿Quién puede traer su propia clave? ¿Todas las organizaciones, o según el plan o el módulo? (punto 2)
3. ¿Viene incluido o es un módulo que se habilita? (punto 6)
4. ¿Las conversaciones vencen solas o quedan hasta que se borran? (punto 5)
5. ¿Qué proveedores en la primera versión? Recomendación: uno solo, Anthropic, con el puerto listo para sumar otros.
6. ¿Solo responde, o más adelante también ejecuta acciones con confirmación? Recomendación: solo responde en la primera versión.
7. ¿Las preguntas sugeridas son fijas por lado o las configura la plataforma y la organización?
