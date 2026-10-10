# E8b Parte 2a · Orden técnico de los cortes restantes

Se conserva el alcance autorizado T02–T09 y el contrato de Parte 1 con CC01–CC03 aprobados. Esta decisión ordena la implementación por dependencias; no agrega funciones ni declara completas tareas que todavía requieren composición.

## Decisiones tomadas

- T04 empieza por catálogo, herramientas tipadas, autorización, proyecciones, respuestas localizadas y ayuda bilingüe. Sus once consultas fijas no se consideran completas hasta persistir historial, mensajes y enlaces y reconstruir replay autorizado.
- Quick necesita el binding privado y el protocolo DurableReference de T05. La consulta Platform de uso necesita la lectura real de T06; su servicio todavía devuelve 501 en el corte T03. Se adelantan los fundamentos necesarios dentro del alcance autorizado, sin sustituir la lectura por ceros ficticios.
- La admisión de turnos T05 depende de cuotas y reservas T06 y del aviso explícito T08. Se implementan esos fundamentos antes de habilitar generación; cada corte coherente tiene tests focales, revisión SPEC/QUALITY y commit propio. El informe de cada corte distingue lo implementado de lo pendiente.
- El cierre de T04 exige las once consultas con resultados reales o el vacío personal explícito de ADR0049, sin proveedor ni consumo de cuota, más historial y replay reales. T05/T06/T08 conservan todos sus criterios originales al completar la composición.
- La acción `personal.organizations` usa `parameters.tenantId`, ya disponible en el contrato, obtenido de las organizaciones del perfil propio y autorizado de nuevo. Front la conecta mediante el flujo existente de Perfiles. No se inventa una ruta de listado ni un identificador.
- El historial HTTP conserva cursor ascendente, limitado y ligado al propietario, acceso y conversación. Front recorre páginas reales; la selección de contexto para el SDK es una lectura interna separada y no modifica la forma HTTP.

## Verificación final e Integración

Después de completar todos los cortes se ejecutan build y tests completos con Docker y se cotejan OpenAPI y tipos Front. Las pruebas focales con Fake verifican conducta y aislamiento; el recorrido real con Anthropic y el E2E conjunto siguen siendo requisitos de Integración. E8 y su recorrido Meta no se cierran con este trabajo.
